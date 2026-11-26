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
    private GameObject _turtlePrefab;
    private static readonly string[] _directions = { "S", "SE", "E", "NE", "N", "NW", "W", "SW" };
    private readonly List<TurtleActor> _turtles = new List<TurtleActor>();
    private readonly List<RawImage> _unitIcons = new List<RawImage>();
    private readonly List<int> _visibleCells = new List<int>();
    private TMP_Text _summary;
    private TMP_Text _hint;
    private TMP_Text _page;
    private Button _hire;
    private LobbyDefenseBoard.Piece _piece = LobbyDefenseBoard.Piece.Archer;
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

    /// <summary>재사용할 거북이 리그와 마지막으로 재생한 칸의 피격 정보를 보관한다.</summary>
    private sealed class TurtleActor
    {
        public RectTransform Rect;
        public Animator Animator;
        public CanvasGroup Group;
        public int Cell = -1;
        public int ContactSecond = -1;
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
        RectTransform board = Element("DefenseBoard", field.transform, 0, 225, 720, 360);
        LobbyDefenseView view = board.gameObject.AddComponent<LobbyDefenseView>();
        view._world = world;
        view._mainTab = status.transform.parent;
        view._font = status.font;
        view._archerSheet = AssetManager.Instance.LoadAsset<Texture2D>("LobbyArcher8Way");
        view._turtlePrefab = AssetManager.Instance.LoadResource<GameObject>("Prefab/LobbyTurtle");
        view._canvasRect = (RectTransform)status.canvas.rootCanvas.transform;
        view.FitTab();
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
        float scale = Mathf.Min(1f, Mathf.Min(size.x / 760f, size.y / 1200f));
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
        _summary = Label(Element("DefenseStatus", transform, 0, 265, 720, 48), 32);
        _hint = Label(Element("PlacementHint", transform, 0, 215, 720, 46), 25);
        MakeButton("이전", -300, 112, PreviousPage);
        Button pageButton = MakeButton("전체 보기", -114, 236, ToggleOverview);
        _page = pageButton.GetComponentInChildren<TMP_Text>();
        MakeButton("다음", 65, 104, NextPage);
        _hire = MakeButton("배치 확정", 248, 216, Confirm);
        Button archer = MakeButton("공격 유닛", -240, 216, () => SelectPiece(LobbyDefenseBoard.Piece.Archer), 328);
        Button wall = MakeButton("길막 유닛", 0, 216, () => SelectPiece(LobbyDefenseBoard.Piece.Wall), 328);
        AddPortrait(archer, LobbyDefenseBoard.Piece.Archer);
        AddPortrait(wall, LobbyDefenseBoard.Piece.Wall);
        MakeButton("마비 트랩", 240, 216, () => SelectPiece(LobbyDefenseBoard.Piece.Trap), 328);
    }

    /// <summary><paramref name="button"/>의 글자 옆에 <paramref name="piece"/>의 초상을 배치한다.</summary>
    private void AddPortrait(Button button, LobbyDefenseBoard.Piece piece)
    {
        if (!HasUnitArt(piece))
        {
            return;
        }
        RectTransform label = button.GetComponentInChildren<TMP_Text>().rectTransform;
        label.anchoredPosition = new Vector2(23f, 0f);
        label.sizeDelta = new Vector2(150f, 60f);
        Vector2 position = new Vector2(-72f, 0f);
        if (piece == LobbyDefenseBoard.Piece.Wall)
        {
            MakeTurtle(button.transform, 56f).anchoredPosition = position;
            return;
        }
        RawImage icon = MakeIcon(button.transform, 56f);
        icon.rectTransform.anchoredPosition = position;
        icon.texture = _archerSheet;
        icon.uvRect = PoseRegion(0, false);
    }

    /// <summary><paramref name="piece"/>의 전장 표시용 원화 또는 프리팹을 사용할 수 있는지 확인한다.</summary>
    private bool HasUnitArt(LobbyDefenseBoard.Piece piece) =>
        (piece == LobbyDefenseBoard.Piece.Archer && _archerSheet != null) ||
        (piece == LobbyDefenseBoard.Piece.Wall && _turtlePrefab != null);

    /// <summary><paramref name="parent"/> 아래에 거북이 리그를 만들고 <paramref name="size"/>에 맞춰 축소한다.</summary>
    private RectTransform MakeTurtle(Transform parent, float size)
    {
        RectTransform rect = (RectTransform)Instantiate(_turtlePrefab, parent, false).transform;
        rect.localScale = Vector3.one * (size / rect.rect.width);
        return rect;
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

    /// <summary>확대 구역의 배치 유닛에 초상을 표시하고 기존 이미지 객체를 재사용한다.</summary>
    private void RefreshUnitIcons()
    {
        _visibleCells.Clear();
        int turtleCount = 0;
        if (_columns == PageWidth)
        {
            float size = rectTransform.rect.width / PageWidth;
            for (int row = 0; row < LobbyDefenseBoard.Height; row++)
            {
                for (int column = 0; column < PageWidth; column++)
                {
                    int cell = row * LobbyDefenseBoard.Width + _first + column;
                    LobbyDefenseBoard.Piece piece = _session.PieceAt(cell);
                    if (!HasUnitArt(piece))
                    {
                        continue;
                    }
                    if (piece == LobbyDefenseBoard.Piece.Wall)
                    {
                        ShowTurtle(turtleCount, cell, size);
                        turtleCount++;
                        continue;
                    }
                    int used = _visibleCells.Count;
                    if (used == _unitIcons.Count)
                    {
                        _unitIcons.Add(MakeIcon(transform, size));
                    }
                    RawImage icon = _unitIcons[used];
                    _visibleCells.Add(cell);
                    icon.enabled = true;
                    icon.color = cell == _selected ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                    icon.texture = _archerSheet;
                }
            }
        }
        for (int i = _visibleCells.Count; i < _unitIcons.Count; i++)
        {
            _unitIcons[i].enabled = false;
        }
        for (int i = turtleCount; i < _turtles.Count; i++)
        {
            TurtleActor turtle = _turtles[i];
            turtle.Rect.gameObject.SetActive(false);
            turtle.Cell = -1;
        }
        AnimateUnits();
    }

    /// <summary><paramref name="index"/>의 리그를 <paramref name="cell"/>에 <paramref name="size"/> 크기로 배치하고 새 피격을 재생한다.</summary>
    private void ShowTurtle(int index, int cell, float size)
    {
        if (index == _turtles.Count)
        {
            RectTransform rect = MakeTurtle(transform, size);
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Animator animator = rect.GetComponent<Animator>();
            animator.keepAnimatorStateOnDisable = true;
            _turtles.Add(new TurtleActor { Rect = rect, Animator = animator, Group = group });
        }
        TurtleActor turtle = _turtles[index];
        turtle.Rect.gameObject.SetActive(true);
        turtle.Rect.anchoredPosition = CellCenter(cell, size);
        turtle.Group.alpha = cell == _selected ? 0.35f : 1f;
        LobbyDefenseBoard.UnitContact contact = _session.ContactAt(cell);
        if (turtle.Cell == cell && turtle.ContactSecond == contact.Second)
        {
            return;
        }
        bool blocking = contact.Second > 0 && contact.Second == _session.Seconds;
        string pose = blocking ? "Special_" : "Idle_";
        Vector2 aim = new Vector2(contact.ColumnDelta, -contact.RowDelta);
        turtle.Animator.Play(pose + _directions[DirectionIndex(aim)], 0, 0f);
        turtle.Cell = cell;
        turtle.ContactSecond = contact.Second;
    }

    /// <summary>확대 전장에서 한 변이 <paramref name="size"/>인 <paramref name="cell"/>의 중심을 구한다.</summary>
    private Vector2 CellCenter(int cell, float size)
    {
        float x = rectTransform.rect.xMin + (cell % LobbyDefenseBoard.Width - _first + 0.5f) * size;
        float y = rectTransform.rect.yMax - (cell / LobbyDefenseBoard.Width + 0.5f) * size;
        return new Vector2(x, y);
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

    /// <summary><paramref name="piece"/>를 선택하고 이동 중이던 유닛 선택을 해제한다.</summary>
    internal void SelectPiece(LobbyDefenseBoard.Piece piece)
    {
        _piece = piece;
        _moving = -1;
        Refresh();
    }

    /// <summary><paramref name="rect"/>에 <paramref name="size"/> 크기의 상태 문구를 만든다.</summary>
    private TMP_Text Label(RectTransform rect, int size)
    {
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    /// <summary><paramref name="caption"/> 버튼을 지정 위치와 폭으로 만들고 <paramref name="action"/>을 연결한다.</summary>
    private Button MakeButton(string caption, int x, int width, UnityEngine.Events.UnityAction action, int y = -220)
    {
        RectTransform rect = Element(caption, transform, x, y, width, 60);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color32(44, 84, 102, 255);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(action);
        Label(Element("Label", rect, 0, 0, width, 60), 24).text = caption;
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
        bool placed = _moving >= 0 ? _session.MoveUnit(_moving, _selected) : _session.Hire(_selected, _piece);
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
        _previewLength = _moving >= 0 ? _session.PreviewMove(_moving, _selected) : _session.Preview(_selected, _piece);
        string timer = "첫 유닛 배치 대기";
        if (_session.Rest > 0)
        {
            timer = $"정비 {_session.Rest}초 / 유닛 이동 가능";
        }
        else if (_session.Started)
        {
            timer = _session.SpawnsFinished ? "남은 적 방어" : $"다음 소환 {_session.NextSpawn}초";
        }
        _summary.text = $"웨이브 {_session.Wave}     골드 {_session.Gold}     {timer}";
        _page.text = _columns == PageWidth ? $"{_first + 1}-{_first + PageWidth}열 / 전체" : "전체 / 확대";
        string pieceName = "공격 유닛";
        if (_piece == LobbyDefenseBoard.Piece.Wall)
        {
            pieceName = "새싹거북";
        }
        else if (_piece == LobbyDefenseBoard.Piece.Trap)
        {
            pieceName = "마비 트랩";
        }
        int cost = _session.CostOf(_piece);
        string order = _moving >= 0 ? "무료 이동" : $"{pieceName} / {cost}골드";
        string route = _session.RouteLength < 0 ? "봉쇄 중" : $"{_session.RouteLength}칸";
        _hint.text = _columns == PageWidth ? $"{order}   /   칸 선택   /   현재 경로 {route}" : "확대할 구역을 선택하세요";
        if (_selected >= 0 && _previewLength == LobbyDefenseBoard.InvalidPlacement)
        {
            _hint.text = "이 칸에는 배치할 수 없습니다";
        }
        else if (_selected >= 0)
        {
            string result = _session.PreviewSiege ? "통로 봉쇄 / 고립된 적이 방벽 공격" : $"경로 {route} > {_previewLength}칸";
            _hint.text = $"{order}   /   {result}";
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
                LobbyDefenseBoard.Piece selectedPiece = _moving >= 0 ? _session.PieceAt(_moving) : _piece;
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
                LobbyDefenseBoard.Piece piece = _session.PieceAt(cell);
                if (_columns == PageWidth && HasUnitArt(piece))
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
                Tile(mesh, monster.Cell, 0.32f, new Color32(164, 241, 244, 255));
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
