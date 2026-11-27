using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>고정된 로비 전장 위에 선택한 페이지를 슬라이드하고 하단 탭과 전장 입력을 관리한다.</summary>
internal sealed class LobbyNavigation : MonoBehaviour
{
    private const int MainPage = 2;
    private const string MotionKey = "Lobby.TabMotion";
    private static readonly string[] Names = { "상점", "인벤토리", "메인", "미확정", "옵션" };
    private const int CardsPerPage = 6;
    private static readonly Color Ink = new Color32(18, 35, 49, 255);
    private static readonly Color Surface = new Color32(35, 61, 73, 255);
    private static readonly Color Gold = new Color32(255, 211, 132, 255);
    private readonly RectTransform[] _pages = new RectTransform[5];
    private readonly Image[] _marks = new Image[5];
    private readonly CatalogPage[] _catalogs = new CatalogPage[2];
    private int _shownGold;
    private int _shownRosterVersion = -1;
    private TMP_Text _motionLabel;
    private Button _draw;
    private TMP_Text _shopTitle;
    private TMP_FontAsset _font;
    private Texture2D _icons;
    private LobbyDefenseView _field;
    private RectTransform _root;
    private GameObject _shield;
    private int _current = MainPage;
    private int _target = MainPage;
    private int _requested = MainPage;
    private float _elapsed;
    private bool _motion;

    /// <summary>한 슬롯에서 현재 캐릭터의 이름, 정지 초상, 능력치와 배치 상태를 표시한다.</summary>
    private sealed class UnitCard
    {
        public RectTransform Rect;
        public RawImage Portrait;
        public GameObject Trap;
        public TMP_Text Name;
        public TMP_Text Value;
        public Button Place;
    }

    /// <summary>상점과 인벤토리가 각각 기억하는 목록 위치와 재사용할 카드들을 보관한다.</summary>
    private sealed class CatalogPage
    {
        public readonly UnitCard[] Cards = new UnitCard[CardsPerPage];
        public int Index;
        public TMP_Text PageNumber;
        public Button Previous;
        public Button Next;
    }

    /// <summary><paramref name="status"/>의 캔버스에 탭을 배치하고 <paramref name="field"/>를 메인으로 연결한다.</summary>
    internal static void Create(TMP_Text status, LobbyDefenseView field)
    {
        RectTransform root = Box("LobbyNavigation", status.canvas.rootCanvas.transform, 0, 0, 1, 1);
        root.SetSiblingIndex(status.transform.parent.GetSiblingIndex() + 1);
        LobbyNavigation nav = root.gameObject.AddComponent<LobbyNavigation>();
        nav._root = root;
        nav._font = status.font;
        nav._field = field;
        nav._icons = AssetManager.Instance.LoadAsset<Texture2D>("LobbyNavigation");
        nav._motion = PlayerPrefs.GetInt(MotionKey, 1) == 1;
        nav.Build();
        field.InventoryRequested += nav.OpenInventory;
    }

    /// <summary>페이지와 탭 바를 한 번 생성하고 첫 선택을 메인으로 표시한다.</summary>
    private void Build()
    {
        _shield = Panel("InputShield", _root, 0, 0, 1, 1, Color.clear).gameObject;
        _shield.SetActive(false);
        RectTransform bar = Panel("TabBar", _root, 0, 0, 1, .10f, Ink);
        for (int i = 0; i < Names.Length; i++)
        {
            if (i != MainPage)
            {
                _pages[i] = BuildPage(i);
                _pages[i].gameObject.SetActive(false);
            }
            int index = i;
            RectTransform tab = Panel("Tab" + i, bar, i / 5f, .02f, (i + 1) / 5f, 1f, Ink);
            tab.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(index));
            Icon(tab, i, .44f);
            Label(tab, Names[i], .04f, .05f, .96f, .34f, 23);
            _marks[i] = Panel("Selected", tab, .16f, .955f, .84f, 1, Gold).GetComponent<Image>();
            _marks[i].enabled = i == MainPage;
        }
        bar.SetAsLastSibling();
    }

    /// <summary><paramref name="index"/> 페이지에 표제와 실제 배치·보유 정보 또는 옵션을 구성한다.</summary>
    private RectTransform BuildPage(int index)
    {
        RectTransform page = Panel("Page" + index, _root, 0, .10f, 1, 1, Ink);
        page.gameObject.AddComponent<CanvasGroup>();
        bool catalog = index < MainPage;
        float headerBottom = catalog ? .79f : .60f;
        RectTransform banner = Panel("Header", page, 0, headerBottom, 1, 1, Surface);
        Icon(banner, index, .51f);
        Label(banner, Names[index], .08f, .06f, .92f, .33f, 56);
        Panel("Rule", page, .08f, headerBottom - .004f, .92f, headerBottom, Gold);
        if (catalog)
        {
            BuildCatalog(page, index);
        }
        else if (index == 3)
        {
            Label(page, "준비 중", .1f, .30f, .9f, .48f, 36);
        }
        else
        {
            RectTransform row = Panel("Motion", page, .08f, .36f, .92f, .50f, Surface);
            Label(row, "탭 전환 애니메이션", .02f, .1f, .68f, .9f, 29);
            _motionLabel = Label(row, MotionText(), .69f, .1f, .98f, .9f, 28);
            _motionLabel.color = Gold;
            row.gameObject.AddComponent<Button>().onClick.AddListener(ToggleMotion);
        }
        return page;
    }

    /// <summary><paramref name="parent"/>에 <paramref name="tab"/>의 여섯 카드와 페이지 이동 버튼을 구성한다.</summary>
    private void BuildCatalog(RectTransform parent, int tab)
    {
        CatalogPage page = new CatalogPage();
        _catalogs[tab] = page;
        if (tab == 0)
        {
            _shopTitle = Label(parent, "", .04f, .715f, .51f, .775f, 26);
            RectTransform draw = Panel("Draw", parent, .53f, .71f, .95f, .779f, Surface);
            _draw = draw.gameObject.AddComponent<Button>();
            _draw.targetGraphic = draw.GetComponent<Image>();
            _draw.onClick.AddListener(DrawCard);
            Label(draw, $"카드 뽑기 · {LobbyDefenseBoard.DrawCost} 골드", 0, 0, 1, 1, 24);
        }
        else
        {
            Label(parent, "보유한 방어 카드", .04f, .715f, .96f, .775f, 29);
        }
        for (int i = 0; i < CardsPerPage; i++)
        {
            int slot = i;
            float left = .045f + .31f * (i % 3);
            float bottom = .464f - .272f * (i / 3);
            RectTransform rect = Panel("Unit" + i, parent, left, bottom, left + .289f, bottom + .234f, Surface);
            UnitCard card = new UnitCard();
            card.Rect = rect;
            card.Portrait = Square(rect, .68f, .54f);
            RawImage trapFrame = Square(rect, .68f, .28f);
            trapFrame.enabled = false;
            card.Trap = trapFrame.gameObject;
            LobbyDefenseView.DrawTrap(Box("Gem", trapFrame.transform, 0, 0, 1, 1));
            card.Name = Label(rect, "", .025f, .26f, .975f, .42f, 26);
            card.Value = Label(rect, "", .025f, .015f, .975f, .25f, 22);
            card.Name.fontSizeMin = 8;
            card.Value.fontSizeMin = 8;
            card.Value.color = Gold;
            page.Cards[i] = card;
            if (tab == 1)
            {
                card.Place = rect.gameObject.AddComponent<Button>();
                card.Place.targetGraphic = rect.GetComponent<Image>();
                card.Place.onClick.AddListener(() => Place(slot));
            }
        }
        page.Previous = CatalogButton(parent, "이전", .08f, () => TurnPage(tab, -1));
        page.Next = CatalogButton(parent, "다음", .69f, () => TurnPage(tab, 1));
        page.PageNumber = Label(parent, "", .34f, .065f, .66f, .15f, 27);
    }

    /// <summary><paramref name="parent"/>의 <paramref name="left"/> 위치에 <paramref name="caption"/>을 표시하고 <paramref name="click"/>을 연결한다.</summary>
    private Button CatalogButton(RectTransform parent, string caption, float left, UnityEngine.Events.UnityAction click)
    {
        RectTransform rect = Panel(caption, parent, left, .065f, left + .23f, .15f, Surface);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(click);
        Label(rect, caption, 0, 0, 1, 1, 26);
        return button;
    }

    /// <summary><paramref name="tab"/>의 현재 페이지를 <paramref name="offset"/>만큼 이동한다.</summary>
    private void TurnPage(int tab, int offset)
    {
        CatalogPage page = _catalogs[tab];
        int lastPage = (CardCount(tab) - 1) / CardsPerPage;
        page.Index = Mathf.Clamp(page.Index + offset, 0, lastPage);
        RefreshCatalog(tab);
    }

    /// <summary><paramref name="tab"/>의 도감 또는 실제 보유 카드 수를 반환한다.</summary>
    private int CardCount(int tab) => tab == 0 ? LobbyUnit.Catalog.Count : _field.Board.Cards.Count;

    /// <summary>전장 아래 카드 배치 버튼으로 보유 카드 목록을 연다.</summary>
    private void OpenInventory() => Select(1);

    /// <summary>뽑기에 성공하면 인벤토리에서 획득한 카드가 있는 페이지를 연다.</summary>
    private void DrawCard()
    {
        LobbyCard drawn = _field.DrawCard();
        if (drawn == null)
        {
            return;
        }
        _catalogs[1].Index = (_field.Board.Cards.Count - 1) / CardsPerPage;
        OpenInventory();
    }

    /// <summary>인벤토리 <paramref name="slot"/>의 미배치 카드를 선택한 상태로 메인 전장으로 돌아간다.</summary>
    private void Place(int slot)
    {
        int index = _catalogs[1].Index * CardsPerPage + slot;
        LobbyCard card = _field.Board.Cards[index];
        if (card.Cell >= 0)
        {
            return;
        }
        _field.SelectCard(card);
        Select(MainPage);
    }

    /// <summary>현재 애니메이션 옵션의 표시 문구를 반환한다.</summary>
    private string MotionText() => _motion ? "켜짐" : "꺼짐";

    /// <summary>전환 효과 사용 여부를 바꾸고 다음 로비 진입에도 적용한다.</summary>
    private void ToggleMotion()
    {
        _motion = !_motion;
        PlayerPrefs.SetInt(MotionKey, _motion ? 1 : 0);
        PlayerPrefs.Save();
        _motionLabel.text = MotionText();
    }

    /// <summary><paramref name="index"/>를 마지막 요청으로 기억하고 선택 표시를 갱신한다.</summary>
    private void Select(int index)
    {
        _requested = index;
        for (int i = 0; i < _marks.Length; i++)
        {
            _marks[i].enabled = i == index;
        }
        if (_current == _target && _current != index)
        {
            Begin();
        }
    }

    /// <summary>다음 페이지 내용을 갱신하고 전환 동안 콘텐츠 입력을 막는다.</summary>
    private void Begin()
    {
        _target = _requested;
        _elapsed = 0f;
        _shield.SetActive(true);
        if (_target == MainPage)
        {
            _field.ShowField(true);
        }
        else
        {
            if (_target < MainPage)
            {
                RefreshCatalog(_target);
            }
            _pages[_target].gameObject.SetActive(true);
            _pages[_target].GetComponent<CanvasGroup>().blocksRaycasts = false;
        }
        if (_current != MainPage)
        {
            _pages[_current].GetComponent<CanvasGroup>().blocksRaycasts = false;
        }
        Slide(0f);
    }

    /// <summary><paramref name="tab"/>에 해당하는 여섯 칸의 초상과 전장의 실제 능력치와 배치 상태를 갱신한다.</summary>
    private void RefreshCatalog(int tab)
    {
        CatalogPage page = _catalogs[tab];
        int lastPage = (CardCount(tab) - 1) / CardsPerPage;
        if (tab == 0)
        {
            _shopTitle.text = $"보유 {_field.Board.Gold} 골드";
            _draw.interactable = _field.Board.Gold >= LobbyDefenseBoard.DrawCost;
        }
        page.Previous.interactable = page.Index > 0;
        page.Next.interactable = page.Index < lastPage;
        page.PageNumber.text = $"{page.Index + 1} / {lastPage + 1}";
        for (int slot = 0; slot < CardsPerPage; slot++)
        {
            int index = page.Index * CardsPerPage + slot;
            UnitCard card = page.Cards[slot];
            bool occupied = index < CardCount(tab);
            card.Rect.gameObject.SetActive(occupied);
            if (!occupied)
            {
                continue;
            }
            LobbyUnit unit;
            if (tab == 0)
            {
                unit = LobbyUnit.Catalog[index];
                card.Name.text = unit.Name;
                card.Value.text = "길막";
                if (unit.Role == LobbyDefenseBoard.Piece.Archer)
                {
                    card.Value.text = "사수";
                }
                else if (unit.Role == LobbyDefenseBoard.Piece.Trap)
                {
                    card.Value.text = "트랩";
                }
            }
            else
            {
                LobbyCard owned = _field.Board.Cards[index];
                unit = owned.Unit;
                card.Name.text = $"{unit.Name} · {owned.Grade}";
                card.Place.interactable = owned.Cell < 0;
                string action = owned.Cell < 0 ? "배치하기" : "배치 중";
                card.Value.text = $"{owned.Stats}\n{action}";
            }
            card.Trap.SetActive(unit == LobbyUnit.Trap);
            _field.SetPortrait(card.Portrait, unit);
        }
        _shownRosterVersion = _field.Board.RosterVersion;
        _shownGold = _field.Board.Gold;
    }

    /// <summary>전환을 마친 뒤 가려진 전장 렌더링을 쉬고 마지막 탭 요청을 처리한다.</summary>
    private void Update()
    {
        if (_current == _target)
        {
            bool goldChanged = _current == 0 && _shownGold != _field.Board.Gold;
            if (_current < MainPage && (goldChanged || _shownRosterVersion != _field.Board.RosterVersion))
            {
                RefreshCatalog(_current);
            }
            return;
        }
        _elapsed += Time.unscaledDeltaTime;
        float t = _motion ? Mathf.Clamp01(_elapsed / .25f) : 1f;
        Slide(Mathf.SmoothStep(0, 1, t));
        if (t < 1f)
        {
            return;
        }
        if (_current != MainPage)
        {
            _pages[_current].gameObject.SetActive(false);
        }
        _current = _target;
        if (_current != MainPage)
        {
            _pages[_current].GetComponent<CanvasGroup>().blocksRaycasts = true;
        }
        _field.ShowField(_current == MainPage);
        _shield.SetActive(_current != MainPage);
        if (_requested != _current)
        {
            Begin();
        }
    }

    /// <summary><paramref name="progress"/>에 맞춰 나가는 패널과 들어오는 패널의 가로 위치만 바꾼다.</summary>
    private void Slide(float progress)
    {
        float distance = _root.rect.width * Mathf.Sign(_target - _current);
        if (_current != MainPage)
        {
            _pages[_current].anchoredPosition = Vector2.left * distance * progress;
        }
        if (_target != MainPage)
        {
            _pages[_target].anchoredPosition = Vector2.right * distance * (1f - progress);
        }
    }

    /// <summary><paramref name="parent"/> 안의 지정한 앵커 범위에 <paramref name="name"/> 영역을 만든다.</summary>
    private static RectTransform Box(string name, Transform parent, float left, float bottom, float right, float top)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    /// <summary><paramref name="parent"/>의 지정 범위에 <paramref name="color"/> 배경을 추가한다.</summary>
    private static RectTransform Panel(string name, Transform parent, float left, float bottom, float right, float top, Color color)
    {
        RectTransform rect = Box(name, parent, left, bottom, right, top);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    /// <summary><paramref name="parent"/>의 지정 범위에 <paramref name="value"/>를 <paramref name="size"/> 이하로 표시한다.</summary>
    private TMP_Text Label(Transform parent, string value, float left, float bottom, float right, float top, int size)
    {
        TMP_Text label = Box("Label", parent, left, bottom, right, top).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.text = value;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12;
        label.fontSizeMax = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    /// <summary><paramref name="parent"/> 중앙에 <paramref name="height"/> 비율 정사각형을 <paramref name="center"/> 높이로 배치한다.</summary>
    private static RawImage Square(Transform parent, float center, float height)
    {
        RectTransform bounds = Box("Art", parent, .03f, center - height / 2f, .97f, center + height / 2f);
        RectTransform rect = Box("Icon", bounds, 0, 0, 1, 1);
        AspectRatioFitter fit = rect.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectRatio = 1f;
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        RawImage icon = rect.gameObject.AddComponent<RawImage>();
        icon.raycastTarget = false;
        return icon;
    }

    /// <summary><paramref name="parent"/>에 <paramref name="index"/> 탭의 원화를 <paramref name="height"/> 비율로 배치한다.</summary>
    private void Icon(RectTransform parent, int index, float height)
    {
        RawImage icon = Square(parent, .65f, height);
        icon.texture = _icons;
        icon.uvRect = new Rect(index / 5f, 0, .2f, 1);
    }
}
