using UnityEngine;
using Puzzle.Core;

/// <summary>
/// 게임 시작 시 필요한 스테이지 데이터와 규칙 정보를 관리하고 주입하는 클래스입니다.
/// 로비에서 결정된 스테이지 사양을 실제 게임 엔진(Model)에 전달하는 역할을 합니다.
/// </summary>
public class StageInjection
{
    #region Singleton
    private static StageInjection _instance;

    /// <summary> 전역 접근을 위한 싱글톤 인스턴스 </summary>
    public static StageInjection Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new StageInjection();
            }
            return _instance;
        }
    }
    #endregion

    /// <summary> 현재 구성된 게임 전체 사양서 </summary>
    private GameSpec _gameSpec;

    /// <summary> 마지막으로 사용된 규칙 에셋 주소 </summary>
    private string _ruleAddress;

    /// <summary> 마지막으로 사용된 스테이지 에셋 주소 </summary>
    private string _stageAddress;

    /// <summary> 상대방 리플레이 데이터 (null이면 리플레이 없음) </summary>
    private ReplayData _replayData;

    /// <summary>
    /// 현재 보관 중인 게임 사양서 객체를 반환합니다.
    /// </summary>
    /// <returns>구성된 GameSpec 객체</returns>
    public GameSpec GetGameSpec()
    {
        return _gameSpec;
    }

    /// <summary>
    /// 지정된 규칙과 스테이지 에셋 주소로부터 데이터를 로드하여 게임 사양서(GameSpec)를 완성합니다.
    /// </summary>
    /// <param name="ruleAddress">Addressable 내 규칙 JSON 에셋 주소</param>
    /// <param name="stageAddress">Addressable 내 스테이지 JSON 에셋 주소</param>
    /// <summary>
    /// 마지막으로 사용된 규칙 에셋 주소를 반환합니다.
    /// </summary>
    /// <returns>규칙 에셋 주소</returns>
    public string GetRuleAddress() => _ruleAddress;

    /// <summary>
    /// 마지막으로 사용된 스테이지 에셋 주소를 반환합니다.
    /// </summary>
    /// <returns>스테이지 에셋 주소</returns>
    public string GetStageAddress() => _stageAddress;

    /// <summary>
    /// 상대방 리플레이 데이터를 설정합니다. GameScene 진입 시 ReplayController가 참조합니다.
    /// </summary>
    /// <param name="replayData">리플레이 데이터 (null이면 리플레이 없음)</param>
    public void SetReplayData(ReplayData replayData)
    {
        _replayData = replayData;
    }

    /// <summary>
    /// 설정된 상대방 리플레이 데이터를 반환합니다.
    /// </summary>
    /// <returns>리플레이 데이터 (없으면 null)</returns>
    public ReplayData GetReplayData()
    {
        return _replayData;
    }

    /// <summary>
    /// 지정된 규칙과 모드별 스테이지 번호로부터 게임 사양서를 완성합니다.
    /// </summary>
    /// <param name="ruleAddress">Addressable 내 규칙 JSON 에셋 주소</param>
    /// <param name="puzzleType">로드할 스테이지의 퍼즐 모드</param>
    /// <param name="stageId">로드할 스테이지 번호</param>
    /// <returns>사양서 생성 성공 여부</returns>
    public bool MakeGameSpec(string ruleAddress, PuzzleType puzzleType, int stageId)
    {
        string stageKey = StageStorage.GetResourceKey(puzzleType, stageId);
        _ruleAddress = ruleAddress;
        _stageAddress = stageKey;
        _gameSpec = new GameSpec();

        if (!TryLoadRule(ruleAddress, _gameSpec))
        {
            _gameSpec = null;
            return false;
        }

        if (!StageStorage.TryLoadStageJson(puzzleType, stageId, out string stageJson))
        {
            Debug.LogError($"[StageInjection] 스테이지 JSON 로드 실패: {stageKey}");
            _gameSpec = null;
            return false;
        }

        if (!TryApplyStageJson(stageKey, stageJson, _gameSpec))
        {
            _gameSpec = null;
            return false;
        }

        _gameSpec.randomSeed = new System.Random().Next();
        return true;
    }

    /// <summary>
    /// 지정된 규칙과 스테이지 에셋 주소로부터 데이터를 로드하여 게임 사양서를 완성합니다.
    /// </summary>
    /// <param name="ruleAddress">Addressable 내 규칙 JSON 에셋 주소</param>
    /// <param name="stageAddress">Addressable 내 스테이지 JSON 에셋 주소</param>
    /// <returns>사양서 생성 성공 여부</returns>
    public bool MakeGameSpec(string ruleAddress, string stageAddress)
    {
        _ruleAddress = ruleAddress;
        _stageAddress = stageAddress;
        _gameSpec = new GameSpec();

        // 1. 규칙(Rule) 데이터 로드 및 파싱
        if (!TryLoadRule(ruleAddress, _gameSpec))
        {
            _gameSpec = null;
            return false;
        }

        // 2. 기존 메인/리플레이 경로는 Addressable 스테이지 에셋만 사용합니다.
        if (!TryLoadLegacyStage(stageAddress, _gameSpec))
        {
            _gameSpec = null;
            return false;
        }

        // 3. 결정론적 리플레이를 위한 랜덤 시드 생성
        _gameSpec.randomSeed = new System.Random().Next();
        return true;
    }

    /// <summary>
    /// 규칙 JSON 에셋을 로드하여 게임 사양서에 적용합니다.
    /// </summary>
    /// <param name="ruleAddress">Addressable 내 규칙 JSON 에셋 주소</param>
    /// <param name="gameSpec">규칙 데이터를 적용할 게임 사양서</param>
    /// <returns>규칙 로드 및 파싱 성공 여부</returns>
    private bool TryLoadRule(string ruleAddress, GameSpec gameSpec)
    {
        TextAsset ruleAsset = AssetManager.Instance.LoadAsset<TextAsset>(ruleAddress);
        if (ruleAsset == null)
        {
            Debug.LogError($"[StageInjection] 규칙 에셋 로드 실패: {ruleAddress}");
            return false;
        }

        GameRuleContainer ruleContainer = JsonUtility.FromJson<GameRuleContainer>(ruleAsset.text);
        if (ruleContainer == null)
        {
            Debug.LogError($"[StageInjection] 규칙 JSON 파싱 실패: {ruleAddress}");
            return false;
        }

        gameSpec.rule = ruleContainer.rule;
        gameSpec.blocks = ruleContainer.blocks;
        return true;
    }

    /// <summary>
    /// 기존 Addressable 스테이지 에셋을 로드하여 게임 사양서에 적용합니다.
    /// </summary>
    /// <param name="stageAddress">Addressable 내 스테이지 JSON 에셋 주소</param>
    /// <param name="gameSpec">스테이지 데이터를 적용할 게임 사양서</param>
    /// <returns>스테이지 로드 및 파싱 성공 여부</returns>
    private bool TryLoadLegacyStage(string stageAddress, GameSpec gameSpec)
    {
        TextAsset stageAsset = AssetManager.Instance.LoadAsset<TextAsset>(stageAddress);
        if (stageAsset != null)
        {
            return TryApplyStageJson(stageAddress, stageAsset.text, gameSpec);
        }

        Debug.LogError($"[StageInjection] 스테이지 에셋 로드 실패: {stageAddress}");
        return false;
    }

    /// <summary>
    /// 스테이지 JSON 문자열을 파싱하여 게임 사양서에 적용합니다.
    /// </summary>
    /// <param name="stageKey">로그에 표시할 스테이지 식별자</param>
    /// <param name="stageJson">파싱할 스테이지 JSON 문자열</param>
    /// <param name="gameSpec">스테이지 데이터를 적용할 게임 사양서</param>
    /// <returns>스테이지 파싱 및 검증 성공 여부</returns>
    private bool TryApplyStageJson(string stageKey, string stageJson, GameSpec gameSpec)
    {
        gameSpec.stageData = JsonUtility.FromJson<StageData>(stageJson);
        if (gameSpec.stageData == null)
        {
            Debug.LogError($"[StageInjection] 스테이지 JSON 파싱 실패: {stageKey}");
            return false;
        }

        if (gameSpec.stageData.cells == null || gameSpec.stageData.cells.Count == 0)
        {
            Debug.LogError($"[StageInjection] 스테이지 셀 데이터가 비어 있습니다: {stageKey}");
            return false;
        }

        return true;
    }
}
