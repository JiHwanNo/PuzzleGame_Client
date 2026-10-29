using System.Collections.Generic;
using Puzzle.Core;
using UnityEngine;

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

    /// <summary>
    /// 리플레이 데이터를 기반으로 보드를 생성하고 재생을 시작합니다.
    /// </summary>
    /// <param name="replayData">재생할 리플레이 데이터</param>
    public void Initialize(ReplayData replayData)
    {
        _isInitialized = false;
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
    private void FixedUpdate()
    {
        if (!_isInitialized || _board == null || _board.State == BoardState.Finish)
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
        _frameCount++;
    }
}
