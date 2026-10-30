using Puzzle.Core;
using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 유저의 입력을 감지하고 퍼즐 보드 모델 및 뷰와 통신하여 전체 게임 루프를 제어하는 컨트롤러 클래스입니다.
/// </summary>
public class PuzzleGameController : MonoBehaviour
{
    [Header("View References")]
    /// <summary> 화면에 보드를 그리는 뷰 객체 </summary>
    public PuzzleBoardView boardView;

    /// <summary> 상대방 리플레이를 재생하는 컨트롤러 </summary>
    public ReplayController replayController;

    [Header("UI References")]
    /// <summary> 남은 시간을 표시하는 텍스트 (인스펙터에서 연결) </summary>
    [SerializeField]
    private TMP_Text _timerText;

    /// <summary> 현재 점수를 표시하는 텍스트 (인스펙터에서 연결) </summary>
    [SerializeField]
    private TMP_Text _scoreText;

    /// <summary> 대전 진행 안내와 최종 점수 비교를 표시합니다. </summary>
    [SerializeField] private TMP_Text _resultText;
    /// <summary> 이번 플레이의 종료 규칙입니다. </summary>
    private GameMode _mode;
    /// <summary> 두 보드를 함께 진행한 총 논리 틱 수입니다. </summary>
    private ulong _elapsedFrames;
    /// <summary> 상대 기록이 있는 대전인지 여부입니다. </summary>
    private bool _hasOpponent;

    /// <summary> 퍼즐 보드의 핵심 로직을 처리하는 모델 인터페이스 </summary>
    private IPuzzleBoard _board;

    /// <summary> 현재 드래그 중 마지막으로 머문 좌표 (되돌리기 등 경로 재진입 허용을 위해) </summary>
    private GridPos? _lastHoveredPos = null;

    /// <summary> 보드가 정상적으로 초기화되었는지 여부 </summary>
    private bool _isInitialized = false;

    /// <summary> 리플레이 저장이 이미 완료되었는지 여부 (중복 저장 방지) </summary>
    private bool _replaySaved = false;

    /// <summary> 캐싱된 메인 카메라 참조 </summary>
    private Camera _mainCamera;

    /// <summary> Physics2D.OverlapPoint 결과를 재사용하기 위한 버퍼 </summary>
    private readonly List<Collider2D> _hitBuffer = new List<Collider2D>(16);

    /// <summary> Physics2D.OverlapPoint에 사용할 캐싱된 필터 (매 프레임 할당 방지) </summary>
    private static readonly ContactFilter2D _noFilter = ContactFilter2D.noFilter;

    /// <summary> 타이머 텍스트 갱신 빈도 제어용 (이전 표시된 초 값) </summary>
    private int _lastDisplayedSeconds = -1;

    /// <summary> 점수 텍스트 갱신 빈도 제어용 (이전 표시된 점수 값) </summary>
    private int _lastDisplayedScore = -1;

    /// <summary>
    /// 게임 시작 시 스테이지 데이터를 로드하고 보드를 초기화합니다.
    /// </summary>
    private void Start()
    {
        GameSpec gameSpec = StageInjection.Instance.GetGameSpec();

        if (gameSpec == null || gameSpec.stageData == null)
        {
            Debug.LogWarning("GameSpec이 주입되지 않았거나 비어 있습니다! 테스트용 더미 데이터로 초기화합니다.");
            if (gameSpec == null)
            {
                gameSpec = new GameSpec();
            }
        }

        // 게임 타입에 따라 적절한 보드 구현체 생성
        switch (gameSpec.rule.puzzleType)
        {
            case PuzzleType.ThreeMatch:
                _board = new ThreeMatchPuzzleBoard();
                break;
            case PuzzleType.Link:
                _board = new LinkPuzzleBoard(); // 링크 전용 보드로 연결
                break;
            case PuzzleType.TapMatch:
                _board = new TapMatchPuzzleBoard();
                break;
            default:
                _board = new ThreeMatchPuzzleBoard();
                break;
        }

        _mode = gameSpec.mode;
        _elapsedFrames = 0;
        _board.Initialize(gameSpec);

        if (boardView != null)
        {
            boardView.DrawBoard(_board);
        }

        _mainCamera = Camera.main;
        _isInitialized = true;

        // 상대방 리플레이 데이터가 있으면 리플레이 컨트롤러 초기화
        ReplayData replayData = StageInjection.Instance.GetReplayData();
        _hasOpponent = replayData != null;
        if (_resultText != null)
        {
            _resultText.text = _mode == GameMode.Battle ?
                (_hasOpponent ? "60초 대전" : "60초 대전 · 첫 기록 도전") : "";
        }
        if (replayData != null && replayController != null)
        {
            replayController.Initialize(replayData);
            StageInjection.Instance.SetReplayData(null); // 사용 후 초기화
        }
    }

    /// <summary>
    /// 매 프레임 입력 신호를 감지하고 보드 상태를 업데이트합니다.
    /// </summary>
    private void Update()
    {
        if (!_isInitialized || _board == null)
        {
            return;
        }

        // 1. 마우스/터치를 누르고 있는 중인가?
        if (_board.State != BoardState.Finish && IsPointerHeld())
        {
            // 뷰가 애니메이션 연출 중일 때는 어떠한 입력도 받지 않도록 차단합니다!
            if (boardView == null || !boardView.IsAnimating)
            {
                Vector2 screenPosition = GetPointerPosition();
                Vector2 worldPosition = _mainCamera.ScreenToWorldPoint(screenPosition);

                // 현재 위치 아래의 콜라이더 감지 (사전 할당 버퍼 사용으로 GC 방지)
                int hitCount = Physics2D.OverlapPoint(worldPosition, _noFilter, _hitBuffer);

                // 모든 충돌체를 순회하며 PuzzleBlockCollider를 찾습니다.
                for (int i = 0; i < hitCount; i++)
                {
                    Collider2D hitCollider = _hitBuffer[i];
                    if (hitCollider.TryGetComponent<PuzzleBlockCollider>(out PuzzleBlockCollider blockCollider))
                    {
                        GridPos? pos = GetGridPosFromCollider(hitCollider);
                        // 이전에 머물렀던 블럭과 다른 블럭으로 마우스가 이동했을 때만 입력 처리
                        if (pos.HasValue && pos.Value != _lastHoveredPos)
                        {
                            _lastHoveredPos = pos.Value;
                            blockCollider.OnClickBlock();
                            break;
                        }
                    }
                }
            }
        }

        // 2. 마우스/터치를 뗐는가? -> 입력 종료 및 경로 초기화
        if (IsPointerReleased())
        {
            _board.InputEnd();
            _lastHoveredPos = null; // 포인터를 떼면 초기화
        }

        // 4. UI 갱신
        UpdateTimerUI();
        UpdateScoreUI();

        // 게임 종료 시 기록을 저장하고, 대전이면 점수 비교를 표시한 뒤 로비로 이동합니다.
        if (_board.State == BoardState.Finish && !_replaySaved)
        {
            _replaySaved = true;
            SaveReplay();
            if (_mode == GameMode.Battle)
            {
                ShowBattleResult();
                StartCoroutine(ReturnToLobbyAfterResult());
            }
            else
            {
                Main.Instance.MoveScene(SceneEnum.GameScene, SceneEnum.LobbyScene);
            }
        }
    }

    /// <summary>
    /// 고정 프레임 간격으로 논리 프레임을 업데이트합니다.
    /// </summary>
    private void FixedUpdate()
    {
        if (!_isInitialized || _board == null || _board.State == BoardState.Finish)
        {
            return;
        }

        // Unity의 컴포넌트 실행 순서와 무관하게 두 보드를 같은 공통 틱에서 진행합니다.
        _board.FixedUpdate();
        if (_hasOpponent && replayController != null && replayController.IsInitialized)
        {
            replayController.AdvanceFrame();
        }
        _elapsedFrames++;
    }

    /// <summary> 공통 제한 시간이 끝난 뒤 점수와 승패를 표시합니다. </summary>
    private void ShowBattleResult()
    {
        int score = _board.Objective.CurrentScore;
        string result;
        if (!_hasOpponent)
        {
            result = $"대전 기록 완료 · {score}점";
        }
        else if (replayController == null || !replayController.HasValidResult)
        {
            result = $"대전 종료 · 내 점수 {score}\n상대 기록 확인 실패 · 승패 비교 제외";
        }
        else
        {
            int opponentScore = replayController.CurrentScore;
            string verdict = score > opponentScore ? "승리" : (score < opponentScore ? "패배" : "무승부");
            result = $"{verdict}\n내 점수 {score} : 상대 점수 {opponentScore}";
        }
        if (_resultText != null)
        {
            _resultText.text = result + "\n잠시 후 로비로 이동합니다";
        }
        Debug.Log($"[PuzzleGameController] {result}");
    }

    /// <summary> 최종 점수 비교를 읽을 수 있도록 표시한 후 로비로 이동합니다. </summary>
    /// <returns>결과 표시 대기 코루틴</returns>
    private IEnumerator ReturnToLobbyAfterResult()
    {
        yield return new WaitForSecondsRealtime(4f);
        Main.Instance.MoveScene(SceneEnum.GameScene, SceneEnum.LobbyScene);
    }

    /// <summary>
    /// 남은 시간을 UI 텍스트에 반영합니다. 초 단위가 변경될 때만 갱신하여 불필요한 문자열 할당을 방지합니다.
    /// </summary>
    private void UpdateTimerUI()
    {
        if (_timerText == null || _board.Objective == null)
        {
            return;
        }

        float remaining = _board.Objective.RemainingTime;
        int seconds = Mathf.CeilToInt(remaining);
        if (seconds < 0)
        {
            seconds = 0;
        }

        if (seconds != _lastDisplayedSeconds)
        {
            _lastDisplayedSeconds = seconds;
            int min = seconds / 60;
            int sec = seconds % 60;
            _timerText.SetText("{0}:{1:00}", min, sec);
        }
    }

    /// <summary>
    /// 현재 점수를 UI 텍스트에 반영합니다. 값이 변경될 때만 갱신합니다.
    /// </summary>
    private void UpdateScoreUI()
    {
        if (_scoreText == null || _board.Objective == null)
        {
            return;
        }

        int score = _board.Objective.CurrentScore;
        if (score != _lastDisplayedScore)
        {
            _lastDisplayedScore = score;
            _scoreText.SetText("{0}", score);
        }
    }

    /// <summary>
    /// 충돌한 콜라이더로부터 그리드 좌표를 가져옵니다.
    /// </summary>
    /// <param name="col">충돌한 콜라이더</param>
    /// <returns>블럭 뷰가 가진 그리드 좌표 혹은 null</returns>
    private GridPos? GetGridPosFromCollider(Collider2D col)
    {
        PuzzleBlockView view = col.GetComponentInParent<PuzzleBlockView>();
        if (view != null && view.BoardView == boardView && boardView != null && boardView.InputEnabled)
        {
            return view.GridPos;
        }
        return null;
    }

    /// <summary>
    /// 현재 화면 클릭 또는 터치가 유지되고 있는지 확인합니다.
    /// </summary>
    private bool IsPointerHeld()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 현재 화면 클릭 또는 터치가 종료되었는지 확인합니다.
    /// </summary>
    private bool IsPointerReleased()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 게임 종료 시 리플레이 데이터를 수집하여 로컬 JSON 파일로 저장합니다.
    /// </summary>
    private void SaveReplay()
    {
        GameSpec spec = StageInjection.Instance.GetGameSpec();
        if (spec == null)
        {
            Debug.LogError("[PuzzleGameController] GameSpec이 null이므로 리플레이를 저장할 수 없습니다.");
            return;
        }

        ReplayData replayData = new ReplayData
        {
            formatVersion = ReplayData.CurrentFormatVersion,
            mode = _mode,
            timeLimit = _board.Objective.TotalLimitTime,
            frameCount = _elapsedFrames,
            finalScore = _board.Objective.CurrentScore,
            ruleAddress = StageInjection.Instance.GetRuleAddress(),
            stageAddress = StageInjection.Instance.GetStageAddress(),
            randomSeed = spec.randomSeed,
            inputs = _board.GetRecordedInputs(),
            inputEnds = _board.GetRecordedInputEnds(),
            recordedAt = DateTime.Now.ToString("o")
        };

        ReplayStorage.Save(replayData);
    }

    /// <summary>
    /// 현재 포인터의 스크린 좌표를 가져옵니다.
    /// </summary>
    private Vector2 GetPointerPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return Touchscreen.current.primaryTouch.position.ReadValue();
        }
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
        return Vector2.zero;
    }
}
