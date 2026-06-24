using System;
using Puzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 맵 툴 그리드의 단일 셀 뷰입니다. 빈 칸("+")·생성된 셀·선택 상태를 표시하고 클릭을 전달합니다.
/// </summary>
public class StageMapCellView : MonoBehaviour
{
    /// <summary> 빈 칸("+")에 사용할 배경 색입니다. </summary>
    private static readonly Color EmptyColor = new Color(0.22f, 0.23f, 0.27f, 1f);

    /// <summary> 빈 칸("+") 라벨 색입니다(밝게 강조). </summary>
    private static readonly Color EmptyLabelColor = new Color(0.78f, 0.82f, 0.90f, 1f);

    /// <summary> 생성된 셀의 라벨 색입니다(어둡게). </summary>
    private static readonly Color FilledLabelColor = new Color(0.12f, 0.12f, 0.14f, 1f);

    /// <summary> 셀 배경 틴트를 표시할 이미지입니다. </summary>
    [SerializeField]
    private Image _background;

    /// <summary> 클릭을 받는 유니티 버튼입니다. </summary>
    [SerializeField]
    private Button _button;

    /// <summary> 블럭 아이디/빈 칸("+")을 표시할 라벨입니다. 없으면 무시됩니다. </summary>
    [SerializeField]
    private TMP_Text _label;

    /// <summary> 선택 상태일 때 표시할 하이라이트 오브젝트입니다. 없으면 무시됩니다. </summary>
    [SerializeField]
    private GameObject _selectionOutline;

    /// <summary> 셀에 놓인 블럭의 스프라이트를 표시할 이미지입니다. 없으면 무시됩니다. </summary>
    [SerializeField]
    private Image _blockIcon;

    /// <summary> 이 셀의 X 좌표입니다. </summary>
    private int _x;

    /// <summary> 이 셀의 Y 좌표입니다. </summary>
    private int _y;

    /// <summary> 클릭 시 좌표를 전달할 콜백입니다. </summary>
    private Action<int, int> _onClicked;

    /// <summary>
    /// 셀 뷰를 좌표/클릭 콜백과 연결하고 초기 상태를 그립니다.
    /// </summary>
    /// <param name="x">셀의 X 좌표입니다.</param>
    /// <param name="y">셀의 Y 좌표입니다.</param>
    /// <param name="cell">표시할 셀 데이터입니다. 빈 칸이면 null입니다.</param>
    /// <param name="onClicked">클릭 시 (x, y)를 전달할 콜백입니다.</param>
    public void Bind(int x, int y, CellData cell, Action<int, int> onClicked)
    {
        _x = x;
        _y = y;
        _onClicked = onClicked;

        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClick);
            _button.onClick.AddListener(HandleClick);
        }

        Refresh(cell, false);
    }

    /// <summary>
    /// 셀 데이터와 선택 상태 기준으로 배경·라벨·하이라이트를 다시 그립니다.
    /// 데이터가 없거나 Close(막힌 구역)이면 빈 칸("+")으로 표시합니다.
    /// </summary>
    /// <param name="cell">표시할 셀 데이터입니다. 빈 칸이면 null입니다.</param>
    /// <param name="selected">선택 상태 여부입니다.</param>
    public void Refresh(CellData cell, bool selected)
    {
        bool isEmpty = cell == null || cell.cell_type == (int)CellType.Close;
        string blockId = isEmpty ? null : cell.block_id;
        bool hasBlock = !string.IsNullOrEmpty(blockId);

        if (_background != null)
        {
            _background.color = isEmpty ? EmptyColor : GetCellColor((CellType)cell.cell_type);
        }

        bool iconShown = UpdateBlockIcon(hasBlock ? blockId : null);

        if (_label != null)
        {
            if (isEmpty)
            {
                _label.text = "+";
                _label.color = EmptyLabelColor;
            }
            else
            {
                // 블럭 스프라이트를 띄우면 라벨은 비우고, 로드 실패/블럭 없음이면 ID(또는 빈 칸)를 표시한다.
                _label.text = iconShown ? string.Empty : (hasBlock ? blockId : string.Empty);
                _label.color = FilledLabelColor;
            }
        }

        if (_selectionOutline != null)
        {
            _selectionOutline.SetActive(selected);
        }
    }

    /// <summary>
    /// 블럭 아이디로 스프라이트를 로드해 아이콘에 표시합니다. 블럭이 없거나 로드 실패면 숨깁니다.
    /// 프로젝트 규약대로 AssetManager 경유 Addressable 로드를 사용합니다(주소: "Block_{blockId}").
    /// </summary>
    /// <param name="blockId">표시할 블럭 아이디입니다. 없으면 null입니다.</param>
    /// <returns>스프라이트를 실제로 표시했으면 true입니다.</returns>
    private bool UpdateBlockIcon(string blockId)
    {
        if (_blockIcon == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(blockId))
        {
            _blockIcon.sprite = null;
            _blockIcon.enabled = false;
            return false;
        }

        Sprite sprite = AssetManager.Instance.LoadAsset<Sprite>("Block_" + blockId);
        if (sprite == null)
        {
            _blockIcon.sprite = null;
            _blockIcon.enabled = false;
            return false;
        }

        _blockIcon.sprite = sprite;
        _blockIcon.enabled = true;
        return true;
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
