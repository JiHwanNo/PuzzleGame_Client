using System.Collections.Generic;
using Puzzle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        BuildBlockPalette();
        BuildNewStage();
        UpdateStageIdLabel();
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
        BuildBlockPalette();
        BuildNewStage();
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

    /// <summary> 현재 선택된 셀이 있는지 여부입니다. </summary>
    private bool _hasSelection;

    /// <summary> 현재 선택된 셀의 X 좌표입니다. </summary>
    private int _selectedX;

    /// <summary> 현재 선택된 셀의 Y 좌표입니다. </summary>
    private int _selectedY;

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
        _editModeButtonGroup?.Select((int)_editMode);
        RefreshInspector();
    }

    /// <summary>
    /// 현재 선택 상태와 편집 모드에 따라 인스펙터 패널 노출을 갱신합니다.
    /// 선택된 셀이 없으면 모든 편집 패널을 숨깁니다.
    /// </summary>
    private void RefreshInspector()
    {
        SetPanelActive(_cellEditPanel, _hasSelection && _editMode == StageMapEditMode.Cell);
        SetPanelActive(_blockEditPanel, _hasSelection && _editMode == StageMapEditMode.Block);
        SetPanelActive(_tileEditPanel, _hasSelection && _editMode == StageMapEditMode.Tile);
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

    /// <summary> 셀 상태 버튼 인덱스 순서에 대응하는 셀 타입 목록입니다(Close=빈칸은 삭제 버튼이 담당). </summary>
    private static readonly CellType[] BrushCellTypes =
    {
        CellType.Normal,
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

        if (!_hasSelection)
        {
            return;
        }

        // 선택된 셀의 타입을 변경하고 그 칸만 갱신한다.
        if (_state.SetCellType(_selectedX, _selectedY, BrushCellTypes[index]))
        {
            _boardView.RefreshCell(_selectedX, _selectedY);
            _cellTypeButtonGroup?.Select(index);
        }
    }

    /// <summary>
    /// 셀 타입 버튼 그룹의 선택 표시를 지정한 셀 타입에 맞춥니다.
    /// </summary>
    /// <param name="cellType">표시할 셀 타입입니다.</param>
    private void SyncCellTypeButtons(CellType cellType)
    {
        int index = GetBrushTypeIndex(cellType);
        if (index < 0)
        {
            index = 0;
        }

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

    #region 블럭 팔레트

    /// <summary> 행 높이를 구할 수 없을 때 사용할 블럭 팔레트 버튼 기본 높이(px)입니다. </summary>
    private const float BlockButtonFallbackHeight = 64f;

    [Header("BLOCK PALETTE COMPONENT")]
    /// <summary> 블럭 팔레트 버튼을 생성할 때 복제할 버튼 프리팹입니다. </summary>
    [SerializeField]
    private UIButton _blockButtonPrefab;

    /// <summary> 블럭 팔레트 버튼이 가로로 나열되는 스크롤 콘텐츠입니다(넘치면 가로 스크롤). </summary>
    [SerializeField]
    private RectTransform _blockPaletteContent;

    /// <summary> 현재 규칙의 블럭 목록을 로드하는 제공자입니다. </summary>
    private readonly StageMapRuleProvider _ruleProvider = new StageMapRuleProvider();

    /// <summary>
    /// 현재 퍼즐 타입의 규칙 블럭으로 블럭 편집 팔레트를 다시 생성합니다.
    /// </summary>
    private void BuildBlockPalette()
    {
        if (_blockPaletteContent == null || _blockButtonPrefab == null)
        {
            return;
        }

        Transform root = _blockPaletteContent;

        // 기존 팔레트 버튼을 모두 제거한다.
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }

        List<BlockData> blocks = _ruleProvider.LoadBlocks(_state.RuleAddress);
        for (int i = 0; i < blocks.Count; i++)
        {
            BlockData block = blocks[i];
            if (block == null || string.IsNullOrEmpty(block.blockId))
            {
                continue;
            }

            UIButton button = Instantiate(_blockButtonPrefab, root);
            button.SetCallback(this, "OnClickBlock", block.blockId);

            // 블럭 스프라이트를 버튼 아이콘으로 표시(셀뷰와 동일하게 "Block_{id}" 주소).
            Sprite sprite = AssetManager.Instance.LoadAsset<Sprite>("Block_" + block.blockId);
            if (sprite != null)
            {
                button.SetIconSprite(sprite);
            }

            // 버튼 규격은 스프라이트 크기(종횡비)를 따른다. 행 높이에 맞추고 폭은 스프라이트 비율로 결정.
            float rowHeight = _blockPaletteContent.rect.height;
            if (rowHeight <= 0f)
            {
                rowHeight = BlockButtonFallbackHeight;
            }

            float buttonWidth = rowHeight;
            if (sprite != null && sprite.rect.height > 0f)
            {
                buttonWidth = rowHeight * (sprite.rect.width / sprite.rect.height);
            }

            RectTransform buttonRect = button.transform as RectTransform;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(buttonWidth, rowHeight);
            }

            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = button.gameObject.AddComponent<LayoutElement>();
            }
            layout.preferredWidth = buttonWidth;
            layout.preferredHeight = rowHeight;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = string.Empty;
            }
        }
    }

    /// <summary>
    /// 블럭 팔레트 버튼 클릭 시 선택된 셀에 해당 블럭을 배치합니다.
    /// 콜백 값으로 블럭 아이디 문자열을 직접 받습니다.
    /// </summary>
    /// <param name="blockId">배치할 블럭 아이디입니다.</param>
    public void OnClickBlock(string blockId)
    {
        if (!_hasSelection)
        {
            return;
        }

        if (_state.SetBlockId(_selectedX, _selectedY, blockId))
        {
            _boardView.RefreshCell(_selectedX, _selectedY);
        }
    }

    #endregion

    #region 보드 편집

    [Header("BOARD COMPONENT")]
    /// <summary> 스테이지 그리드를 렌더링하는 보드 뷰입니다. </summary>
    [SerializeField]
    private StageMapBoardView _boardView;

    /// <summary> 스테이지 JSON 로드/저장 저장소입니다. </summary>
    private readonly StageMapJsonRepository _repository = new StageMapJsonRepository();

    /// <summary> 저장 직전 내용 기준 정규화(trim)를 수행하는 정규화기입니다. </summary>
    private readonly StageMapNormalizer _normalizer = new StageMapNormalizer();

    /// <summary> 저장 직전 데이터 정합성을 검증하는 검증기입니다. </summary>
    private readonly StageMapValidator _validator = new StageMapValidator();

    /// <summary>
    /// 새 빈 스테이지(전부 "+")를 만들고 보드 뷰를 다시 그립니다.
    /// 맵툴은 시작/퍼즐타입 변경 시 항상 빈 맵으로 시작한다(기존 스테이지 편집은 별도 불러오기에서 처리 — Phase 3).
    /// </summary>
    private void BuildNewStage()
    {
        if (_boardView == null)
        {
            return;
        }

        StageData stageData = _repository.CreateEmptyStage(_state.StageId);
        _state.SetStage(stageData);
        _hasSelection = false;
        _boardView.Build(stageData, IsHexLayout());
        RefreshInspector();
    }

    /// <summary>
    /// 현재 퍼즐 타입이 헥사 배치(Link 모드)인지 반환합니다.
    /// </summary>
    /// <returns>헥사 배치면 true입니다.</returns>
    private bool IsHexLayout()
    {
        return _state.PuzzleType == PuzzleType.Link;
    }

    /// <summary>
    /// 셀 클릭을 처리합니다. 빈 칸("+")이면 셀을 생성하고, 그 셀을 선택 상태로 만듭니다.
    /// </summary>
    /// <param name="x">클릭한 X 좌표입니다.</param>
    /// <param name="y">클릭한 Y 좌표입니다.</param>
    private void HandleCellClicked(int x, int y)
    {
        CellData cell = _state.GetCell(x, y);
        if (cell == null || cell.cell_type == (int)CellType.Close)
        {
            // 빈 칸("+") 클릭 → 일반 셀 생성
            cell = _state.CreateCell(x, y);
            _boardView.RefreshCell(x, y);
        }

        // 셀 선택(하이라이트) + 인스펙터 노출
        _selectedX = x;
        _selectedY = y;
        _hasSelection = true;
        _boardView.Select(x, y);

        if (cell != null)
        {
            SyncCellTypeButtons((CellType)cell.cell_type);
        }

        RefreshInspector();
    }

    /// <summary>
    /// 삭제 버튼: 선택된 셀을 제거해 빈 칸("+")으로 되돌리고 선택을 해제합니다.
    /// </summary>
    public void OnClickDeleteCell()
    {
        if (!_hasSelection)
        {
            return;
        }

        _state.RemoveCell(_selectedX, _selectedY);
        _boardView.RefreshCell(_selectedX, _selectedY);
        _boardView.ClearSelection();
        _hasSelection = false;
        RefreshInspector();
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

    #region 스테이지 파일 (저장/로드/검증)

    [Header("STAGE FILE COMPONENT")]
    /// <summary> 현재 편집 중인 스테이지 파일명을 표시할 라벨입니다. 없으면 무시됩니다. </summary>
    [SerializeField]
    private TMP_Text _stageIdLabel;

    /// <summary> 저장/로드/검증 결과 메시지를 표시할 상태 라벨입니다. 없으면 무시됩니다. </summary>
    [SerializeField]
    private TMP_Text _statusText;

    /// <summary>
    /// 저장 버튼: 현재 편집 데이터를 내용 기준으로 정규화하고 검증한 뒤 Resources에 저장합니다.
    /// </summary>
    public void OnClickSave()
    {
        if (!TryNormalizeAndValidate(out StageData normalized))
        {
            return;
        }

        bool saved = _repository.SaveToResources(_state.PuzzleType, _state.StageId, normalized);
        if (saved)
        {
            SetStatus($"저장 완료: {StageStorage.GetStageFileName(_state.StageId)} ({normalized.stage_width}×{normalized.stage_height})", false);
        }
        else
        {
            SetStatus("저장 실패 (콘솔 확인)", true);
        }
    }

    /// <summary>
    /// 불러오기 버튼: 현재 퍼즐 타입/스테이지 번호의 저장된 스테이지를 읽어 편집 보드에 올립니다.
    /// 정규화된 파일의 Close 셀은 편집 모델에서 빈 칸("+")으로 표시되어 이어서 편집할 수 있습니다.
    /// </summary>
    public void OnClickLoad()
    {
        if (_boardView == null)
        {
            return;
        }

        StageData loaded = _repository.LoadOrCreate(_state.PuzzleType, _state.StageId);
        _state.SetStage(loaded);
        // 불러온 파일의 stage_id가 현재 선택한 슬롯 번호와 다를 수 있으므로 슬롯 기준으로 맞춘다(저장 시 검증 일치).
        _state.SetStageId(_state.StageId);
        _hasSelection = false;
        _boardView.ClearSelection();
        _boardView.Build(loaded, IsHexLayout());
        RefreshInspector();
        SetStatus($"불러오기 완료: {StageStorage.GetStageFileName(_state.StageId)}", false);
    }

    /// <summary>
    /// 맵 시작해보기 버튼: 현재 편집 중인 맵을 저장 없이 즉시 게임으로 실행합니다.
    /// 저장과 동일하게 정규화·검증을 거친 뒤, 통과하면 메모리 주입으로 GameSpec을 만들고 GameScene으로 이동합니다.
    /// </summary>
    public void OnClickTestPlay()
    {
        if (!TryNormalizeAndValidate(out StageData normalized))
        {
            return;
        }

        // 저장하지 않은 편집 맵이라도, 리플레이가 이 맵을 다시 찾을 수 있도록 슬롯 주소를 함께 주입한다.
        string stageAddress = StageStorage.GetResourceKey(_state.PuzzleType, _state.StageId);
        if (!StageInjection.Instance.MakeGameSpecFromData(_state.RuleAddress, stageAddress, normalized))
        {
            SetStatus("게임 데이터 준비 실패 (콘솔 확인)", true);
            return;
        }

        // 직전 리플레이가 남아 있으면 테스트 플레이가 상대 리플레이로 구동되므로 반드시 비운다.
        StageInjection.Instance.SetReplayData(null);

        if (Main.Instance == null)
        {
            SetStatus("Main을 찾을 수 없어 씬을 이동할 수 없습니다.", true);
            return;
        }

        SetStatus("맵 시작...", false);
        Main.Instance.MoveScene(SceneEnum.ToolScene, SceneEnum.GameScene);
    }

    /// <summary>
    /// 저장/시작 공통 전처리: 현재 편집 데이터를 정규화하고 규칙 기준으로 검증합니다.
    /// 실패 시 상태 라벨에 사유를 출력하고 false를 반환합니다.
    /// </summary>
    /// <param name="normalized">검증을 통과한 정규화 스테이지 데이터입니다. 실패 시 null입니다.</param>
    /// <returns>정규화·검증을 모두 통과하면 true입니다.</returns>
    private bool TryNormalizeAndValidate(out StageData normalized)
    {
        normalized = null;

        if (_state.StageData == null)
        {
            SetStatus("스테이지가 없습니다.", true);
            return false;
        }

        if (!_normalizer.TryNormalize(_state.StageData, IsHexLayout(), out normalized))
        {
            SetStatus("셀이 없습니다. 셀을 먼저 배치하세요.", true);
            return false;
        }

        List<BlockData> ruleBlocks = _ruleProvider.LoadBlocks(_state.RuleAddress);
        StageMapValidationResult validation = _validator.Validate(normalized, _state.StageId, ruleBlocks);
        if (!validation.IsValid())
        {
            LogValidation(validation);
            SetStatus($"검증 실패: 오류 {validation.errors.Count}건 (콘솔 확인)", true);
            normalized = null;
            return false;
        }

        if (validation.warnings.Count > 0)
        {
            LogValidation(validation);
        }

        return true;
    }

    /// <summary>
    /// 스테이지 번호 감소 버튼입니다.
    /// </summary>
    public void OnClickStageIdPrev()
    {
        SetStageId(_state.StageId - 1);
    }

    /// <summary>
    /// 스테이지 번호 증가 버튼입니다.
    /// </summary>
    public void OnClickStageIdNext()
    {
        SetStageId(_state.StageId + 1);
    }

    /// <summary>
    /// 스테이지 번호를 허용 범위로 보정해 적용하고 라벨을 갱신합니다.
    /// </summary>
    /// <param name="stageId">적용할 스테이지 번호입니다.</param>
    private void SetStageId(int stageId)
    {
        int clamped = Mathf.Clamp(stageId, StageStorage.MinStageId, StageStorage.MaxStageId);
        _state.SetStageId(clamped);
        UpdateStageIdLabel();
    }

    /// <summary>
    /// 현재 스테이지 번호를 라벨에 표시합니다.
    /// </summary>
    private void UpdateStageIdLabel()
    {
        if (_stageIdLabel != null)
        {
            _stageIdLabel.text = StageStorage.GetStageFileName(_state.StageId);
        }
    }

    /// <summary>
    /// 상태 라벨과 콘솔에 결과 메시지를 출력합니다.
    /// </summary>
    /// <param name="message">표시할 메시지입니다.</param>
    /// <param name="isError">오류 메시지 여부입니다.</param>
    private void SetStatus(string message, bool isError)
    {
        if (isError)
        {
            Debug.LogError($"[StageMapToolController] {message}");
        }
        else
        {
            Debug.Log($"[StageMapToolController] {message}");
        }

        if (_statusText != null)
        {
            _statusText.text = message;
        }
    }

    /// <summary>
    /// 검증 결과의 오류/경고를 콘솔에 출력합니다.
    /// </summary>
    /// <param name="result">출력할 검증 결과입니다.</param>
    private void LogValidation(StageMapValidationResult result)
    {
        for (int i = 0; i < result.errors.Count; i++)
        {
            Debug.LogError($"[StageMapTool 검증] {result.errors[i]}");
        }

        for (int i = 0; i < result.warnings.Count; i++)
        {
            Debug.LogWarning($"[StageMapTool 검증] {result.warnings[i]}");
        }
    }

    #endregion
}
