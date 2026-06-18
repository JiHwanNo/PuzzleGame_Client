using Puzzle.Core;
using UnityEngine;

/// <summary>
/// 스테이지 맵 툴 UI 입력을 상태 모듈에 연결하는 컨트롤러입니다.
/// </summary>
public class StageMapToolController : MonoBehaviour
{
    /// <summary>
    /// 스테이지 맵 툴의 상위 편집 모드입니다.
    /// </summary>
    private enum StageMapEditMode
    {
        /// <summary> 셀 속성 편집 </summary>
        Cell = 0,

        /// <summary> 블럭 배치 편집 </summary>
        Block = 1,

        /// <summary> 타일/패널 편집 </summary>
        Tile = 2
    }

    #region 퍼즐 타입 선택

    [Header("PUZZLE TYPE COMPONENT")]
    /// <summary> 마지막으로 선택한 퍼즐 타입 저장 키입니다. </summary>
    private const string LastPuzzleTypeKey = "StageMapTool.LastPuzzleType";

    /// <summary> 버튼 인덱스 순서에 대응하는 퍼즐 타입 목록입니다. </summary>
    private static readonly PuzzleType[] PuzzleTypes =
    {
        PuzzleType.ThreeMatch,
        PuzzleType.TapMatch,
        PuzzleType.Link
    };

    /// <summary> 퍼즐 타입별 기본 규칙 주소 목록입니다. </summary>
    private static readonly string[] RuleAddresses =
    {
        "ThreeMatchRule",
        "TapMatchRule",
        "LinkMatchRule"
    };

    /// <summary> 퍼즐 타입 버튼 선택 뷰 그룹입니다. </summary>
    [SerializeField]
    private UIButtonGroup _puzzleTypeButtonGroup;

    /// <summary> 맵툴 현재 작업 상태 모듈입니다. </summary>
    private readonly StageMapToolState _state = new StageMapToolState();

    /// <summary> 현재 선택된 퍼즐 타입입니다. </summary>
    public PuzzleType CurrentPuzzleType
    {
        get
        {
            return _state.PuzzleType;
        }
    }

    /// <summary>
    /// 맵 데이터가 아직 없을 때 마지막 선택한 퍼즐 타입을 복원합니다.
    /// </summary>
    private void Awake()
    {
        if (_boardView != null)
        {
            _boardView.OnCellClicked += HandleCellClicked;
        }

        if (!_state.HasStageData)
        {
            ApplyPuzzleType(LoadLastPuzzleType(), false);
        }

        ApplyEditMode(_editMode);
        ApplyCellType(_state.Brush.cellType);
        LoadStage();
    }

    /// <summary>
    /// 퍼즐 타입 버튼 클릭 시 상태 모듈과 버튼 뷰를 갱신합니다.
    /// UIButton 콜백 값은 0, 1, 2 인덱스를 사용합니다.
    /// </summary>
    /// <param name="val">선택한 퍼즐 타입 버튼 인덱스 문자열입니다.</param>
    public void OnClickPuzzleType(string val)
    {
        if (!int.TryParse(val, out int index) || !IsValidPuzzleIndex(index))
        {
            Debug.LogError($"[StageMapToolController] 지원하지 않는 퍼즐 타입 인덱스입니다. value: {val}");
            return;
        }

        ApplyPuzzleType(PuzzleTypes[index], true);
        LoadStage();
    }

    /// <summary>
    /// 로드한 맵 파일의 퍼즐 타입을 상태와 버튼 뷰에 적용합니다.
    /// 파일 데이터가 우선이므로 마지막 선택 PlayerPrefs는 갱신하지 않습니다.
    /// </summary>
    /// <param name="puzzleType">로드한 맵 파일의 퍼즐 타입입니다.</param>
    public void ApplyLoadedPuzzleType(PuzzleType puzzleType)
    {
        ApplyPuzzleType(puzzleType, false);
    }

    /// <summary>
    /// 퍼즐 타입을 상태 모듈과 버튼 뷰에 적용합니다.
    /// </summary>
    /// <param name="puzzleType">적용할 퍼즐 타입입니다.</param>
    /// <param name="savePreference">마지막 선택값으로 저장할지 여부입니다.</param>
    private void ApplyPuzzleType(PuzzleType puzzleType, bool savePreference)
    {
        int index = GetPuzzleTypeIndex(puzzleType);
        if (!IsValidPuzzleIndex(index))
        {
            puzzleType = PuzzleType.ThreeMatch;
            index = 0;
        }

        _state.SetPuzzleType(puzzleType, RuleAddresses[index]);
        _puzzleTypeButtonGroup?.Select(index);

        if (savePreference)
        {
            PlayerPrefs.SetInt(LastPuzzleTypeKey, (int)puzzleType);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// 마지막으로 선택한 퍼즐 타입을 불러옵니다.
    /// </summary>
    /// <returns>저장된 퍼즐 타입입니다.</returns>
    private PuzzleType LoadLastPuzzleType()
    {
        return (PuzzleType)PlayerPrefs.GetInt(LastPuzzleTypeKey, (int)PuzzleType.ThreeMatch);
    }

    /// <summary>
    /// 퍼즐 타입에 대응하는 버튼 인덱스를 반환합니다.
    /// </summary>
    /// <param name="puzzleType">검색할 퍼즐 타입입니다.</param>
    /// <returns>버튼 인덱스입니다. 없으면 -1입니다.</returns>
    private int GetPuzzleTypeIndex(PuzzleType puzzleType)
    {
        for (int i = 0; i < PuzzleTypes.Length; i++)
        {
            if (PuzzleTypes[i] == puzzleType)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 퍼즐 타입 버튼 인덱스가 유효한지 확인합니다.
    /// </summary>
    /// <param name="index">검사할 버튼 인덱스입니다.</param>
    /// <returns>유효하면 true입니다.</returns>
    private bool IsValidPuzzleIndex(int index)
    {
        return index >= 0 && index < PuzzleTypes.Length;
    }

    #endregion

    #region 편집 모드 선택

    [Header("EDIT BUTTON COMPONENT")]
    /// <summary> 편집 모드 버튼 선택 뷰 그룹입니다. </summary>
    [SerializeField]
    private UIButtonGroup _editModeButtonGroup;

    /// <summary> 셀 편집 하위 버튼 패널입니다. </summary>
    [SerializeField]
    private GameObject _cellEditPanel;

    /// <summary> 블럭 편집 하위 버튼 패널입니다. </summary>
    [SerializeField]
    private GameObject _blockEditPanel;

    /// <summary> 타일 편집 하위 버튼 패널입니다. </summary>
    [SerializeField]
    private GameObject _tileEditPanel;

    /// <summary> 현재 선택된 편집 모드입니다. </summary>
    private StageMapEditMode _editMode = StageMapEditMode.Cell;

    /// <summary>
    /// 편집 모드 버튼 클릭 시 선택 상태와 하위 패널 노출을 갱신합니다.
    /// UIButton 콜백 값은 0, 1, 2 인덱스를 사용합니다.
    /// </summary>
    /// <param name="val">선택한 편집 모드 버튼 인덱스 문자열입니다.</param>
    public void OnClickEditMode(string val)
    {
        if (!int.TryParse(val, out int index) || !IsValidEditModeIndex(index))
        {
            Debug.LogError($"[StageMapToolController] 지원하지 않는 편집 모드 인덱스입니다. value: {val}");
            return;
        }

        ApplyEditMode((StageMapEditMode)index);
    }

    /// <summary>
    /// 편집 모드를 적용하고 버튼 및 하위 패널 상태를 갱신합니다.
    /// </summary>
    /// <param name="editMode">적용할 편집 모드입니다.</param>
    private void ApplyEditMode(StageMapEditMode editMode)
    {
        _editMode = editMode;
        int index = (int)_editMode;

        _editModeButtonGroup?.Select(index);
        SetPanelActive(_cellEditPanel, _editMode == StageMapEditMode.Cell);
        SetPanelActive(_blockEditPanel, _editMode == StageMapEditMode.Block);
        SetPanelActive(_tileEditPanel, _editMode == StageMapEditMode.Tile);
    }

    /// <summary>
    /// 패널 활성 상태를 변경합니다.
    /// </summary>
    /// <param name="panel">활성 상태를 바꿀 패널입니다.</param>
    /// <param name="isActive">활성화 여부입니다.</param>
    private void SetPanelActive(GameObject panel, bool isActive)
    {
        if (panel == null)
        {
            return;
        }

        panel.SetActive(isActive);
    }

    /// <summary>
    /// 편집 모드 버튼 인덱스가 유효한지 확인합니다.
    /// </summary>
    /// <param name="index">검사할 버튼 인덱스입니다.</param>
    /// <returns>유효하면 true입니다.</returns>
    private bool IsValidEditModeIndex(int index)
    {
        return index >= 0 && index <= (int)StageMapEditMode.Tile;
    }

    #endregion

    #region 셀 타입 브러시 선택

    /// <summary> 버튼 인덱스 순서에 대응하는 셀 타입 목록입니다. </summary>
    private static readonly CellType[] BrushCellTypes =
    {
        CellType.Normal,
        CellType.Close,
        CellType.Lock,
        CellType.Generator
    };

    [Header("CELL BRUSH COMPONENT")]
    /// <summary> 셀 타입 브러시 버튼 선택 뷰 그룹입니다. </summary>
    [SerializeField]
    private UIButtonGroup _cellTypeButtonGroup;

    /// <summary>
    /// 셀 타입 브러시 버튼 클릭 시 현재 브러시의 셀 타입을 변경합니다.
    /// UIButton 콜백 값은 BrushCellTypes 배열 인덱스(0~3)를 사용합니다.
    /// </summary>
    /// <param name="val">선택한 셀 타입 버튼 인덱스 문자열입니다.</param>
    public void OnClickCellType(string val)
    {
        if (!int.TryParse(val, out int index) || !IsValidBrushIndex(index))
        {
            Debug.LogError($"[StageMapToolController] 지원하지 않는 셀 타입 인덱스입니다. value: {val}");
            return;
        }

        ApplyCellType(BrushCellTypes[index]);
    }

    /// <summary>
    /// 셀 타입을 현재 브러시와 버튼 뷰에 적용합니다.
    /// </summary>
    /// <param name="cellType">적용할 셀 타입입니다.</param>
    private void ApplyCellType(CellType cellType)
    {
        int index = GetBrushTypeIndex(cellType);
        if (!IsValidBrushIndex(index))
        {
            cellType = CellType.Normal;
            index = 0;
        }

        _state.Brush.cellType = cellType;
        _cellTypeButtonGroup?.Select(index);
    }

    /// <summary>
    /// 셀 타입에 대응하는 브러시 버튼 인덱스를 반환합니다.
    /// </summary>
    /// <param name="cellType">검색할 셀 타입입니다.</param>
    /// <returns>버튼 인덱스입니다. 없으면 -1입니다.</returns>
    private int GetBrushTypeIndex(CellType cellType)
    {
        for (int i = 0; i < BrushCellTypes.Length; i++)
        {
            if (BrushCellTypes[i] == cellType)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 셀 타입 브러시 버튼 인덱스가 유효한지 확인합니다.
    /// </summary>
    /// <param name="index">검사할 버튼 인덱스입니다.</param>
    /// <returns>유효하면 true입니다.</returns>
    private bool IsValidBrushIndex(int index)
    {
        return index >= 0 && index < BrushCellTypes.Length;
    }

    #endregion

    #region 보드 편집

    [Header("BOARD COMPONENT")]
    /// <summary> 스테이지 그리드를 렌더링하는 보드 뷰입니다. </summary>
    [SerializeField]
    private StageMapBoardView _boardView;

    /// <summary> 스테이지 JSON 로드/저장 저장소입니다. </summary>
    private readonly StageMapJsonRepository _repository = new StageMapJsonRepository();

    /// <summary>
    /// 현재 퍼즐 타입/스테이지 번호로 스테이지를 로드하고 보드 뷰를 다시 그립니다.
    /// </summary>
    private void LoadStage()
    {
        if (_boardView == null)
        {
            return;
        }

        StageData stageData = _repository.LoadOrCreate(_state.PuzzleType, _state.StageId);
        _state.SetStage(stageData);
        _boardView.Build(stageData);
    }

    /// <summary>
    /// 셀 클릭 시 현재 브러시를 적용하고 해당 셀만 갱신합니다.
    /// </summary>
    /// <param name="x">클릭한 X 좌표입니다.</param>
    /// <param name="y">클릭한 Y 좌표입니다.</param>
    private void HandleCellClicked(int x, int y)
    {
        if (_state.PaintCell(x, y))
        {
            _boardView.RefreshCell(x, y);
        }
    }

    /// <summary>
    /// 오브젝트 파괴 시 보드 뷰 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (_boardView != null)
        {
            _boardView.OnCellClicked -= HandleCellClicked;
        }
    }

    #endregion
}
