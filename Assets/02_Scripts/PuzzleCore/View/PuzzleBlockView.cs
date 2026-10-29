using Puzzle.Core;
using UnityEngine;
using DG.Tweening;
using System;

/// <summary>
/// 개별 퍼즐 블럭의 시각적 표현과 애니메이션을 담당하는 클래스입니다.
/// </summary>
public class PuzzleBlockView : MonoBehaviour    
{
    /// <summary> 블럭 이미지를 렌더링할 컴포넌트 </summary>
    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    /// <summary> 블럭의 물리적 충돌 및 입력을 담당하는 컴포넌트 </summary>
    [SerializeField]
    private PuzzleBlockCollider _boxCollider;

    /// <summary> 외부(Collider 등)에서 스프라이트 정보를 얻기 위한 프로퍼티 </summary>
    public SpriteRenderer SpriteRenderer => _spriteRenderer;

    /// <summary> 이 뷰와 연결된 블럭 모델 데이터 </summary>
    private Block _blockData;

    /// <summary> 이 블럭이 위치한 보드상의 그리드 좌표 </summary>
    private GridPos _gridPos;

    /// <summary> 현재 그리드 좌표를 반환하는 프로퍼티 (Controller 등에서 활용) </summary>
    public GridPos GridPos => _gridPos;

    /// <summary> 이 블럭을 관리하는 보드 뷰 참조 </summary>
    private PuzzleBoardView _boardView;

    /// <summary> 입력 소유권을 확인하기 위한 소속 보드입니다. </summary>
    public PuzzleBoardView BoardView => _boardView;

    /// <summary> 풀 반환과 새 요청마다 증가하여 이전 스프라이트 콜백을 무효화합니다. </summary>
    private ulong _visualRequestVersion;

    /// <summary> 풀 반환 또는 비활성화 시 대기 중인 이미지 요청을 무효화합니다. </summary>
    private void OnDisable()
    {
        _visualRequestVersion++;
    }

    /// <summary>
    /// 블럭 뷰를 특정 모델 데이터 및 좌표로 초기화합니다.
    /// </summary>
    /// <param name="blockData">연결할 블럭 모델 객체</param>
    /// <param name="pos">배치될 그리드 좌표</param>
    /// <param name="boardView">관리 중인 보드 뷰 객체</param>
    public void Initialize(Block blockData, GridPos pos, PuzzleBoardView boardView)
    {
        _blockData = blockData;
        _gridPos = pos;
        _boardView = boardView;

        // 생성되거나 풀에서 재사용될 때 이전의 애니메이션 상태(예: Scale 0)를 리셋
        transform.DOKill();
        transform.localScale = Vector3.one;

        UpdateVisual();
        UpdateStateVisual();
    }

    /// <summary>
    /// 모델 데이터의 상태에 맞춰 블럭의 스프라이트나 색상 등을 업데이트합니다.
    /// </summary>
    public void UpdateVisual()
    {
        ulong requestVersion = ++_visualRequestVersion;
        if (_blockData == null)
        {
            return;
        }

        if (_spriteRenderer != null)
        {
            // 새 이미지가 도착하기 전에는 이전 블럭 이미지를 노출하지 않습니다.
            _spriteRenderer.sprite = null;
            string blockId = _blockData.GetBlockId();
            string address = $"Block_{blockId}";

            // 풀 재사용 시 레이어가 리셋될 수 있으므로 명시적으로 설정
            _spriteRenderer.sortingLayerName = "Ingame";
            _spriteRenderer.sortingOrder = 10;

            /// <summary> 현재 요청의 스프라이트를 적용하고 충돌체 크기를 갱신합니다. </summary>
            /// <param name="sprite">로드한 블럭 스프라이트</param>
            void OnSpriteLoaded(Sprite sprite)
            {
                // 비동기 로드 도중 블럭이 파괴되었을 수 있으므로 null 체크 필수
                if (this == null || _spriteRenderer == null || requestVersion != _visualRequestVersion)
                {
                    return;
                }

                _spriteRenderer.sprite = sprite;

                if (_boxCollider != null)
                {
                    _boxCollider.AdjustColliderSize();
                }
            }

            /// <summary> 현재 스프라이트 요청의 실패를 기록합니다. </summary>
            void OnSpriteLoadFailed()
            {
                if (this == null || requestVersion != _visualRequestVersion)
                {
                    return;
                }
                Debug.LogWarning($"[PuzzleBlockView] 스프라이트 로드 실패! 주소: {address}");
            }

            AssetManager.AssetArguments<Sprite> args = new AssetManager.AssetArguments<Sprite>
            {
                address = address,
                successCallback = OnSpriteLoaded,
                failedCallback = OnSpriteLoadFailed
            };

            AssetManager.Instance.LoadAssetAsync(args);
        }
    }

    /// <summary>
    /// 블럭의 논리적 상태(State)에 따라 색상이나 크기 등 시각적 피드백을 업데이트합니다.
    /// </summary>
    public void UpdateStateVisual()
    {
        if (_blockData == null || _spriteRenderer == null)
        {
            return;
        }

        switch (_blockData.State)
        {
            case BlockState.Selected:
                // 선택 시 노란색 강조
                _spriteRenderer.color = Color.yellow;
                break;

            case BlockState.Matched:
                // 매칭 시 반투명하게 표시
                _spriteRenderer.color = new Color(1.0f, 1.0f, 1.0f, 0.5f);
                break;

            default:
                // 기본 상태
                _spriteRenderer.color = Color.white;
                break;
        }
    }

    /// <summary>
    /// 블럭이 클릭되었을 때의 시각적 연출을 수행합니다.
    /// </summary>
    /// <param name="onComplete">연출 완료 시 호출할 콜백</param>
    public void PlayClickAnimation(Action onComplete)
    {
        Tween tween = transform.DOScale(1.1f, 0.038f).SetLoops(2, LoopType.Yoyo);
        if (onComplete != null)
        {
            tween.OnComplete(() => onComplete.Invoke());
        }
    }

    /// <summary>
    /// 지정된 좌표로 부드럽게 이동하는 애니메이션을 수행합니다.
    /// </summary>
    /// <param name="targetLocalPos">이동할 로컬 좌표</param>
    /// <param name="onComplete">연출 완료 시 호출할 콜백</param>
    public void PlayMoveAnimation(Vector3 targetLocalPos, Action onComplete)
    {
        Tween tween = transform.DOLocalMove(targetLocalPos, 0.075f).SetEase(Ease.OutBack);
        if (onComplete != null)
        {
            tween.OnComplete(() => onComplete.Invoke());
        }
    }

    /// <summary>
    /// 지정된 좌표로 낙하하는 애니메이션을 수행합니다.
    /// </summary>
    /// <param name="targetLocalPos">이동할 로컬 좌표</param>
    /// <param name="onComplete">연출 완료 시 호출할 콜백</param>
    public void PlayFallAnimation(Vector3 targetLocalPos, Action onComplete)
    {
        float duration = 0.075f;
        Tween tween = transform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutQuad);
        if (onComplete != null)
        {
            tween.OnComplete(() => onComplete.Invoke());
        }
    }

    /// <summary>
    /// 블럭이 파괴될 때의 시각적 연출을 수행합니다.
    /// </summary>
    /// <param name="onComplete">연출 완료 시 호출할 콜백</param>
    public void PlayDestroyAnimation(Action onComplete)
    {
        Tween tween = transform.DOScale(0f, 0.075f).SetEase(Ease.InBack);
        if (onComplete != null)
        {
            tween.OnComplete(() => onComplete.Invoke());
        }
    }

    /// <summary>
    /// 블럭이 생성될 때의 시각적 연출을 수행합니다.
    /// </summary>
    /// <param name="onComplete">연출 완료 시 호출할 콜백</param>
    public void PlayCreateAnimation(Action onComplete)
    {
        transform.localScale = Vector3.zero;
        Tween tween = transform.DOScale(1.0f, 0.075f).SetEase(Ease.OutBack);
        if (onComplete != null)
        {
            tween.OnComplete(() => onComplete.Invoke());
        }
    }

    /// <summary>
    /// 이 뷰가 참조하고 있는 블럭 모델 데이터를 반환합니다.
    /// </summary>
    /// <returns>연결된 블럭 모델 데이터</returns>
    public Block GetBlockData() => _blockData;

    /// <summary>
    /// 보드 뷰(Raycast) 등에 의해 이 블럭이 클릭되었다고 판정되었을 때 호출됩니다.
    /// </summary>
    public void OnClicked()
    {
        // 연출 중이면 입력 무시
        if (_boardView == null || !_boardView.InputEnabled || _boardView.IsAnimating)
        {
            return;
        }

        // 로직 전달은 즉시 수행
        if (_boardView != null)
        {
            _boardView.OnBlockInput(_gridPos);
        }

        // 클릭 애니메이션 재생 (비동기)
        PlayClickAnimation(null);
    }
}
