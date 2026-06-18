using System;
using Puzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 맵 툴 그리드의 단일 셀 뷰입니다. 셀 타입 틴트와 블럭 라벨을 표시하고 클릭을 전달합니다.
/// </summary>
public class StageMapCellView : MonoBehaviour
{
    /// <summary> 셀 배경 틴트를 표시할 이미지입니다. </summary>
    [SerializeField]
    private Image _background;

    /// <summary> 클릭을 받는 유니티 버튼입니다. </summary>
    [SerializeField]
    private Button _button;

    /// <summary> 블럭 아이디/좌표를 표시할 라벨입니다. 없으면 무시됩니다. </summary>
    [SerializeField]
    private TMP_Text _label;

    /// <summary> 이 셀의 X 좌표입니다. </summary>
    private int _x;

    /// <summary> 이 셀의 Y 좌표입니다. </summary>
    private int _y;

    /// <summary> 클릭 시 좌표를 전달할 콜백입니다. </summary>
    private Action<int, int> _onClicked;

    /// <summary>
    /// 셀 뷰를 좌표/클릭 콜백과 연결하고 초기 데이터를 그립니다.
    /// </summary>
    /// <param name="cell">표시할 셀 데이터입니다.</param>
    /// <param name="onClicked">클릭 시 (x, y)를 전달할 콜백입니다.</param>
    public void Bind(CellData cell, Action<int, int> onClicked)
    {
        _x = cell.x;
        _y = cell.y;
        _onClicked = onClicked;

        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        Refresh(cell);
    }

    /// <summary>
    /// 셀 데이터 기준으로 틴트와 라벨을 다시 그립니다.
    /// </summary>
    /// <param name="cell">표시할 셀 데이터입니다.</param>
    public void Refresh(CellData cell)
    {
        if (_background != null)
        {
            _background.color = GetCellColor((CellType)cell.cell_type);
        }

        if (_label != null)
        {
            _label.text = string.IsNullOrEmpty(cell.block_id) ? string.Empty : cell.block_id;
        }
    }

    /// <summary>
    /// 버튼 클릭 시 좌표 콜백을 호출합니다.
    /// </summary>
    private void HandleClick()
    {
        _onClicked?.Invoke(_x, _y);
    }

    /// <summary>
    /// 오브젝트 파괴 시 버튼 리스너를 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
        }
    }

    /// <summary>
    /// 셀 타입에 대응하는 배경 색을 반환합니다.
    /// </summary>
    /// <param name="cellType">색을 구할 셀 타입입니다.</param>
    /// <returns>셀 타입 틴트 색입니다.</returns>
    private static Color GetCellColor(CellType cellType)
    {
        switch (cellType)
        {
            case CellType.Close:
                return new Color(0.18f, 0.18f, 0.20f, 1f);
            case CellType.Lock:
                return new Color(0.35f, 0.45f, 0.70f, 1f);
            case CellType.Generator:
                return new Color(0.30f, 0.62f, 0.38f, 1f);
            default:
                return new Color(0.90f, 0.90f, 0.92f, 1f);
        }
    }
}
