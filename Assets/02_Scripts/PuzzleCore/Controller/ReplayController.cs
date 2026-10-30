using System.Collections.Generic;
using Puzzle.Core;
using Battle.Pivot;
using UnityEngine;
using TMPro;

/// <summary>
/// 상대방의 리플레이 데이터를 기반으로 보드를 자동 재생하는 컨트롤러입니다.
/// 기록된 Input/InputEnd를 프레임에 맞춰 주입하여 결정론적으로 동일한 게임을 재현합니다.
/// </summary>
public class ReplayController : MonoBehaviour
{
    [Header("View References")]
    /// <summary> 리플레이 보드를 그리는 뷰 객체 </summary>
    public PuzzleBoardView replayBoardView;

    [Header("배치 설정")]
    /// <summary> 카메라 뷰포트 기준 리플레이 보드의 축소 비율 (0~1) </summary>
    [Range(0.1f, 0.5f)]
    public float viewScale = 0.3f;

    /// <summary> 화면 가장자리로부터의 여백 (월드 단위) </summary>
    public float margin = 0.3f;

    /// <summary> 리플레이용 퍼즐 보드 모델 </summary>
    private IPuzzleBoard _board;

    /// <summary> 상대 보드의 파괴 통계를 관찰하는 독립 피벗 세션입니다. </summary>
    private BattlePivotSession _pivotSession;

    /// <summary> 재생할 리플레이 데이터 </summary>
    private ReplayData _replayData;

    /// <summary> 현재 논리 프레임 카운터 </summary>
    private ulong _frameCount;

    /// <summary> 다음에 처리할 입력 기록의 인덱스 </summary>
    private int _inputIndex;

    /// <summary> 다음에 처리할 입력 종료 기록의 인덱스 </summary>
    private int _inputEndIndex;

    /// <summary> 초기화 완료 여부 </summary>
    private bool _isInitialized = false;
    /// <summary> 기록 재생을 끝냈는지 여부입니다. </summary>
    private bool _isComplete;
    /// <summary> 기록된 종료 틱과 점수가 실제 재생 결과와 일치하는지 여부입니다. </summary>
    private bool _resultValid;
    /// <summary> 리플레이 보드의 남은 시간과 재생 상태를 표시합니다. </summary>
    [SerializeField] private TMP_Text _statusText;
    /// <summary> 리플레이 안내 문구를 결정하는 표시 상태입니다. </summary>
    private enum ReplayStatusState
    {
        /// <summary> 아직 표시하지 않은 상태입니다. </summary>
        None,
        /// <summary> 초기화하지 못한 상태입니다. </summary>
        Failed,
        /// <summary> 입력을 재생 중인 상태입니다. </summary>
        Playing,
        /// <summary> 입력을 모두 소비하고 종료를 기다리는 상태입니다. </summary>
        Waiting,
        /// <summary> 이전 형식의 기록 재생을 마친 상태입니다. </summary>
        LegacyComplete,
        /// <summary> 기록 검증을 통과하고 재생을 마친 상태입니다. </summary>
        Complete,
        /// <summary> 기록된 결과와 재생 결과가 다른 상태입니다. </summary>
        Mismatch
    }

    /// <summary> 마지막으로 표시한 재생 상태입니다. </summary>
    private ReplayStatusState _lastStatusState;
    /// <summary> 마지막으로 표시한 남은 초입니다. </summary>
    private int _lastStatusSeconds;
    /// <summary> 마지막으로 표시한 점수입니다. </summary>
    private int _lastStatusScore;
    /// <summary> 마지막으로 표시한 피벗의 0.1점 단위 값이며 -1은 미표시입니다. </summary>
    private int _lastPivotTenths;
    /// <summary> 마지막으로 표시한 피벗 최대치입니다. </summary>
    private int _lastPivotCapacity;
    /// <summary> 마지막으로 표시한 피벗 활성 여부입니다. </summary>
    private bool _lastPivotActive;
    /// <summary> 메인 게임에서 공동 틱 진행을 시작할 수 있는지 여부입니다. </summary>
    public bool IsInitialized => _isInitialized;
    /// <summary> 종료 점수를 비교할 수 있는 완주 기록인지 여부입니다. </summary>
    public bool HasValidResult => _isComplete && _resultValid;
    /// <summary> 재생 중인 기록이 현재 플레이어와 동일한 대전 점수 조건인지 확인합니다. </summary>
    /// <param name="settings">플레이어에게 적용된 피벗 설정</param>
    /// <returns>현재 콤보 및 피벗 규칙과 같으면 true</returns>
    public bool HasCompatibleBattleRules(PivotSettings settings)
    {
        if (_replayData == null)
        {
            return false;
        }
        return _replayData.HasCompatibleBattleRules(settings);
    }
    /// <summary> 현재 리플레이 점수입니다. </summary>
    public int CurrentScore
    {
        get
        {
            if (_board == null || _board.Objective == null)
            {
                return 0;
            }
            return _board.Objective.CurrentScore;
        }
    }

    /// <summary>
    /// 리플레이 데이터를 기반으로 보드를 생성하고 재생을 시작합니다.
    /// </summary>
    /// <param name="replayData">재생할 리플레이 데이터</param>
    public void Initialize(ReplayData replayData)
    {
        _isInitialized = false;
        _isComplete = false;
        _resultValid = false;
        _lastStatusState = ReplayStatusState.None;
        if (_pivotSession != null)
        {
            _pivotSession.Dispose();
            _pivotSession = null;
        }
        if (replayData == null || replayData.inputs == null || replayData.inputEnds == null)
        {
            Debug.LogError("[ReplayController] 리플레이 입력 데이터가 없습니다.");
            return;
        }
        _replayData = replayData;
        _frameCount = 0;
        _inputIndex = 0;
        _inputEndIndex = 0;

        // 리플레이 데이터에서 GameSpec 복원
        GameSpec replaySpec = new GameSpec();

        TextAsset ruleAsset = AssetManager.Instance.LoadAsset<TextAsset>(_replayData.ruleAddress);
        if (ruleAsset != null)
        {
            GameRuleContainer ruleContainer = JsonUtility.FromJson<GameRuleContainer>(ruleAsset.text);
            if (ruleContainer == null)
            {
                Debug.LogError("[ReplayController] 규칙 데이터가 없습니다.");
                return;
            }
            replaySpec.rule = ruleContainer.rule;
            replaySpec.blocks = ruleContainer.blocks;
        }
        else
        {
            Debug.LogError($"[ReplayController] 규칙 에셋 로드 실패: {_replayData.ruleAddress}");
            return;
        }

        TextAsset stageAsset = AssetManager.Instance.LoadAsset<TextAsset>(_replayData.stageAddress);
        if (stageAsset != null)
        {
            replaySpec.stageData = JsonUtility.FromJson<StageData>(stageAsset.text);
        }
        else
        {
            Debug.LogError($"[ReplayController] 스테이지 에셋 로드 실패: {_replayData.stageAddress}");
            return;
        }

        // 기록된 시드를 적용하여 동일한 난수 시퀀스 재현
        replaySpec.randomSeed = _replayData.randomSeed;
        replaySpec.UseLegacyAutomaticCombos = _replayData.comboRuleVersion == 0;
        if (_replayData.formatVersion == ReplayData.CurrentFormatVersion)
        {
            replaySpec.mode = _replayData.mode;
            replaySpec.rule.timeLimit = _replayData.timeLimit;
        }

        // 퍼즐 타입에 맞는 보드 생성
        switch (replaySpec.rule.puzzleType)
        {
            case PuzzleType.ThreeMatch:
                _board = new ThreeMatchPuzzleBoard();
                break;
            case PuzzleType.Link:
                _board = new LinkPuzzleBoard();
                break;
            case PuzzleType.TapMatch:
                _board = new TapMatchPuzzleBoard();
                break;
            default:
                _board = new ThreeMatchPuzzleBoard();
                break;
        }

        _board.Initialize(replaySpec);

        if (replaySpec.mode == GameMode.Battle)
        {
            PivotSettings pivotSettings = replayData.pivotSettings;
            if (pivotSettings == null)
            {
                pivotSettings = new PivotSettings();
            }
            try
            {
                _pivotSession = new BattlePivotSession(_board, replaySpec.mode, pivotSettings,
                    replaySpec.UseLegacyAutomaticCombos);
            }
            catch (System.ArgumentOutOfRangeException exception)
            {
                Debug.LogError($"[ReplayController] 피벗 설정 오류: {exception.Message}");
                return;
            }
        }

        // 뷰에 보드 그리기 (카메라 정렬 스킵, 보드 모양 직접 전달)
        if (replayBoardView != null)
        {
            replayBoardView.InputEnabled = false;
            replayBoardView.skipCameraAlign = true;
            replayBoardView.DrawBoard(_board, replaySpec.rule.boardShape);
            AlignToTopRight();
        }

        _isInitialized = true;
        Debug.Log($"[ReplayController] 리플레이 재생 시작 (입력 {_replayData.inputs.Count}개, 입력종료 {_replayData.inputEnds.Count}개)");
    }

    /// <summary>
    /// 메인 카메라 기준 우측 상단에 리플레이 보드를 축소 배치합니다.
    /// </summary>
    private void AlignToTopRight()
    {
        Camera cam = Camera.main;
        if (cam == null || _board == null)
        {
            return;
        }

        // 리플레이 보드뷰의 스케일 설정
        replayBoardView.transform.localScale = Vector3.one * viewScale;

        // 카메라 뷰 영역 계산
        float camHeight = cam.orthographicSize * 2f;
        float camWidth = camHeight * cam.aspect;

        // 리플레이 보드의 실제 월드 크기 (축소 적용)
        float boardWorldWidth = _board.Width * replayBoardView.cellSize * viewScale;
        float boardWorldHeight = _board.Height * replayBoardView.cellSize * viewScale;

        // 우측 상단 위치 계산 (카메라 중심 기준)
        float posX = (camWidth / 2f) - (boardWorldWidth / 2f) - margin;
        float posY = (camHeight / 2f) - (boardWorldHeight / 2f) - margin;

        replayBoardView.transform.position = new Vector3(posX, posY, 0f);
    }

    /// <summary> 현재 논리 프레임의 입력을 수신 순서대로 주입한 뒤 보드를 한 틱 진행합니다. </summary>
    public void AdvanceFrame()
    {
        if (!_isInitialized || _board == null || _isComplete)
        {
            return;
        }

        // 증가 전에 0번 프레임부터 처리합니다. 오래된 기록도 큐를 막지 않도록 소비합니다.
        while (true)
        {
            bool hasInput = _inputIndex < _replayData.inputs.Count &&
                _replayData.inputs[_inputIndex].frame <= _frameCount;
            bool hasEnd = _inputEndIndex < _replayData.inputEnds.Count &&
                _replayData.inputEnds[_inputEndIndex].frame <= _frameCount;
            if (!hasInput && !hasEnd)
            {
                break;
            }

            bool inputFirst = hasInput;
            if (hasInput && hasEnd)
            {
                InputRecord input = _replayData.inputs[_inputIndex];
                InputEndRecord end = _replayData.inputEnds[_inputEndIndex];
                // 순서 필드가 없는 기존 기록은 동일 프레임의 좌표 입력을 먼저 처리합니다.
                inputFirst = input.frame < end.frame ||
                    (input.frame == end.frame && input.sequence <= end.sequence);
            }

            if (inputFirst)
            {
                _board.Input(_replayData.inputs[_inputIndex++].position);
            }
            else
            {
                _board.InputEnd();
                _inputEndIndex++;
            }
        }

        _board.FixedUpdate();
        if (_pivotSession != null)
        {
            _pivotSession.AdvanceAfterBoardTick();
        }
        _frameCount++;
        bool hasEndFrame = _replayData.formatVersion == ReplayData.CurrentFormatVersion;
        if (_board.State == BoardState.Finish || (hasEndFrame && _frameCount >= _replayData.frameCount))
        {
            _isComplete = true;
            if (_pivotSession != null)
            {
                _pivotSession.Dispose();
            }
            _resultValid = hasEndFrame && _board.State == BoardState.Finish &&
                _frameCount == _replayData.frameCount && CurrentScore == _replayData.finalScore;
            if (hasEndFrame && !_resultValid)
            {
                Debug.LogError("[ReplayController] 기록된 종료 틱 또는 점수와 재생 결과가 다릅니다.");
            }
        }
    }

    /// <summary> 논리 판정과 독립적으로 리플레이 상태를 화면에 표시합니다. </summary>
    private void Update()
    {
        if (_statusText == null)
        {
            return;
        }
        _statusText.gameObject.SetActive(_replayData != null);
        if (_replayData == null)
        {
            return;
        }

        UpdateStatusPosition();
        ReplayStatusState statusState;
        int seconds = -1;
        if (!_isInitialized)
        {
            statusState = ReplayStatusState.Failed;
        }
        else if (_isComplete)
        {
            statusState = _replayData.formatVersion == 0 ? ReplayStatusState.LegacyComplete :
                (_resultValid ? ReplayStatusState.Complete : ReplayStatusState.Mismatch);
        }
        else
        {
            seconds = Mathf.CeilToInt(_board.Objective.RemainingTime);
            bool waiting = _inputIndex >= _replayData.inputs.Count && _inputEndIndex >= _replayData.inputEnds.Count;
            statusState = waiting ? ReplayStatusState.Waiting : ReplayStatusState.Playing;
        }
        int score = CurrentScore;
        int pivotTenths = -1;
        int pivotCapacity = 0;
        bool pivotActive = false;
        if (_isInitialized && !_isComplete && _pivotSession != null && _pivotSession.Mode.IsEnabled)
        {
            PivotMode pivot = _pivotSession.Mode;
            pivotTenths = pivot.GaugeUnits / 10;
            pivotCapacity = pivot.Capacity;
            pivotActive = pivot.IsActive;
        }
        // 표시값이 같으면 문자열 생성 자체를 건너뛰어 매 렌더 프레임의 할당을 막습니다.
        if (_lastStatusState == statusState && _lastStatusSeconds == seconds && _lastStatusScore == score &&
            _lastPivotTenths == pivotTenths && _lastPivotCapacity == pivotCapacity && _lastPivotActive == pivotActive)
        {
            return;
        }
        _lastStatusState = statusState;
        _lastStatusSeconds = seconds;
        _lastStatusScore = score;
        _lastPivotTenths = pivotTenths;
        _lastPivotCapacity = pivotCapacity;
        _lastPivotActive = pivotActive;

        string status;
        switch (statusState)
        {
            case ReplayStatusState.Failed:
                status = "리플레이를 불러오지 못했습니다";
                break;
            case ReplayStatusState.LegacyComplete:
                status = "이전 기록 · 재생 완료";
                break;
            case ReplayStatusState.Complete:
                status = "재생 완료";
                break;
            case ReplayStatusState.Mismatch:
                status = "기록 불일치 · 점수 비교 제외";
                break;
            default:
                string label = statusState == ReplayStatusState.Waiting ? "남은 입력 없음" : "재생 중";
                status = $"{label} · {seconds / 60}:{seconds % 60:00}";
                break;
        }
        string text = $"상대 리플레이\n{status}\n점수 {score}";
        if (pivotTenths >= 0)
        {
            string pivotState = pivotActive ? "피벗 모드" : "피벗 충전";
            text += $"\n{pivotState} · {pivotTenths / 10f:0.0} / {pivotCapacity}";
        }
        _statusText.text = text;
    }
    /// <summary> 컨트롤러 제거 시 상대 보드의 피벗 효과 구독을 정리합니다. </summary>
    private void OnDestroy()
    {
        if (_pivotSession != null)
        {
            _pivotSession.Dispose();
        }
    }

    /// <summary> 화면 비율에 관계없이 상태 텍스트를 작은 보드 바로 아래에 배치합니다. </summary>
    private void UpdateStatusPosition()
    {
        if (_board == null || replayBoardView == null || Camera.main == null)
        {
            return;
        }
        RectTransform parent = _statusText.rectTransform.parent as RectTransform;
        Canvas canvas = _statusText.GetComponentInParent<Canvas>();
        if (parent == null || canvas == null)
        {
            return;
        }
        float halfHeight = (_board.Height + 0.5f) * replayBoardView.cellSize * viewScale * 0.5f;
        Vector3 world = replayBoardView.transform.position - Vector3.up * (halfHeight + margin);
        Vector2 screen = Camera.main.WorldToScreenPoint(world);
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local))
        {
            _statusText.rectTransform.localPosition = new Vector3(local.x, local.y, 0f);
        }
    }

}
