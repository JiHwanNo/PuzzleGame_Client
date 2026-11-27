using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>로비 디펜스의 경로와 전투를 그리고 칸 선택 및 고용 입력을 전달한다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class LobbyDefenseView : MaskableGraphic, IPointerClickHandler
{
    private const int PageWidth = 20;
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
    private static readonly Color DeepBlue = new Color32(22, 43, 62, 255);
    private static readonly Color CardBlue = new Color32(45, 76, 98, 255);
    private static readonly Color SelectedBlue = new Color32(32, 111, 145, 255);
    private static readonly Color Honey = new Color32(255, 213, 117, 255);
    private readonly Image[] _cards = new Image[3];
    private readonly LobbyUnit[] _choices = { LobbyUnit.Rabbit, LobbyUnit.Turtle, LobbyUnit.Trap };
    private readonly RawImage[] _portraits = new RawImage[3];
    private readonly TMP_Text[] _names = new TMP_Text[3];
    private readonly TMP_Text[] _costs = new TMP_Text[3];
    private RectTransform _backdrop;
    private RectTransform _actorsRoot;
    private TMP_Text _gold;
    private TMP_Text _waveTime;
    private Button _previous;
    private Button _next;
    private TMP_Text _summary;
    private TMP_Text _hint;
    private TMP_Text _page;
    private Button _hire;
    private LobbyUnit _selectedUnit = LobbyUnit.Rabbit;
    private LobbyDefenseBoard.Piece SelectedRole => _selectedUnit.Role;
    private int _first;
    private int _columns = PageWidth;
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

    /// <summary><paramref name="camera"/> 앞에 고정된 전장을 만들고 <paramref name="status"/>의 글꼴과 화면 크기를 사용한다.</summary>
    internal static LobbyDefenseView Create(TMP_Text status, Camera camera)
    {
        if (_session == null)
        {
            _session = new LobbyDefenseBoard();
            _session.StartFirstWave();
        }
        GameObject field = new GameObject("DefenseWorld", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(field, status.gameObject.scene);
        Canvas world = field.GetComponent<Canvas>();
        world.renderMode = RenderMode.WorldSpace;
        world.worldCamera = camera;
        world.sortingLayerName = "Ingame";
        field.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 10f, camera.transform.rotation);
        RectTransform board = Element("DefenseBoard", field.transform, 0, 180, 720, 360);
        LobbyDefenseView view = board.gameObject.AddComponent<LobbyDefenseView>();
        view._world = world;
        view._mainTab = status.transform.parent;
        view._font = status.font;
        view._archerSheet = AssetManager.Instance.LoadAsset<Texture2D>("LobbyArcher8Way");
        view._canvasRect = (RectTransform)status.canvas.rootCanvas.transform;
        view._backdrop = Element("LobbyBackground", field.transform, 0, 0, 1, 1);
        view._backdrop.SetAsFirstSibling();
        Image backdrop = view._backdrop.gameObject.AddComponent<Image>();
        backdrop.color = DeepBlue;
        backdrop.raycastTarget = false;
        view.FitTab();
        view._actorsRoot = Element("FieldActors", board, 0, 0, 720, 360);
        view.BuildControls();
        view.Refresh();
        return view;
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

    /// <summary>전투 상태와 구역 이동 및 배치 확정 버튼을 구성한다.</summary>
    private void BuildControls()
    {
        _summary = Label(Element("Wave", transform, -160, 328, 400, 60), 44);
        _summary.alignment = TextAlignmentOptions.MidlineLeft;
        _gold = Label(Element("Gold", transform, 245, 328, 230, 60), 33);
        _gold.alignment = TextAlignmentOptions.MidlineRight;
        _gold.color = Honey;

        _previous = MakeButton("이전", -313, 232, 94, 64, PreviousPage);
        Button pageButton = MakeButton("전체 보기", -129, 232, 252, 64, ToggleOverview);
        _page = pageButton.GetComponentInChildren<TMP_Text>();
        _next = MakeButton("다음", 55, 232, 94, 64, NextPage);
        _waveTime = Label(Element("WaveTimer", transform, 238, 232, 244, 60), 24);
        _waveTime.alignment = TextAlignmentOptions.MidlineRight;

        for (int i = 0; i < _cards.Length; i++)
        {
            LobbyDefenseBoard.Piece piece = (LobbyDefenseBoard.Piece)(i + 1);
            BuildUnitCard(piece, i);
        }
        _hint = Label(Element("PlacementHint", transform, -113, -394, 494, 100), 24);
        _hint.alignment = TextAlignmentOptions.MidlineLeft;
        _hire = MakeButton("배치 확정", 253, -394, 214, 78, Confirm);
        _hire.image.color = SelectedBlue;
    }

    /// <summary><paramref name="piece"/>의 초상·이름·비용을 <paramref name="index"/>번째 선택 카드에 배치한다.</summary>
    private void BuildUnitCard(LobbyDefenseBoard.Piece piece, int index)
    {
        string name = _choices[index].Name;
        Button card = MakeButton(name, (index - 1) * 246, -278, 228, 124, () => SelectUnit(_choices[index]));
        _cards[index] = card.image;
        TMP_Text label = card.GetComponentInChildren<TMP_Text>();
        label.rectTransform.anchoredPosition = new Vector2(44, 17);
        label.rectTransform.sizeDelta = new Vector2(136, 42);
        label.enableAutoSizing = true;
        label.fontSizeMin = 15;
        label.fontSizeMax = 25;
        _names[index] = label;
        _costs[index] = Label(Element("Cost", card.transform, 44, -20, 132, 32), 20);
        _costs[index].color = Honey;

        Vector2 position = new Vector2(-66, 0);
        if (piece == LobbyDefenseBoard.Piece.Trap)
        {
            RectTransform trap = Element("Trap", card.transform, position.x, 0, 46, 46);
            DrawTrap(trap);
        }
        else
        {
            RawImage icon = MakeIcon(card.transform, 94);
            icon.rectTransform.anchoredPosition = position;
            _portraits[index] = icon;
            SetPortrait(icon, _choices[index]);
        }
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

    /// <summary><paramref name="cell"/>이 현재 확대 구역에 있는지 확인한다.</summary>
    private bool InPage(int cell)
    {
        int column = cell % LobbyDefenseBoard.Width;
        return column >= _first && column < _first + PageWidth;
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

    /// <summary>확대 구역의 사수·방어 유닛·유령을 표시하고 남은 배우는 다음 표시를 위해 숨긴다.</summary>
    private void RefreshUnitIcons()
    {
        _visibleCells.Clear();
        _walkingGhosts.Clear();
        foreach (UnitArt art in _loadedArt)
        {
            art.Used = 0;
        }
        if (_columns == PageWidth)
        {
            float size = rectTransform.rect.width / PageWidth;
            for (int row = 0; row < LobbyDefenseBoard.Height; row++)
            {
                for (int column = 0; column < PageWidth; column++)
                {
                    int cell = row * LobbyDefenseBoard.Width + _first + column;
                    LobbyUnit unit = _session.UnitAt(cell);
                    if (HasUnitArt(unit))
                    {
                        ShowUnit(unit, cell, size);
                    }
                }
            }
            ShowGhosts(size);
        }
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
                _unitIcons.Add(MakeIcon(_actorsRoot, size));
            }
            RawImage icon = _unitIcons[used];
            _visibleCells.Add(cell);
            icon.enabled = true;
            icon.color = cell == _selected ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            icon.texture = _archerSheet;
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
            if (!monster.Ghost || !InPage(monster.Cell) || !HasUnitArt(LobbyUnit.Ghost))
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
            if (monster.Cell != monster.PreviousCell && InPage(monster.PreviousCell))
            {
                actor.Start = CellCenter(monster.PreviousCell, size);
                actor.End = CellCenter(monster.Cell, size);
                _walkingGhosts.Add(actor);
            }
        }
        if (_session.GhostExitSecond == _session.Seconds && InPage(LobbyDefenseBoard.Exit) && HasUnitArt(LobbyUnit.Ghost))
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
            rect.localScale = Vector3.one * (size / rect.rect.width);
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Animator animator = rect.GetComponent<Animator>();
            animator.keepAnimatorStateOnDisable = true;
            art.Actors.Add(new FieldActor { Rect = rect, Animator = animator, Group = group });
        }
        FieldActor actor = art.Actors[art.Used++];
        actor.Rect.gameObject.SetActive(true);
        actor.Rect.anchoredPosition = CellCenter(cell, size);
        actor.Group.alpha = cell == _selected && monster == null ? 0.35f : 1f;
        int state = Animator.StringToHash(pose + _directions[direction]);
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

    /// <summary>확대 전장에서 한 변이 <paramref name="size"/>인 <paramref name="cell"/>의 중심을 구한다.</summary>
    private Vector2 CellCenter(int cell, float size)
    {
        float x = rectTransform.rect.xMin + (cell % LobbyDefenseBoard.Width - _first + 0.5f) * size;
        float y = rectTransform.rect.yMax - (cell / LobbyDefenseBoard.Width + 0.5f) * size;
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
            float size = rect.sizeDelta.x;
            Vector2 position = CellCenter(cell, size);
            if (action)
            {
                float reach = Mathf.Sin(_elapsed / 0.42f * Mathf.PI);
                position += aim * (size * 0.16f * reach);
            }
            rect.anchoredPosition = position;
        }
    }

    /// <summary><paramref name="unit"/>을 해당 역할의 전장 카드와 다음 배치 후보로 설정한다.</summary>
    internal void SelectUnit(LobbyUnit unit)
    {
        int index = (int)unit.Role - 1;
        _choices[index] = unit;
        _names[index].text = unit.Name;
        if (_portraits[index] != null)
        {
            SetPortrait(_portraits[index], unit);
        }
        _selectedUnit = unit;
        _moving = -1;
        if (_world.enabled)
        {
            Refresh();
        }
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
    private Button MakeButton(string caption, int x, int y, int width, int height, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Element(caption, transform, x, y, width, height);
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
        while (_elapsed >= 1f)
        {
            _elapsed -= 1f;
            _session.Tick();
        }
        if (_world.enabled)
        {
            Refresh();
        }
    }

    /// <summary>앞쪽 구역을 표시한다.</summary>
    private void PreviousPage() => OpenPage(_first - PageWidth);

    /// <summary>뒤쪽 구역을 표시한다.</summary>
    private void NextPage() => OpenPage(_first + PageWidth);

    /// <summary>전체 경로와 마지막으로 열었던 구역 사이를 전환한다.</summary>
    private void ToggleOverview()
    {
        if (_columns == LobbyDefenseBoard.Width)
        {
            OpenPage(_first);
            return;
        }
        _columns = LobbyDefenseBoard.Width;
        _selected = -1;
        _moving = -1;
        Refresh();
    }

    /// <summary><paramref name="first"/>에서 시작하는 구역을 확대한다.</summary>
    private void OpenPage(int first)
    {
        _columns = PageWidth;
        _first = Mathf.Clamp(first, 0, LobbyDefenseBoard.Width - PageWidth);
        _selected = -1;
        Refresh();
    }

    /// <summary><paramref name="eventData"/>의 화면 좌표를 보드 칸으로 변환해 구역 또는 후보 칸을 선택한다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 point))
        {
            return;
        }
        Rect area = rectTransform.rect;
        int column = Mathf.FloorToInt((point.x - area.xMin) * _columns / area.width);
        int row = Mathf.FloorToInt((area.yMax - point.y) * LobbyDefenseBoard.Height / area.height);
        if (column < 0 || column >= _columns || row < 0 || row >= LobbyDefenseBoard.Height)
        {
            return;
        }
        if (_columns == LobbyDefenseBoard.Width)
        {
            OpenPage(column / PageWidth * PageWidth);
            return;
        }
        _selected = row * LobbyDefenseBoard.Width + _first + column;
        if (_session.Rest > 0 && _session.HasUnit(_selected))
        {
            _moving = _selected;
            _selected = -1;
        }
        Refresh();
    }

    /// <summary>선택한 후보 칸의 배치를 확정한다.</summary>
    private void Confirm()
    {
        bool placed = _moving >= 0 ? _session.MoveUnit(_moving, _selected) : _session.Hire(_selected, _selectedUnit);
        if (placed)
        {
            _selected = -1;
            _moving = -1;
        }
        Refresh();
    }

    /// <summary>현재 후보 경로와 비용, 소환 상태를 화면에 반영한다.</summary>
    private void Refresh()
    {
        if (_moving >= 0 && (_session.Rest == 0 || !_session.HasUnit(_moving)))
        {
            _moving = -1;
            _selected = -1;
        }
        _previewLength = _moving >= 0 ? _session.PreviewMove(_moving, _selected) : _session.Preview(_selected, SelectedRole);
        string timer = "첫 유닛 배치 대기";
        if (_session.Rest > 0)
        {
            timer = $"정비 {_session.Rest}초";
        }
        else if (_session.Started)
        {
            timer = _session.SpawnsFinished ? "남은 적 방어" : $"다음 소환 {_session.NextSpawn}초";
        }
        _summary.text = $"웨이브 {_session.Wave:00}";
        _gold.text = $"{_session.Gold} 골드";
        _waveTime.text = timer;
        bool overview = _columns == LobbyDefenseBoard.Width;
        _previous.interactable = overview || _first > 0;
        _next.interactable = overview || _first + PageWidth < LobbyDefenseBoard.Width;
        _page.text = overview ? "구역 보기" : $"{_first + 1}–{_first + PageWidth}열 · 전체";
        for (int i = 0; i < _cards.Length; i++)
        {
            LobbyDefenseBoard.Piece choice = (LobbyDefenseBoard.Piece)(i + 1);
            _cards[i].color = SelectedRole == choice && _moving < 0 ? SelectedBlue : CardBlue;
            _costs[i].text = $"{_session.CostOf(choice)} 골드";
        }
        int cost = _session.CostOf(SelectedRole);
        string order = _moving >= 0 ? "무료 이동" : _selectedUnit.Name;
        string route = _session.RouteLength < 0 ? "봉쇄 중" : $"{_session.RouteLength}칸";
        _hint.text = overview ? "확대할 구역을 선택하세요" : $"{order} · 놓을 칸 선택\n현재 경로 {route}";
        if (_selected >= 0 && _previewLength == LobbyDefenseBoard.InvalidPlacement)
        {
            _hint.text = "이 칸에는 배치할 수 없습니다";
        }
        else if (_selected >= 0)
        {
            string result = _session.PreviewSiege ? "통로 봉쇄 / 방벽 공격" : $"경로 {route} → {_previewLength}칸";
            _hint.text = $"{order} · 배치 미리보기\n{result}";
        }
        _hire.interactable = _previewLength != LobbyDefenseBoard.InvalidPlacement && (_moving >= 0 || _session.Gold >= cost);
        RefreshUnitIcons();
        SetVerticesDirty();
    }

    /// <summary><paramref name="mesh"/>에 격자, 경로, 유닛과 체력이 표시된 몬스터를 그린다.</summary>
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (_session == null)
        {
            return;
        }
        int start = _columns == PageWidth ? _first : 0;
        for (int row = 0; row < LobbyDefenseBoard.Height; row++)
        {
            for (int column = start; column < start + _columns; column++)
            {
                int cell = row * LobbyDefenseBoard.Width + column;
                Color32 shade = new Color32(32, 53, 70, 255);
                LobbyDefenseBoard.Piece selectedPiece = _moving >= 0 ? _session.PieceAt(_moving) : SelectedRole;
                if (_selected >= 0 && selectedPiece == LobbyDefenseBoard.Piece.Archer && LobbyDefenseBoard.Separation(cell, _selected) <= LobbyDefenseBoard.Range)
                {
                    shade = new Color32(53, 89, 97, 255);
                }
                Tile(mesh, cell, 0.02f, shade);
            }
        }
        DrawRoute(mesh, false, new Color32(77, 161, 166, 255));
        if (_previewLength >= 0)
        {
            DrawRoute(mesh, true, new Color32(248, 197, 100, 255));
        }
        for (int cell = 0; cell < LobbyDefenseBoard.Width * LobbyDefenseBoard.Height; cell++)
        {
            if (_session.HasUnit(cell))
            {
                if (_columns == PageWidth && InPage(cell) && HasUnitArt(_session.UnitAt(cell)))
                {
                    continue;
                }
                Color32 tint = new Color32(104, 201, 255, 255);
                if (_session.PieceAt(cell) == LobbyDefenseBoard.Piece.Wall)
                {
                    tint = new Color32(191, 161, 110, 255);
                }
                else if (_session.PieceAt(cell) == LobbyDefenseBoard.Piece.Trap)
                {
                    tint = _session.IsCharged(cell) ? new Color32(197, 127, 255, 255) : new Color32(100, 87, 122, 255);
                }
                Tile(mesh, cell, 0.14f, tint);
            }
        }
        Tile(mesh, LobbyDefenseBoard.Entrance, 0.12f, new Color32(130, 212, 154, 255));
        Tile(mesh, LobbyDefenseBoard.Exit, 0.12f, new Color32(209, 164, 241, 255));
        if (_selected >= 0)
        {
            Tile(mesh, _selected, 0.18f, _previewLength != LobbyDefenseBoard.InvalidPlacement ? Color.yellow : Color.red);
        }
        foreach (LobbyDefenseBoard.Monster monster in _session.Monsters)
        {
            if (monster.Ghost)
            {
                if (_columns != PageWidth || !InPage(monster.Cell) || !HasUnitArt(LobbyUnit.Ghost))
                {
                    Tile(mesh, monster.Cell, 0.32f, new Color32(164, 241, 244, 255));
                }
                continue;
            }
            Color32 body = monster.Stun > 0 ? new Color32(207, 147, 254, 255) : new Color32(241, 123, 102, 255);
            Tile(mesh, monster.Cell, 0.13f, body);
            Tile(mesh, monster.Cell, 0.25f, Color.white, monster.Health / 12f);
        }
    }

    /// <summary><paramref name="preview"/> 경로를 <paramref name="shade"/> 색으로 <paramref name="mesh"/>에 표시한다.</summary>
    private void DrawRoute(VertexHelper mesh, bool preview, Color32 shade)
    {
        int cell = LobbyDefenseBoard.Entrance;
        while (cell >= 0)
        {
            Tile(mesh, cell, 0.32f, shade);
            cell = _session.Next(cell, preview);
        }
    }

    /// <summary><paramref name="mesh"/>의 보이는 <paramref name="cell"/> 안에 여백, 색, 너비 비율을 적용해 그린다.</summary>
    private void Tile(VertexHelper mesh, int cell, float inset, Color32 shade, float fraction = 1f)
    {
        int column = cell % LobbyDefenseBoard.Width;
        if (_columns == PageWidth)
        {
            column -= _first;
        }
        if (column < 0 || column >= _columns)
        {
            return;
        }
        Rect area = rectTransform.rect;
        float width = area.width / _columns;
        float height = area.height / LobbyDefenseBoard.Height;
        float left = area.xMin + (column + inset) * width;
        float top = area.yMax - (cell / LobbyDefenseBoard.Width + inset) * height;
        float right = left + width * (1f - 2f * inset) * fraction;
        float bottom = top - height * (1f - 2f * inset);
        int index = mesh.currentVertCount;
        mesh.AddVert(new Vector3(left, bottom), shade, Vector2.zero);
        mesh.AddVert(new Vector3(left, top), shade, Vector2.zero);
        mesh.AddVert(new Vector3(right, top), shade, Vector2.zero);
        mesh.AddVert(new Vector3(right, bottom), shade, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
