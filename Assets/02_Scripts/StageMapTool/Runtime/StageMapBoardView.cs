using System;
using System.Collections.Generic;
using Puzzle.Core;
using UnityEngine;

/// <summary>
/// 스테이지 맵 툴에서 StageData를 격자 셀 뷰로 렌더링하고 셀 클릭을 전달합니다.
/// 빈 칸("+")을 포함한 전체 격자를 그리며, Link 모드의 헥사 오프셋과 선택 하이라이트를 지원합니다.
/// 인게임 보드 뷰와 로직을 공유하지 않는 편집 전용 경량 그리드입니다.
/// _fitTarget(예: BoardBackground)을 지정하면 그리드를 그 영역 중앙에 맞추고 영역 크기에 비례해 균등 스케일합니다.
/// </summary>
[ExecuteAlways]
public class StageMapBoardView : MonoBehaviour
{
    /// <summary> 셀 하나를 표현할 셀 뷰 프리팹입니다. </summary>
    [SerializeField]
    private StageMapCellView _cellPrefab;

    /// <summary> 셀 뷰를 배치할 부모 RectTransform입니다(_fitTarget과 형제여야 함 — 스케일이 영역에 영향 주지 않도록). </summary>
    [SerializeField]
    private RectTransform _cellRoot;

    /// <summary> 그리드를 가운데 맞춰 채울 대상 영역입니다(예: BoardBackground). 비우면 스케일/중앙정렬을 하지 않습니다. </summary>
    [SerializeField]
    private RectTransform _fitTarget;

    /// <summary> fit 시 대상 영역 대비 여백 비율입니다(0.1~1, 1이면 영역에 가득). </summary>
    [SerializeField]
    [Range(0.1f, 1f)]
    private float _fitPadding = 0.92f;

    /// <summary> 셀 한 칸의 픽셀 크기입니다(좌하단 원점 기준 배치). </summary>
    [SerializeField]
    private float _cellSize = 64f;

    /// <summary> 셀 사이의 간격(px)입니다. 타일이 서로 구분되어 보이도록 합니다. </summary>
    [SerializeField]
    private float _cellSpacing = 6f;

    /// <summary> 마지막으로 fit 적용한 대상 영역 크기입니다(변경 감지용). </summary>
    private Vector2 _lastFitSize;

    /// <summary> 마지막으로 fit 적용한 대상 영역 월드 위치입니다(변경 감지용). </summary>
    private Vector3 _lastFitWorldPosition;

    /// <summary> fit 변경 감지 캐시 초기화 여부입니다. </summary>
    private bool _hasFitCache;

    /// <summary> 좌표로 셀 뷰를 조회하기 위한 2차원 배열입니다. </summary>
    private StageMapCellView[,] _cellViews;

    /// <summary> 생성된 셀 뷰 목록입니다(정리용). </summary>
    private readonly List<StageMapCellView> _spawned = new List<StageMapCellView>();

    /// <summary> 현재 렌더링 중인 스테이지 데이터입니다. </summary>
    private StageData _stageData;

    /// <summary> 헥사(Even-Q Flat-Top) 오프셋 적용 여부입니다. </summary>
    private bool _hexLayout;

    /// <summary> 현재 선택된 셀의 X 좌표입니다. 선택이 없으면 -1입니다. </summary>
    private int _selectedX = -1;

    /// <summary> 현재 선택된 셀의 Y 좌표입니다. 선택이 없으면 -1입니다. </summary>
    private int _selectedY = -1;

    /// <summary> 셀 클릭 시 (x, y)를 전달하는 이벤트입니다. </summary>
    public event Action<int, int> OnCellClicked;

    /// <summary>
    /// 스테이지 데이터로 그리드 전체(빈 칸 포함)를 다시 생성합니다.
    /// </summary>
    /// <param name="stageData">렌더링할 스테이지 데이터입니다.</param>
    /// <param name="hexLayout">헥사 오프셋(Link 모드) 적용 여부입니다.</param>
    public void Build(StageData stageData, bool hexLayout)
    {
        Clear();

        if (stageData == null)
        {
            Debug.LogError("[StageMapBoardView] 렌더링할 스테이지 데이터가 없습니다.");
            return;
        }

        if (_cellPrefab == null || _cellRoot == null)
        {
            Debug.LogError("[StageMapBoardView] 셀 프리팹 또는 셀 루트가 설정되지 않았습니다.");
            return;
        }

        _stageData = stageData;
        _hexLayout = hexLayout;
        int width = stageData.stage_width;
        int height = stageData.stage_height;
        _cellViews = new StageMapCellView[width, height];

        // 데이터 유무와 무관하게 전체 격자를 빈 칸("+")까지 모두 생성한다.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                StageMapCellView view = Instantiate(_cellPrefab, _cellRoot);
                RectTransform rect = view.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = GetCellPosition(x, y);
                }

                view.Bind(x, y, FindCell(x, y), HandleCellClicked);
                _cellViews[x, y] = view;
                _spawned.Add(view);
            }
        }

        ApplyFit();
    }

    /// <summary>
    /// 대상 영역(_fitTarget)의 크기·위치가 바뀌면 그리드 fit을 다시 적용합니다(해상도/반응형 대응).
    /// </summary>
    private void Update()
    {
        if (_fitTarget == null || _cellRoot == null)
        {
            return;
        }

        Vector2 size = _fitTarget.rect.size;
        Vector3 worldPosition = _fitTarget.position;
        if (_hasFitCache && size == _lastFitSize && worldPosition == _lastFitWorldPosition)
        {
            return;
        }

        _lastFitSize = size;
        _lastFitWorldPosition = worldPosition;
        _hasFitCache = true;
        ApplyFit();
    }

    /// <summary>
    /// 현재 그리드를 _fitTarget 영역 중앙에 맞추고, 영역 안에 들어가도록 균등 스케일합니다.
    /// 셀 컨테이너(_cellRoot)는 _fitTarget과 형제이므로 스케일이 영역 크기에 영향을 주지 않습니다.
    /// </summary>
    public void ApplyFit()
    {
        if (_cellRoot == null || _fitTarget == null || _stageData == null)
        {
            return;
        }

        float step = _cellSize + _cellSpacing;
        float spanX = (_stageData.stage_width - 1) * step + _cellSize;
        float spanY = (_stageData.stage_height - 1) * step + _cellSize;
        if (spanX <= 0f || spanY <= 0f)
        {
            return;
        }

        Rect target = _fitTarget.rect;
        float scale = Mathf.Min(target.width / spanX, target.height / spanY) * _fitPadding;

        // 셀 컨테이너를 균등 스케일하고 대상 영역의 월드 중심에 맞춘다(그리드는 컨테이너 원점 기준 중앙 배치됨).
        // 대상 피벗이 0.5가 아니어도 정확히 가운데 오도록 rect.center(피벗 무관 시각 중심)를 월드로 변환해 사용한다.
        _cellRoot.localScale = new Vector3(scale, scale, 1f);
        _cellRoot.position = _fitTarget.TransformPoint(target.center);
    }

    /// <summary>
    /// 지정한 좌표의 셀 한 칸만 선택 상태를 반영해 다시 그립니다.
    /// </summary>
    /// <param name="x">갱신할 X 좌표입니다.</param>
    /// <param name="y">갱신할 Y 좌표입니다.</param>
    public void RefreshCell(int x, int y)
    {
        if (_stageData == null || !IsInBounds(x, y))
        {
            return;
        }

        StageMapCellView view = _cellViews[x, y];
        if (view == null)
        {
            return;
        }

        view.Refresh(FindCell(x, y), x == _selectedX && y == _selectedY);
    }

    /// <summary>
    /// 지정 좌표를 선택 상태로 만들고 이전 선택을 해제합니다.
    /// </summary>
    /// <param name="x">선택할 X 좌표입니다.</param>
    /// <param name="y">선택할 Y 좌표입니다.</param>
    public void Select(int x, int y)
    {
        int prevX = _selectedX;
        int prevY = _selectedY;
        _selectedX = x;
        _selectedY = y;

        if (IsInBounds(prevX, prevY))
        {
            RefreshCell(prevX, prevY);
        }

        RefreshCell(x, y);
    }

    /// <summary>
    /// 현재 선택을 해제합니다.
    /// </summary>
    public void ClearSelection()
    {
        int prevX = _selectedX;
        int prevY = _selectedY;
        _selectedX = -1;
        _selectedY = -1;

        if (IsInBounds(prevX, prevY))
        {
            RefreshCell(prevX, prevY);
        }
    }

    /// <summary>
    /// 좌표에 대응하는 셀 뷰의 배치 위치를 계산합니다(헥사면 짝수 열을 반 칸 내림).
    /// </summary>
    /// <param name="x">셀 X 좌표입니다.</param>
    /// <param name="y">셀 Y 좌표입니다.</param>
    /// <returns>좌하단 원점 기준 anchoredPosition입니다.</returns>
    private Vector2 GetCellPosition(int x, int y)
    {
        float step = _cellSize + _cellSpacing;

        // 격자를 CellRoot 원점 기준으로 가운데 정렬한다(크기와 무관하게 항상 중앙).
        int width = _stageData != null ? _stageData.stage_width : 0;
        int height = _stageData != null ? _stageData.stage_height : 0;
        float halfSpanX = ((width - 1) * step + _cellSize) * 0.5f;
        float halfSpanY = ((height - 1) * step + _cellSize) * 0.5f;

        float xPos = x * step - halfSpanX;
        float yPos = y * step - halfSpanY;

        // 헥사(Even-Q Flat-Top): 짝수 열을 반 칸 아래로 배치 (인게임 PuzzleBoardView와 동일 규칙).
        if (_hexLayout && x % 2 == 0)
        {
            yPos -= step * 0.5f;
        }

        return new Vector2(xPos, yPos);
    }

    /// <summary>
    /// 생성된 셀 뷰를 모두 제거하고 선택을 초기화합니다.
    /// </summary>
    private void Clear()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
            {
                Destroy(_spawned[i].gameObject);
            }
        }

        _spawned.Clear();
        _cellViews = null;
        _stageData = null;
        _selectedX = -1;
        _selectedY = -1;
    }

    /// <summary>
    /// 셀 클릭 이벤트를 외부로 전달합니다.
    /// </summary>
    /// <param name="x">클릭한 X 좌표입니다.</param>
    /// <param name="y">클릭한 Y 좌표입니다.</param>
    private void HandleCellClicked(int x, int y)
    {
        OnCellClicked?.Invoke(x, y);
    }

    /// <summary>
    /// 좌표가 현재 보드 범위 안인지 확인합니다.
    /// </summary>
    /// <param name="x">검사할 X 좌표입니다.</param>
    /// <param name="y">검사할 Y 좌표입니다.</param>
    /// <returns>범위 안이면 true입니다.</returns>
    private bool IsInBounds(int x, int y)
    {
        return _cellViews != null
            ? x >= 0 && x < _cellViews.GetLength(0) && y >= 0 && y < _cellViews.GetLength(1)
            : _stageData != null && x >= 0 && x < _stageData.stage_width && y >= 0 && y < _stageData.stage_height;
    }

    /// <summary>
    /// 현재 스테이지 데이터에서 좌표에 대응하는 셀을 찾습니다.
    /// </summary>
    /// <param name="x">조회할 X 좌표입니다.</param>
    /// <param name="y">조회할 Y 좌표입니다.</param>
    /// <returns>좌표에 대응하는 셀 데이터입니다. 없으면 null입니다.</returns>
    private CellData FindCell(int x, int y)
    {
        if (_stageData == null || _stageData.cells == null)
        {
            return null;
        }

        for (int i = 0; i < _stageData.cells.Count; i++)
        {
            CellData cell = _stageData.cells[i];
            if (cell != null && cell.x == x && cell.y == y)
            {
                return cell;
            }
        }

        return null;
    }
}
