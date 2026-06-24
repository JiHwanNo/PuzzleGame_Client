using UnityEngine;

/// <summary>
/// 루트 캔버스의 가장자리(상단/세이프상단/하단/세이프하단) 기준으로 UI 오브젝트의 세로 위치를 잡는 공용 컴포넌트입니다.
/// 캔버스가 match-width로 스케일되어 화면 비율마다 세로 가시 범위가 달라져도, 캔버스 rect와 Screen.safeArea를 읽어
/// 선택한 가장자리에 항상 정렬합니다(가로 위치는 작성된 값 유지). 에디트 모드에서도 동작합니다.
/// 전제: 루트 캔버스가 Screen Space - Overlay이고(픽셀↔캔버스 단위 환산이 scaleFactor로 일치) 회전·비균등 스케일이 없는 계층입니다.
/// 가로 세이프영역(좌우 노치, 가로 모드 등)은 의도적으로 처리하지 않습니다(세로 전용).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class CanvasEdgeAnchor : MonoBehaviour
{
    /// <summary> 배치 기준 가장자리입니다. </summary>
    public enum AnchorEdge
    {
        /// <summary> 캔버스 최상단입니다. </summary>
        Top,
        /// <summary> 세이프 영역 상단(노치 등 아래)입니다. </summary>
        SafeTop,
        /// <summary> 캔버스 최하단입니다. </summary>
        Bottom,
        /// <summary> 세이프 영역 하단(홈 인디케이터 등 위)입니다. </summary>
        SafeBottom
    }

    /// <summary> 배치 기준 가장자리입니다. </summary>
    [SerializeField]
    private AnchorEdge _edge = AnchorEdge.Bottom;

    /// <summary> 가장자리로부터 안쪽으로 띄울 여백입니다(캔버스 단위, +면 화면 안쪽). </summary>
    [SerializeField]
    private float _offset = 0f;

    /// <summary> 배치 대상 RectTransform입니다. </summary>
    private RectTransform _rt;

    /// <summary> 기준이 되는 루트 캔버스입니다. </summary>
    private Canvas _rootCanvas;

    /// <summary> 루트 캔버스의 RectTransform입니다. </summary>
    private RectTransform _canvasRt;

    /// <summary> 재배치 중 재진입을 막는 플래그입니다. </summary>
    private bool _applying;

    /// <summary> 마지막으로 적용한 화면 크기입니다(변경 감지용). </summary>
    private Vector2 _lastScreenSize;

    /// <summary> 마지막으로 적용한 세이프 영역입니다(변경 감지용). </summary>
    private Rect _lastSafeArea;

    /// <summary> 마지막으로 적용한 캔버스 rect 크기입니다(변경 감지용). </summary>
    private Vector2 _lastCanvasSize;

    /// <summary> 변경 감지 캐시 초기화 여부입니다. </summary>
    private bool _hasLayoutCache;

    /// <summary>
    /// 활성화 시 참조를 확보하고 즉시 재배치합니다.
    /// </summary>
    private void OnEnable()
    {
        Resolve();
        _hasLayoutCache = false;
        Apply();
    }

    /// <summary>
    /// 매 프레임 화면/세이프영역/캔버스 크기 변화를 감지해, 바뀐 경우에만 재배치합니다.
    /// Device Simulator 기기 전환·해상도 변경·회전에 에디트 모드와 런타임 모두 대응하기 위함입니다.
    /// </summary>
    private void Update()
    {
        if (HasLayoutChanged())
        {
            Apply();
        }
    }

    /// <summary>
    /// 화면 크기·세이프 영역·캔버스 rect 크기 중 하나라도 직전 적용 값과 달라졌는지 확인하고 캐시를 갱신합니다.
    /// </summary>
    /// <returns>레이아웃 기준 값이 바뀌었으면 true입니다.</returns>
    private bool HasLayoutChanged()
    {
        Resolve();
        if (_canvasRt == null)
        {
            return false;
        }

        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        Rect safeArea = Screen.safeArea;
        Vector2 canvasSize = _canvasRt.rect.size;

        if (_hasLayoutCache
            && screenSize == _lastScreenSize
            && safeArea == _lastSafeArea
            && canvasSize == _lastCanvasSize)
        {
            return false;
        }

        _lastScreenSize = screenSize;
        _lastSafeArea = safeArea;
        _lastCanvasSize = canvasSize;
        _hasLayoutCache = true;
        return true;
    }

    /// <summary>
    /// 캔버스/요소 크기(해상도·세이프영역 변화 포함)가 바뀌면 다시 배치합니다.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
        {
            Apply();
        }
    }

    /// <summary>
    /// 부모가 바뀌면(다른 캔버스로 이동 포함) 캐시된 캔버스 참조를 버리고 다시 배치합니다.
    /// </summary>
    private void OnTransformParentChanged()
    {
        _rootCanvas = null;
        _canvasRt = null;
        if (isActiveAndEnabled)
        {
            Resolve();
            Apply();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 인스펙터에서 값 변경 시 에디트 모드에서도 즉시 반영합니다.
    /// transform 변경은 OnValidate 호출 스택에서 직접 하면 경고/프리팹 문제가 생길 수 있어 다음 에디터 틱으로 미룹니다.
    /// </summary>
    private void OnValidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null)
            {
                return;
            }

            Resolve();
            Apply();
        };
    }
#endif

    /// <summary>
    /// 배치 기준 가장자리를 런타임에 변경하고 즉시 재배치합니다.
    /// </summary>
    /// <param name="edge">새 배치 기준 가장자리입니다.</param>
    public void SetEdge(AnchorEdge edge)
    {
        _edge = edge;
        Apply();
    }

    /// <summary>
    /// 필요한 참조(RectTransform, 루트 캔버스)를 확보합니다.
    /// </summary>
    private void Resolve()
    {
        if (_rt == null)
        {
            _rt = GetComponent<RectTransform>();
        }

        if (_rootCanvas == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _rootCanvas = canvas.rootCanvas;
                _canvasRt = _rootCanvas.GetComponent<RectTransform>();
            }
        }
    }

    /// <summary>
    /// 선택한 가장자리에 맞춰 요소의 세로 위치를 다시 계산해 적용합니다.
    /// </summary>
    public void Apply()
    {
        if (_applying)
        {
            return;
        }

        Resolve();
        if (_rt == null || _rootCanvas == null || _canvasRt == null)
        {
            return;
        }

        Rect canvasRect = _canvasRt.rect;

        // 캔버스 로컬 좌표 기준 가장자리 Y.
        float canvasTopY = canvasRect.yMax;
        float canvasBottomY = canvasRect.yMin;

        // 세이프 영역 인셋을 "화면 대비 비율"로 구해 캔버스 rect 높이에 적용한다.
        // 픽셀/scaleFactor(= rect.height == Screen.height/scaleFactor 항등) 방식과 달리 게임뷰/스케일 상태에 의존하지 않아 안정적이다.
        Rect safeArea = Screen.safeArea;
        float screenHeight = Screen.height;
        float topFraction = screenHeight > 0f ? (screenHeight - (safeArea.y + safeArea.height)) / screenHeight : 0f;
        float bottomFraction = screenHeight > 0f ? safeArea.y / screenHeight : 0f;
        float topInset = topFraction * canvasRect.height;
        float bottomInset = bottomFraction * canvasRect.height;

        float edgeY;
        bool fromTop;
        switch (_edge)
        {
            case AnchorEdge.Top:
                edgeY = canvasTopY;
                fromTop = true;
                break;
            case AnchorEdge.SafeTop:
                edgeY = canvasTopY - topInset;
                fromTop = true;
                break;
            case AnchorEdge.Bottom:
                edgeY = canvasBottomY;
                fromTop = false;
                break;
            default:
                // SafeBottom
                edgeY = canvasBottomY + bottomInset;
                fromTop = false;
                break;
        }

        // 요소의 가까운 변(상단 기준이면 윗변, 하단 기준이면 아랫변)을 edgeY에서 _offset만큼 안쪽에 둔다.
        float height = _rt.rect.height;
        float centerY;
        if (fromTop)
        {
            centerY = edgeY - _offset - height * 0.5f;
        }
        else
        {
            centerY = edgeY + _offset + height * 0.5f;
        }

        // 피벗이 중앙이 아닐 수 있으므로 피벗 지점의 캔버스 로컬 Y로 보정한 뒤 월드 Y로 변환한다.
        float pivotLocalY = centerY + (_rt.pivot.y - 0.5f) * height;
        Vector3 worldPivot = _canvasRt.TransformPoint(new Vector3(0f, pivotLocalY, 0f));

        Vector3 position = _rt.position;
        if (Mathf.Approximately(position.y, worldPivot.y))
        {
            return;
        }

        _applying = true;
        position.y = worldPivot.y;
        _rt.position = position;
        _applying = false;
    }
}
