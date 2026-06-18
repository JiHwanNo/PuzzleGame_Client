using System;
using System.Collections.Generic;
using Puzzle.Core;
using UnityEngine;

/// <summary>
/// 스테이지 맵 툴에서 StageData를 격자 셀 뷰로 렌더링하고 셀 클릭을 전달합니다.
/// 인게임 보드 뷰와 로직을 공유하지 않는 편집 전용 경량 그리드입니다.
/// </summary>
public class StageMapBoardView : MonoBehaviour
{
    /// <summary> 셀 하나를 표현할 셀 뷰 프리팹입니다. </summary>
    [SerializeField]
    private StageMapCellView _cellPrefab;

    /// <summary> 셀 뷰를 배치할 부모 RectTransform입니다. </summary>
    [SerializeField]
    private RectTransform _cellRoot;

    /// <summary> 셀 한 칸의 픽셀 크기입니다(좌하단 원점 기준 배치). </summary>
    [SerializeField]
    private float _cellSize = 64f;

    /// <summary> 좌표로 셀 뷰를 조회하기 위한 2차원 배열입니다. </summary>
    private StageMapCellView[,] _cellViews;

    /// <summary> 생성된 셀 뷰 목록입니다(정리용). </summary>
    private readonly List<StageMapCellView> _spawned = new List<StageMapCellView>();

    /// <summary> 현재 렌더링 중인 스테이지 데이터입니다. </summary>
    private StageData _stageData;

    /// <summary> 셀 클릭 시 (x, y)를 전달하는 이벤트입니다. </summary>
    public event Action<int, int> OnCellClicked;

    /// <summary>
    /// 스테이지 데이터로 그리드 전체를 다시 생성합니다.
    /// </summary>
    /// <param name="stageData">렌더링할 스테이지 데이터입니다.</param>
    public void Build(StageData stageData)
    {
        Clear();

        if (stageData == null || stageData.cells == null)
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
        _cellViews = new StageMapCellView[stageData.stage_width, stageData.stage_height];

        for (int i = 0; i < stageData.cells.Count; i++)
        {
            CellData cell = stageData.cells[i];
            if (cell == null || !IsInBounds(cell.x, cell.y))
            {
                continue;
            }

            StageMapCellView view = Instantiate(_cellPrefab, _cellRoot);
            RectTransform rect = view.transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(cell.x * _cellSize, cell.y * _cellSize);
            }

            view.Bind(cell, HandleCellClicked);
            _cellViews[cell.x, cell.y] = view;
            _spawned.Add(view);
        }
    }

    /// <summary>
    /// 지정한 좌표의 셀 한 칸만 다시 그립니다.
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

        CellData cell = FindCell(x, y);
        if (cell != null)
        {
            view.Refresh(cell);
        }
    }

    /// <summary>
    /// 생성된 셀 뷰를 모두 제거합니다.
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
