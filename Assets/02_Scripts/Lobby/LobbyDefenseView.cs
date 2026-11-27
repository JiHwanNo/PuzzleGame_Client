using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>로비 전투와 경로를 표시하고 보유 카드 배치와 스테이지 진행 입력을 전달한다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed partial class LobbyDefenseView : MaskableGraphic, IPointerClickHandler
{
    private const int FieldWidth = 744;
    private const int FieldHeight = FieldWidth * LobbyDefenseBoard.Height / LobbyDefenseBoard.Width;
    private const int WestDirection = 6;
    private static LobbyDefenseBoard _session;
    private TMP_FontAsset _font;
    private Texture2D _archerSheet;
    private readonly Dictionary<LobbyUnit, UnitArt> _artByUnit = new Dictionary<LobbyUnit, UnitArt>();
    private readonly List<UnitArt> _loadedArt = new List<UnitArt>();
    private readonly List<FieldActor> _walkingGhosts = new List<FieldActor>();
    private static readonly string[] _directions = { "S", "SE", "E", "NE", "N", "NW", "W", "SW" };
    private readonly List<RawImage> _unitIcons = new List<RawImage>();
    private readonly List<int> _visibleCells = new List<int>();
    private static readonly Color CardBlue = new Color32(45, 76, 98, 255);
    private static readonly Color SelectedBlue = new Color32(32, 111, 145, 255);
    private static readonly Color Honey = new Color32(255, 213, 117, 255);
    private const string SaveKey = "Lobby.Defense.Cards.v1";
    internal static bool HasSavedProgress => PlayerPrefs.HasKey(SaveKey);
    private Button _stageButton;
    private TMP_Text _stageAction;
    private GameObject _play;
    private GameObject _playHint;
    private GameObject _placeIcon;
    private GameObject _placementBar;
    private bool _placing;
    private LobbyCard _focused;
    private LobbyCardPortrait _hero;
    private LobbyCardPortrait _previewActor;
    private RawImage _forest;
    private Texture2D _terrain;
    public override Texture mainTexture => _terrain != null ? _terrain : base.mainTexture;
    private TMP_Text _selectionText;
    private LobbyCard _selectedCard;
    internal event System.Action InventoryRequested;
    private RectTransform _backdrop;
    private RectTransform _actorsRoot;
    private TMP_Text _gold;
    private TMP_Text _waveTime;
    private TMP_Text _summary;
    private TMP_Text _hint;
    private Button _confirmPlacement;
    private LobbyDefenseBoard.Piece SelectedRole
    {
        get
        {
            if (_selectedCard == null)
            {
                return LobbyDefenseBoard.Piece.Empty;
            }
            return _selectedCard.Unit.Role;
        }
    }
    private int _selected = -1;
    private int _moving = -1;
    private int _previewLength = -1;
    private float _elapsed;
    private RectTransform _canvasRect;
    private Vector2 _canvasSize;
    private Canvas _world;
    private Transform _mainTab;
    internal LobbyDefenseBoard Board => _session;

    /// <summary>같은 프리팹으로 만든 전장 배우와 카드에 표시할 원본 초상을 보관한다.</summary>
    private sealed class UnitArt
    {
        public GameObject Prefab;
        public RawImage Portrait;
        public readonly List<FieldActor> Actors = new List<FieldActor>();
        public int Used;
    }

    /// <summary>방향별 상태를 재생하는 배우와 유령이 한 칸을 이동할 때의 표시 좌표를 보관한다.</summary>
    private sealed class FieldActor
    {
        public RectTransform Rect;
        public Animator Animator;
        public CanvasGroup Group;
        public int Cell = -1;
        public int State;
        public int ActionSecond = -1;
        public LobbyDefenseBoard.Monster Monster;
        public Vector2 Start;
        public Vector2 End;
    }

    /// <summary>새 플레이 세션의 로비 전투 상태를 초기화한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        _session = null;
    }

    /// <summary><paramref name="stages"/> 전장을 <paramref name="camera"/> 앞에 만들고 <paramref name="status"/>의 글꼴과 화면 크기를 사용한다.</summary>
    internal static LobbyDefenseView Create(TMP_Text status, Camera camera, LobbyDefenseBoard.StagePlan[] stages)
    {
        if (_session == null)
        {
            if (!LobbyDefenseBoard.ValidStages(stages))
            {
                Debug.LogWarning("[로비] 스테이지 편성이 비어 있거나 소환 간격이 잘못되어 기본 편성을 사용합니다.");
                stages = LobbyDefenseBoard.DefaultStages();
            }
            LobbyDefenseBoard.Progress saved = null;
            if (PlayerPrefs.HasKey(SaveKey))
            {
                string json = PlayerPrefs.GetString(SaveKey);
                try
                {
                    saved = JsonUtility.FromJson<LobbyDefenseBoard.Progress>(json);
                }
                catch (System.ArgumentException exception)
                {
                    Debug.LogWarning($"[로비] 방어전 저장 데이터를 읽지 못했습니다. {exception.Message}");
                }
                if (saved == null || !saved.TryRestoreLayout())
                {
                    PlayerPrefs.SetString(SaveKey + ".Recovery", json);
                    Debug.LogWarning("[로비] 복구할 수 없는 저장본을 별도로 보관하고 새 방어전을 시작합니다.");
                    saved = null;
                }
            }
            _session = new LobbyDefenseBoard(stages, System.Guid.NewGuid().GetHashCode(), saved);
            SaveProgress();
        }
        GameObject field = new GameObject("DefenseWorld", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(field, status.gameObject.scene);
        Canvas world = field.GetComponent<Canvas>();
        world.renderMode = RenderMode.WorldSpace;
        world.worldCamera = camera;
        world.sortingLayerName = "Ingame";
        field.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 10f, camera.transform.rotation);
        RectTransform board = Element("DefenseBoard", field.transform, 0, 160, FieldWidth, FieldHeight);
        LobbyDefenseView view = board.gameObject.AddComponent<LobbyDefenseView>();
        view._world = world;
        view._mainTab = status.transform.parent;
        view._font = status.font;
        view._playHint = status.gameObject;
        view._play = view._mainTab.Find("PlayButton").gameObject;
        view._terrain = AssetManager.Instance.LoadAsset<Texture2D>("LobbyTerrain");
        view._archerSheet = AssetManager.Instance.LoadAsset<Texture2D>("LobbyArcher8Way");
        view._canvasRect = (RectTransform)status.canvas.rootCanvas.transform;
        view._backdrop = Element("LobbyBackground", field.transform, 0, 0, 1, 1);
        view._backdrop.SetAsFirstSibling();
        view._forest = view._backdrop.gameObject.AddComponent<RawImage>();
        view._placementForest = AssetManager.Instance.LoadAsset<Texture2D>("LobbyForest");
        view._meadow = AssetManager.Instance.LoadAsset<Texture2D>("LobbyBattleMeadow");
        view._raiders = AssetManager.Instance.LoadAsset<Texture2D>("LobbyRaiders");
        view._forest.texture = view._meadow;
        view._forest.raycastTarget = false;
        Image rim = Panel("FieldFrame", field.transform, 0, 160, FieldWidth + 8, FieldHeight + 8, new Color32(57, 80, 49, 255));
        view._rim = rim;
        rim.transform.SetSiblingIndex(board.GetSiblingIndex());
        view.FitTab();
        view._actorsRoot = Element("FieldActors", board, 0, 0, FieldWidth, FieldHeight);
        for (int row = 0; row < view._rows.Length; row++)
        {
            view._rows[row] = Element("ActorRow" + row, view._actorsRoot, 0, 0, 0, 0);
        }
        view._effects = Element("CombatEffects", view._actorsRoot, 0, 0, 0, 0);
        float cellSize = FieldWidth / (float)LobbyDefenseBoard.Width;
        RectTransform preview = Element("PlacementPreview", view._actorsRoot, 0, 0, cellSize, cellSize);
        CanvasGroup ghost = preview.gameObject.AddComponent<CanvasGroup>();
        ghost.blocksRaycasts = false;
        ghost.alpha = 0.70f;
        view._previewActor = preview.gameObject.AddComponent<LobbyCardPortrait>();
        view.BuildControls();
        view.Refresh();
        return view;
    }

    /// <summary>보유 카드, 배치, 현재 스테이지의 적과 진행 결과를 한 묶음으로 저장한다.</summary>
    private static void SaveProgress()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_session.Save));
        PlayerPrefs.Save();
    }

    /// <summary><paramref name="paused"/>로 앱이 백그라운드에 들어갈 때 진행 중인 전투를 저장한다.</summary>
    private void OnApplicationPause(bool paused)
    {
        if (paused && _session != null)
        {
            SaveProgress();
        }
    }

    /// <summary>로비 씬을 떠나거나 앱이 종료될 때까지 진행한 상태를 저장한다.</summary>
    protected override void OnDestroy()
    {
        if (_session != null)
        {
            SaveProgress();
        }
        base.OnDestroy();
    }

    /// <summary>상점에서 카드 한 장을 뽑아 골드와 획득 결과를 함께 저장한다.</summary>
    internal LobbyCard DrawCard()
    {
        LobbyCard drawn = _session.Draw();
        if (drawn != null)
        {
            SaveProgress();
        }
        return drawn;
    }

    /// <summary><paramref name="visible"/>에 맞게 전장의 그리기와 입력을 바꾸고 전투 시간은 계속 진행한다.</summary>
    internal void ShowField(bool visible)
    {
        if (_world.enabled != visible)
        {
            _world.enabled = visible;
            _actorsRoot.gameObject.SetActive(visible);
            _world.GetComponent<GraphicRaycaster>().enabled = visible;
            if (visible)
            {
                Refresh();
            }
        }
    }

    /// <summary>세로 공간이 짧은 창에서도 전장과 기존 모드 버튼이 함께 들어오도록 탭을 축소한다.</summary>
    private void FitTab()
    {
        Vector2 size = _canvasRect.rect.size;
        if (size == _canvasSize || size.x <= 0 || size.y <= 0)
        {
            return;
        }
        _canvasSize = size;
        float scale = Mathf.Min(1f, Mathf.Min(size.x / 780f, size.y / 1400f));
        _backdrop.sizeDelta = size / scale;
        if (_forest.texture != null)
        {
            float ratio = size.x * _forest.texture.height / (size.y * _forest.texture.width);
            float u = Mathf.Min(1f, ratio);
            float v = Mathf.Min(1f, 1f / ratio);
            _forest.uvRect = new Rect((1f - u) / 2f, (1f - v) / 2f, u, v);
        }
        _mainTab.localScale = new Vector3(scale, scale, 1f);
        float worldScale = 2f * _world.worldCamera.orthographicSize / size.y * scale;
        _world.transform.localScale = Vector3.one * worldScale;
    }

    /// <summary>이름 <paramref name="name"/>의 UI를 <paramref name="parent"/> 아래 중심 좌표와 크기에 배치한다.</summary>
    private static RectTransform Element(string name, Transform parent, float x, float y, float width, float height)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    /// <summary><paramref name="parent"/> 아래 지정한 중심과 크기로 입력을 통과시키는 <paramref name="color"/> 바탕을 만든다.</summary>
    private static Image Panel(string name, Transform parent, float x, float y, float width, float height, Color color)
    {
        Image image = Element(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>전장 위에 스테이지 정보를 두고 아래 숲 공터에 대표 캐릭터를 표시한다.</summary>
    private void BuildControls()
    {
        Color canopy = new Color32(19, 53, 53, 230);
        RectTransform header = Panel("StageHeader", transform, 0, 250, FieldWidth + 8, 110, canopy).rectTransform;
        _stageHeader = header;
        _summary = Label(Element("Stage", header, -229, 13, 240, 52), 37);
        _summary.alignment = TextAlignmentOptions.MidlineLeft;
        _stageButton = MakeButton(header, "다음 스테이지", 0, 0, 206, 66, StartStage);
        _stageAction = _stageButton.GetComponentInChildren<TMP_Text>();
        _gold = Label(Element("Gold", header, 234, 9, 226, 52), 32);
        _gold.alignment = TextAlignmentOptions.MidlineRight;
        _gold.color = Honey;
        _waveTime = Label(Element("WaveTimer", header, -230, -28, 238, 35), 21);
        _waveTime.alignment = TextAlignmentOptions.MidlineLeft;
        _hero = Element("FeaturedUnit", transform, 0, -322, 264, 264).gameObject.AddComponent<LobbyCardPortrait>();
        RectTransform plate = Panel("FeaturedCaption", transform, 0, -459, 388, 54, canopy).rectTransform;
        _selectionText = Label(Element("FeaturedName", plate, 0, 0, 382, 52), 26);
        BuildPlacementBar();
    }

    /// <summary>플레이 옆 배치 아이콘과 같은 자리에 전환해 표시할 카드 선택·확정·취소 UI를 구성한다.</summary>
    private void BuildPlacementBar()
    {
        RectTransform play = (RectTransform)_play.transform;
        Vector2 position = play.anchoredPosition;
        RectTransform icon = Element("PlacementIcon", _mainTab, position.x + 228, position.y, 88, 88);
        RawImage card = icon.gameObject.AddComponent<RawImage>();
        card.texture = AssetManager.Instance.LoadAsset<Texture2D>("LobbyPlaceIcon");
        Button toggle = icon.gameObject.AddComponent<Button>();
        toggle.targetGraphic = card;
        toggle.onClick.AddListener(BeginPlacement);
        _placeIcon = icon.gameObject;

        RectTransform bar = Element("PlacementMode", _mainTab, position.x, position.y, 730, 90);
        _placementBar = bar.gameObject;
        MakeButton(bar, "카드 선택", -225, 0, 182, 74, OpenInventory);
        _confirmPlacement = MakeButton(bar, "배치 확정", 0, 0, 228, 84, Confirm);
        _confirmPlacement.image.color = SelectedBlue;
        MakeButton(bar, "취소", 206, 0, 138, 74, CancelPlacement);
        RectTransform strip = Panel("PlacementGuide", bar, 0, -84, 640, 56, new Color32(16, 48, 44, 228)).rectTransform;
        _hint = Label(Element("PlacementHint", strip, 0, 0, 632, 56), 23);
    }

    /// <summary>첫 미배치 카드를 후보로 열고 플레이 버튼 자리를 배치 도구로 전환한다.</summary>
    private void BeginPlacement()
    {
        foreach (LobbyCard card in _session.Cards)
        {
            if (card.Cell < 0)
            {
                SelectCard(card);
                return;
            }
        }
        SelectCard(null);
    }

    /// <summary>하단 인벤토리를 열어 보유 카드를 고르게 한다.</summary>
    private void OpenInventory()
    {
        if (InventoryRequested != null)
        {
            InventoryRequested();
        }
    }

    /// <summary>방어 결과에 따라 다음 스테이지 또는 같은 스테이지의 새 전투를 시작한다.</summary>
    private void StartStage()
    {
        if (_session.StartNextAttempt())
        {
            SaveProgress();
            CancelPlacement();
        }
    }

    /// <summary>확정 전 카드와 셀 선택을 취소한다.</summary>
    private void CancelPlacement()
    {
        _placing = false;
        _selectedCard = null;
        _selected = -1;
        _moving = -1;
        Refresh();
    }

    /// <summary>메인과 상점의 <paramref name="icon"/>에 <paramref name="unit"/>의 남동쪽 원화를 표시한다.</summary>
    internal void SetPortrait(RawImage icon, LobbyUnit unit)
    {
        icon.texture = null;
        if (unit == LobbyUnit.Rabbit)
        {
            icon.texture = _archerSheet;
            icon.uvRect = PoseRegion(1, false);
        }
        else if (unit.PrefabPath != null)
        {
            RawImage portrait = GetArt(unit).Portrait;
            if (portrait != null)
            {
                icon.texture = portrait.texture;
                icon.uvRect = portrait.uvRect;
            }
        }
        icon.enabled = icon.texture != null;
    }

    /// <summary><paramref name="unit"/>의 프리팹과 정지 초상을 처음 표시할 때 읽고 이후에 재사용한다.</summary>
    private UnitArt GetArt(LobbyUnit unit)
    {
        if (_artByUnit.TryGetValue(unit, out UnitArt art))
        {
            return art;
        }
        art = new UnitArt();
        art.Prefab = AssetManager.Instance.LoadResource<GameObject>(unit.PrefabPath);
        if (art.Prefab != null)
        {
            if (unit == LobbyUnit.Turtle)
            {
                Transform portrait = art.Prefab.transform.Find("SE/Weight/Chest/SEArt");
                if (portrait != null)
                {
                    art.Portrait = portrait.GetComponent<RawImage>();
                }
            }
            else
            {
                art.Portrait = art.Prefab.GetComponentInChildren<RawImage>(true);
            }
        }
        _artByUnit.Add(unit, art);
        _loadedArt.Add(art);
        return art;
    }

    /// <summary><paramref name="rect"/>에 마비 트랩의 보라색 보석과 노란 중심을 그린다.</summary>
    internal static void DrawTrap(RectTransform rect)
    {
        rect.localRotation = Quaternion.Euler(0, 0, 45);
        Image gem = rect.gameObject.AddComponent<Image>();
        gem.color = new Color32(187, 137, 245, 255);
        gem.raycastTarget = false;
        RectTransform core = Element("Core", rect, 0, 0, 16, 16);
        Image center = core.gameObject.AddComponent<Image>();
        center.color = Honey;
        center.raycastTarget = false;
    }

    /// <summary><paramref name="unit"/>을 전장의 색 표식 대신 캐릭터로 그릴 수 있는지 확인한다.</summary>
    private bool HasUnitArt(LobbyUnit unit)
    {
        if (unit == LobbyUnit.Rabbit)
        {
            return _archerSheet != null;
        }
        return unit != null && unit.PrefabPath != null && GetArt(unit).Prefab != null;
    }

    /// <summary><paramref name="direction"/> 열의 대기 또는 <paramref name="action"/> 동작 칸을 UV로 반환한다.</summary>
    private static Rect PoseRegion(int direction, bool action) => new Rect(direction * 0.125f, action ? 0.25f : 0.75f, 0.125f, 0.25f);

    /// <summary><paramref name="parent"/> 아래에 한 변이 <paramref name="size"/>인 비입력 캐릭터 이미지를 만든다.</summary>
    private RawImage MakeIcon(Transform parent, float size)
    {
        RawImage icon = Element("UnitPortrait", parent, 0f, 0f, size, size).gameObject.AddComponent<RawImage>();
        icon.raycastTarget = false;
        return icon;
    }

    /// <summary>전장의 사수·방어 유닛·유령을 표시하고 남은 배우는 다음 표시를 위해 숨긴다.</summary>
    private void RefreshUnitIcons()
    {
        _visibleCells.Clear();
        _walkingGhosts.Clear();
        foreach (UnitArt art in _loadedArt)
        {
            art.Used = 0;
        }
        float size = rectTransform.rect.width / LobbyDefenseBoard.Width;
        for (int cell = 0; cell < LobbyDefenseBoard.Width * LobbyDefenseBoard.Height; cell++)
        {
            LobbyUnit unit = _session.UnitAt(cell);
            if (HasUnitArt(unit))
            {
                ShowUnit(unit, cell, size);
            }
        }
        ShowGhosts(size);
        RefreshCombatActors(size);
        for (int i = _visibleCells.Count; i < _unitIcons.Count; i++)
        {
            _unitIcons[i].enabled = false;
        }
        foreach (UnitArt art in _loadedArt)
        {
            for (int i = art.Used; i < art.Actors.Count; i++)
            {
                FieldActor actor = art.Actors[i];
                actor.Rect.gameObject.SetActive(false);
                actor.Cell = -1;
                actor.Monster = null;
            }
        }
        AnimateUnits();
    }

    /// <summary><paramref name="unit"/>의 <paramref name="cell"/> 교전 방향과 실제 역할을 <paramref name="size"/> 크기의 배우에 반영한다.</summary>
    private void ShowUnit(LobbyUnit unit, int cell, float size)
    {
        if (unit == LobbyUnit.Rabbit)
        {
            int used = _visibleCells.Count;
            if (used == _unitIcons.Count)
            {
                _unitIcons.Add(MakeIcon(_actorsRoot, size * 1.18f));
            }
            RawImage icon = _unitIcons[used];
            _visibleCells.Add(cell);
            icon.enabled = true;
            icon.color = cell == _selected ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            icon.texture = _archerSheet;
            icon.rectTransform.SetParent(_rows[cell / LobbyDefenseBoard.Width], false);
            icon.rectTransform.sizeDelta = Vector2.one * size * (_placing ? 1.18f : 2.05f);
            return;
        }
        LobbyDefenseBoard.UnitContact contact = _session.ContactAt(cell);
        int direction = DirectionIndex(new Vector2(contact.ColumnDelta, -contact.RowDelta));
        string pose = "Idle_";
        int actionSecond = -1;
        if (contact.Second > 0 && contact.Second == _session.Seconds)
        {
            bool normalShot = unit.Role == LobbyDefenseBoard.Piece.Archer && !contact.FinishingBlow;
            pose = normalShot ? "Attack_" : "Special_";
            actionSecond = contact.Second;
        }
        ShowActor(GetArt(unit), size, cell, null, pose, direction, actionSecond);
    }

    /// <summary><paramref name="size"/> 크기의 등불유령을 실제 소환·이동·봉쇄 타격·출구 통과에 맞게 표시한다.</summary>
    private void ShowGhosts(float size)
    {
        foreach (LobbyDefenseBoard.Monster monster in _session.Monsters)
        {
            if (!monster.Ghost || !HasUnitArt(LobbyUnit.Ghost))
            {
                continue;
            }
            int direction = MovementDirection(monster.PreviousCell, monster.FacingCell);
            string pose = "Walk_";
            int actionSecond = -1;
            if (monster.SpawnSecond == _session.Seconds)
            {
                pose = "Special_";
                actionSecond = _session.Seconds;
            }
            else if (monster.StrikeSecond == _session.Seconds)
            {
                pose = "Attack_";
                actionSecond = _session.Seconds;
            }
            FieldActor actor = ShowActor(GetArt(LobbyUnit.Ghost), size, monster.Cell, monster, pose, direction, actionSecond);
            if (monster.Cell != monster.PreviousCell)
            {
                actor.Start = ActorCenter(monster.PreviousCell, size);
                actor.End = ActorCenter(monster.Cell, size);
                _walkingGhosts.Add(actor);
            }
        }
        if (_session.GhostExitSecond == _session.Seconds && HasUnitArt(LobbyUnit.Ghost))
        {
            int direction = MovementDirection(_session.GhostExitFrom, LobbyDefenseBoard.Exit);
            ShowActor(GetArt(LobbyUnit.Ghost), size, LobbyDefenseBoard.Exit, null, "Special_", direction, _session.Seconds);
        }
    }

    /// <summary><paramref name="art"/>의 <paramref name="size"/> 크기 배우를 <paramref name="cell"/>에 두고 <paramref name="monster"/> 또는 유닛이 표시할
    /// <paramref name="pose"/>를 <paramref name="direction"/> 방향으로 재생하며 <paramref name="actionSecond"/>로 연속 타격을 구분한다.</summary>
    private FieldActor ShowActor(UnitArt art, float size, int cell, LobbyDefenseBoard.Monster monster, string pose, int direction, int actionSecond)
    {
        if (art.Used == art.Actors.Count)
        {
            RectTransform rect = (RectTransform)Instantiate(art.Prefab, _actorsRoot, false).transform;
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Animator animator = rect.GetComponent<Animator>();
            animator.keepAnimatorStateOnDisable = true;
            art.Actors.Add(new FieldActor { Rect = rect, Animator = animator, Group = group });
        }
        FieldActor actor = art.Actors[art.Used++];
        actor.Rect.gameObject.SetActive(true);
        actor.Rect.SetParent(_rows[cell / LobbyDefenseBoard.Width], false);
        actor.Rect.localScale = Vector3.one * (size * (_placing ? 1.12f : 1.92f) / actor.Rect.rect.width);
        actor.Rect.anchoredPosition = ActorCenter(cell, size);
        actor.Group.alpha = cell == _selected && monster == null ? 0.35f : 1f;
        int state = Animator.StringToHash("Base Layer." + pose + _directions[direction]);
        bool changed = actor.Monster != monster || (monster == null && actor.Cell != cell);
        if (changed || actor.State != state || actor.ActionSecond != actionSecond)
        {
            actor.Animator.Play(state, 0, 0f);
            actor.State = state;
            actor.ActionSecond = actionSecond;
        }
        actor.Cell = cell;
        actor.Monster = monster;
        return actor;
    }

    /// <summary>전장에서 한 변이 <paramref name="size"/>인 <paramref name="cell"/>의 중심을 구한다.</summary>
    private Vector2 CellCenter(int cell, float size)
    {
        float x = rectTransform.rect.xMin + (cell % LobbyDefenseBoard.Width + 0.5f) * size;
        float y = rectTransform.rect.yMax - (cell / LobbyDefenseBoard.Width + 0.5f) * rectTransform.rect.height / LobbyDefenseBoard.Height;
        return new Vector2(x, y);
    }

    /// <summary><paramref name="from"/> 칸에서 <paramref name="to"/> 칸을 바라보는 전장 애니메이션 방향을 구한다.</summary>
    private static int MovementDirection(int from, int to)
    {
        int dx = to % LobbyDefenseBoard.Width - from % LobbyDefenseBoard.Width;
        int dy = from / LobbyDefenseBoard.Width - to / LobbyDefenseBoard.Width;
        return DirectionIndex(new Vector2(dx, dy));
    }

    /// <summary>화면상 <paramref name="aim"/>을 여덟 방향으로 바꾸고 첫 교전 전에는 서쪽 입구를 바라본다.</summary>
    private static int DirectionIndex(Vector2 aim)
    {
        if (aim == Vector2.zero)
        {
            return WestDirection;
        }
        float angle = Mathf.Atan2(aim.x, -aim.y) * Mathf.Rad2Deg;
        return (Mathf.RoundToInt(angle / 45f) + 8) % 8;
    }

    /// <summary>화면에 보이는 유닛만 마지막 교전 방향으로 움직이고 타격 후 같은 방향의 대기로 되돌린다.</summary>
    private void AnimateUnits()
    {
        float progress = Mathf.Clamp01(_elapsed / 0.35f);
        foreach (FieldActor ghost in _walkingGhosts)
        {
            ghost.Rect.anchoredPosition = Vector2.Lerp(ghost.Start, ghost.End, progress);
        }
        for (int i = 0; i < _visibleCells.Count; i++)
        {
            int cell = _visibleCells[i];
            RawImage icon = _unitIcons[i];
            LobbyDefenseBoard.UnitContact contact = _session.ContactAt(cell);
            Vector2 aim = new Vector2(contact.ColumnDelta, -contact.RowDelta).normalized;
            int direction = DirectionIndex(aim);
            bool action = contact.Second > 0 && contact.Second == _session.Seconds && _elapsed < 0.42f;
            icon.uvRect = PoseRegion(direction, action);
            RectTransform rect = icon.rectTransform;
            float breath = 0.04f * Mathf.Sin((_session.Seconds % 2 + _elapsed) * Mathf.PI + cell);
            rect.localScale = new Vector3(1f - breath * 0.5f, 1f + breath, 1f);
            float size = rectTransform.rect.width / LobbyDefenseBoard.Width;
            Vector2 position = ActorCenter(cell, size);
            if (action)
            {
                float reach = Mathf.Sin(_elapsed / 0.42f * Mathf.PI);
                position += aim * (size * 0.22f * reach);
            }
            rect.anchoredPosition = position;
        }
        AnimateCombat();
    }

    /// <summary>인벤토리에서 선택한 <paramref name="card"/>를 메인 전장의 배치 후보로 지정한다.</summary>
    internal void SelectCard(LobbyCard card)
    {
        _selectedCard = card;
        _moving = -1;
        _selected = -1;
        _placing = true;
        Refresh();
    }

    /// <summary><paramref name="rect"/>에 <paramref name="size"/> 크기의 상태 문구를 만든다.</summary>
    private TMP_Text Label(RectTransform rect, int size)
    {
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.fontSize = size;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    /// <summary><paramref name="caption"/> 버튼을 (<paramref name="x"/>, <paramref name="y"/>)에 <paramref name="width"/> × <paramref name="height"/> 크기로 만들고 <paramref name="action"/>을 연결한다.</summary>
    private Button MakeButton(Transform parent, string caption, int x, int y, int width, int height, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Element(caption, parent, x, y, width, height);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = CardBlue;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(action);
        Label(Element("Label", rect, 0, 0, width, height), 24).text = caption;
        return button;
    }

    /// <summary>로비가 활성화된 시간만 정수 초 단위로 모델에 전달한다.</summary>
    private void Update()
    {
        FitTab();
        if (!_session.Started)
        {
            return;
        }
        _elapsed += Time.deltaTime;
        if (_elapsed < 1f)
        {
            if (_world.enabled)
            {
                AnimateUnits();
            }
            return;
        }
        bool finished = _session.Finished;
        while (_elapsed >= 1f)
        {
            _elapsed -= 1f;
            _session.Tick();
        }
        if (!finished && _session.Finished)
        {
            SaveProgress();
        }
        if (_world.enabled)
        {
            Refresh();
        }
    }

    /// <summary><paramref name="eventData"/>의 화면 좌표를 보드 칸으로 변환해 대표 캐릭터 또는 배치 후보 칸을 선택한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 point))
        {
            return;
        }
        Rect area = rectTransform.rect;
        int column = Mathf.FloorToInt((point.x - area.xMin) * LobbyDefenseBoard.Width / area.width);
        int row = Mathf.FloorToInt((area.yMax - point.y) * LobbyDefenseBoard.Height / area.height);
        if (column < 0 || column >= LobbyDefenseBoard.Width || row < 0 || row >= LobbyDefenseBoard.Height)
        {
            return;
        }
        int cell = row * LobbyDefenseBoard.Width + column;
        LobbyCard card = _session.CardAt(cell);
        if (!_placing)
        {
            if (card != null)
            {
                _focused = card;
                RefreshPlacementControls();
                _hero.PlaySpecial();
            }
            return;
        }
        if (_session.Finished && card != null)
        {
            _moving = cell;
            _selectedCard = null;
            _selected = -1;
        }
        else if (_selectedCard != null || _moving >= 0)
        {
            _selected = cell;
        }
        Refresh();
    }

    /// <summary>선택한 후보 칸의 배치를 확정한다.</summary>
    private void Confirm()
    {
        if (!_placing || _selected < 0)
        {
            return;
        }
        bool placed = _moving >= 0 ? _session.MoveUnit(_moving, _selected) : _session.PlaceCard(_selected, _selectedCard);
        if (placed)
        {
            SaveProgress();
            _focused = _session.CardAt(_selected);
            CancelPlacement();
            return;
        }
        Refresh();
    }

    /// <summary>현재 후보 경로와 보유 카드, 스테이지 상태를 화면에 반영한다.</summary>
    private void Refresh()
    {
        if (_moving >= 0 && (!_session.Finished || !_session.HasUnit(_moving)))
        {
            _moving = -1;
            _selected = -1;
        }
        _previewLength = LobbyDefenseBoard.InvalidPlacement;
        if (_moving >= 0)
        {
            _previewLength = _session.PreviewMove(_moving, _selected);
        }
        else if (_selectedCard != null)
        {
            _previewLength = _session.Preview(_selected, SelectedRole);
        }
        FitBattlefield();
        RefreshStageControls();
        RefreshPlacementControls();
        RefreshUnitIcons();
        SetVerticesDirty();
    }

    /// <summary>스테이지명 옆 진행 버튼과 보유 골드, 다음 소환 시점을 표시한다.</summary>
    private void RefreshStageControls()
    {
        string timer = "첫 카드 배치 대기";
        if (_session.Finished)
        {
            timer = _session.Cleared ? "방어 완료" : $"돌파 {_session.Save.Escaped}기";
        }
        else if (_session.Started)
        {
            timer = _session.SpawnsFinished ? "남은 적 방어" : $"다음 소환 {_session.NextSpawn}초";
        }
        _stageButton.gameObject.SetActive(_session.Finished);
        _stageAction.text = _session.Cleared ? "다음 스테이지" : "다시 도전";
        _summary.text = $"스테이지 {_session.Stage}";
        _gold.text = $"{_session.Gold} 골드";
        _waveTime.text = timer;
    }

    /// <summary>배치 모드에서만 조작 도구를 열고 대표 캐릭터와 선택한 셀의 경로를 갱신한다.</summary>
    private void RefreshPlacementControls()
    {
        bool preview = _placing && _selected >= 0 && _previewLength != LobbyDefenseBoard.InvalidPlacement;
        _previewActor.gameObject.SetActive(preview);
        _play.SetActive(!_placing);
        _playHint.SetActive(!_placing);
        _placeIcon.SetActive(!_placing);
        _placementBar.SetActive(_placing);
        LobbyCard card = _selectedCard;
        if (_moving >= 0)
        {
            card = _session.CardAt(_moving);
        }
        if (card == null)
        {
            card = _focused;
            if (card == null || card.Cell < 0)
            {
                card = _session.Cards[0];
                foreach (LobbyCard owned in _session.Cards)
                {
                    if (owned.Cell >= 0)
                    {
                        card = owned;
                        break;
                    }
                }
            }
        }
        if (preview)
        {
            RectTransform actor = (RectTransform)_previewActor.transform;
            actor.anchoredPosition = CellCenter(_selected, rectTransform.rect.width / LobbyDefenseBoard.Width);
            _previewActor.Bind(card.Unit, this);
        }
        _hero.Bind(card.Unit, this);
        _selectionText.fontSize = _placing ? 21 : 26;
        _selectionText.text = _placing ? $"{card.Unit.Name}  ·  {card.Grade}\n{card.Stats}" : $"{card.Unit.Name}  ·  {card.Grade}";
        if (!_placing)
        {
            return;
        }
        _confirmPlacement.interactable = _previewLength != LobbyDefenseBoard.InvalidPlacement;
        if (_selected >= 0)
        {
            if (!_confirmPlacement.interactable)
            {
                _hint.text = "이 칸에는 배치할 수 없습니다";
                return;
            }
            _hint.text = _session.PreviewSiege ? "통로 봉쇄 · 방벽 공격" : $"선택한 칸으로 배치하면 경로 {_previewLength}칸";
            return;
        }
        _hint.text = "배치할 빈 칸을 선택하세요";
        if (_moving >= 0)
        {
            _hint.text = "유닛을 옮길 빈 칸을 선택하세요";
        }
        else if (_session.Finished)
        {
            _hint.text = _selectedCard == null ? "옮길 유닛을 선택하세요" : "배치할 빈 칸 또는 옮길 유닛을 선택하세요";
        }
        else if (_selectedCard == null)
        {
            _hint.text = "보유 카드가 모두 배치되어 있습니다";
        }
    }

    /// <summary><paramref name="mesh"/>에 격자, 경로, 유닛과 체력이 표시된 몬스터를 그린다.</summary>
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (_session == null)
        {
            return;
        }
        if (!_placing)
        {
            DrawBattleGround(mesh);
            return;
        }
        LobbyDefenseBoard.Piece selectedPiece = _moving >= 0 ? _session.PieceAt(_moving) : SelectedRole;
        for (int cell = 0; cell < LobbyDefenseBoard.Width * LobbyDefenseBoard.Height; cell++)
        {
            Color tint = Color.white;
            if (_selected >= 0 && selectedPiece == LobbyDefenseBoard.Piece.Archer && LobbyDefenseBoard.Separation(cell, _selected) <= LobbyDefenseBoard.Range)
            {
                tint = new Color32(178, 238, 233, 255);
            }
            Tile(mesh, cell, 0.036f, tint, 1f, GroundUV(cell, _session.HasUnit(cell)));
        }
        DrawRoute(mesh, false, new Color32(77, 161, 166, 255));
        if (_previewLength >= 0)
        {
            DrawRoute(mesh, true, new Color32(248, 197, 100, 255));
        }
        DrawTraps(mesh);
        Tile(mesh, LobbyDefenseBoard.Entrance, 0.12f, new Color32(130, 212, 154, 255));
        Tile(mesh, LobbyDefenseBoard.Exit, 0.12f, new Color32(209, 164, 241, 255));
        if (_selected >= 0)
        {
            bool valid = _previewLength != LobbyDefenseBoard.InvalidPlacement;
            Color border = valid ? Honey : new Color32(255, 111, 103, 255);
            Tile(mesh, _selected, 0.035f, border);
            Tile(mesh, _selected, 0.105f, Color.white, 1f, GroundUV(_selected, _session.HasUnit(_selected)));
        }
    }

    /// <summary><paramref name="cell"/> 위치와 <paramref name="occupied"/>에 맞춰 잔디 또는 흙 아틀라스의 네 무늬 중 하나를 고른다.</summary>
    private static Rect GroundUV(int cell, bool occupied)
    {
        int variant = (cell / LobbyDefenseBoard.Width * 7 + cell * 13 + cell / 3) % 4;
        float u = (occupied ? 0.5f : 0f) + (variant % 2) * 0.25f;
        float v = (variant / 2) * 0.5f;
        return new Rect(u + 0.025f, v + 0.05f, 0.20f, 0.40f);
    }

    /// <summary><paramref name="preview"/> 경로를 <paramref name="shade"/> 색으로 <paramref name="mesh"/>에 표시한다.</summary>
    private void DrawRoute(VertexHelper mesh, bool preview, Color32 shade)
    {
        int cell = LobbyDefenseBoard.Entrance;
        while (cell >= 0)
        {
            Tile(mesh, cell, 0.385f, shade);
            cell = _session.Next(cell, preview);
        }
    }

    /// <summary><paramref name="mesh"/>의 보이는 <paramref name="cell"/> 안에 여백, 색, 너비 비율을 적용해 그린다.</summary>
    private void Tile(VertexHelper mesh, int cell, float inset, Color32 shade, float fraction = 1f, Rect uv = default)
    {
        int column = cell % LobbyDefenseBoard.Width;
        Rect area = rectTransform.rect;
        float width = area.width / LobbyDefenseBoard.Width;
        float height = area.height / LobbyDefenseBoard.Height;
        float left = area.xMin + (column + inset) * width;
        float top = area.yMax - (cell / LobbyDefenseBoard.Width + inset) * height;
        float right = left + width * (1f - 2f * inset) * fraction;
        float bottom = top - height * (1f - 2f * inset);
        // 아틀라스 왼쪽 아래 흰 견본은 경로·체력 등 단색 표식에 사용한다.
        if (uv.width == 0f)
        {
            uv = new Rect(0.01f, 0.02f, 0f, 0f);
        }
        int index = mesh.currentVertCount;
        mesh.AddVert(new Vector3(left, bottom), shade, new Vector2(uv.xMin, uv.yMin));
        mesh.AddVert(new Vector3(left, top), shade, new Vector2(uv.xMin, uv.yMax));
        mesh.AddVert(new Vector3(right, top), shade, new Vector2(uv.xMax, uv.yMax));
        mesh.AddVert(new Vector3(right, bottom), shade, new Vector2(uv.xMax, uv.yMin));
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
