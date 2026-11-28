using TMPro;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>고정된 로비 전장 위에 선택한 페이지를 슬라이드하고 하단 탭과 전장 입력을 관리한다.</summary>
internal sealed partial class LobbyNavigation : MonoBehaviour
{
    private const int MainPage = 2;
    private const string MotionKey = "Lobby.TabMotion";
    private static readonly string[] Names = { "상점", "인벤토리", "메인", "미확정", "옵션" };
    private const int CardsPerPage = 6;
    private static readonly Color Ink = new Color32(13, 67, 79, 255);
    private static readonly Color Lagoon = new Color32(6, 105, 117, 255);
    private static readonly Color Mint = new Color32(159, 245, 222, 255);
    private static readonly Color Pearl = new Color32(253, 255, 240, 255);
    private static readonly Color Lemon = new Color32(255, 226, 91, 255);
    private static readonly Color Surface = new Color32(35, 61, 73, 255);
    private static readonly Color Gold = new Color32(255, 211, 132, 255);
    private readonly RectTransform[] _pages = new RectTransform[5];
    private readonly RectTransform[] _pageTops = new RectTransform[5];
    private readonly Image[] _marks = new Image[5];
    private readonly TMP_Text[] _tabLabels = new TMP_Text[5];
    private readonly CatalogPage _shop = new CatalogPage();
    private int _barGold = -1;
    private int _barLives = -1;
    private long? _barGems;
    private double _nextLifeCheck;
    private TMP_Text _lifeAmount;
    private TMP_Text _goldAmount;
    private TMP_Text _gemAmount;
    private TMP_Text _profileName;
    private TMP_Text _profileInitial;
    private string _shownNickname;
    private RectTransform _resourceBar;
    private Rect _resourceSafeArea;
    private Vector2Int _resourceScreenSize;
    private Vector2 _resourceCanvasSize;
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

    /// <summary>상점 도감 한 칸의 정지 초상, 이름과 역할을 표시한다.</summary>
    private sealed class UnitCard
    {
        public RectTransform Rect;
        public RawImage Portrait;
        public GameObject Trap;
        public TMP_Text Name;
        public TMP_Text Value;
    }

    /// <summary>상점 도감의 현재 페이지와 여섯 개의 표시 슬롯을 보관한다.</summary>
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
        nav._cardFrame = AssetManager.Instance.LoadAsset<Texture2D>("LobbyInventoryCard");
        nav._cardIcons = AssetManager.Instance.LoadAsset<Texture2D>("LobbyCardIcons");
        nav._motion = PlayerPrefs.GetInt(MotionKey, 1) == 1;
        nav.Build();
        field.PlacementChanged += nav.RefreshBottomBar;
        nav.RefreshBottomBar();
    }

    /// <summary>페이지와 탭 바를 한 번 생성하고 첫 선택을 메인으로 표시한다.</summary>
    private void Build()
    {
        _shield = Panel("InputShield", _root, 0, 0, 1, 1, Color.clear).gameObject;
        _shield.SetActive(false);
        RectTransform bar = JellyPanel("TabBar", _root, 0, 0, 1, .10f, Lagoon);
        _tabInput = bar.gameObject.AddComponent<CanvasGroup>();
        for (int i = 0; i < Names.Length; i++)
        {
            if (i != MainPage)
            {
                _pages[i] = BuildPage(i);
                _pages[i].gameObject.SetActive(false);
            }
            int index = i;
            RectTransform tab = Panel("Tab" + i, bar, i / 5f, 0, (i + 1) / 5f, 1, Color.clear);
            _marks[i] = JellyPanel("Selected", tab, .035f, .035f, .965f, .985f, Mint).GetComponent<Image>();
            _marks[i].raycastTarget = false;
            Button button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = Icon(tab, i, .645f, .59f);
            button.onClick.AddListener(() => Select(index));
            _tabLabels[i] = Label(tab, Names[i], .035f, .055f, .965f, .32f, 26);
            _tabLabels[i].fontStyle = FontStyles.Bold;
        }
        ShowSelectedTab(MainPage);
        bar.SetAsLastSibling();
        BuildPlacementInventory();
        BuildResourceBar();
        BuildCardDetail();
    }

    /// <summary>모든 탭 위에 독립된 재화 캡슐 세 개를 표시하고 입력은 아래 화면에 전달한다.</summary>
    private void BuildResourceBar()
    {
        _resourceBar = Box("ResourceBar", _root, 0, 1, 1, 1);
        _resourceBar.pivot = new Vector2(.5f, 1);
        CanvasGroup group = _resourceBar.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        Texture2D atlas = AssetManager.Instance.LoadAsset<Texture2D>("LobbyResourceBar");
        _lifeAmount = ResourceAmount(atlas, "Life", "라이프", 0);
        _goldAmount = ResourceAmount(atlas, "Gold", "골드", 1);
        _gemAmount = ResourceAmount(atlas, "Gem", "잼", 2);
        BuildProfile();
        LayoutResourceBar();
        RefreshResources();
    }

    /// <summary><paramref name="atlas"/>의 <paramref name="column"/>번째 메달과 <paramref name="name"/> 잔량을 만들며, 원화가 없으면 <paramref name="caption"/>을 표시한다.</summary>
    private TMP_Text ResourceAmount(Texture2D atlas, string name, string caption, int column)
    {
        // D안의 666폭 기준 원본 치수로 메달과 캡슐이 겹치는 위치를 유지한다.
        float left = (column * 222f + 4f) / 666f;
        RectTransform capsule = Box(name + "Capsule", _resourceBar, left + 17f / 666f, .087f, left + 214f / 666f, .913f);
        RectTransform medal = Box(name + "Medal", _resourceBar, left, .027f, left + 63f / 666f, .973f);
        if (atlas != null)
        {
            float row = (2 - column) / 3f;
            ResourceArt(capsule, atlas, new Rect(0, row, .75f, 1f / 3f));
            ResourceArt(medal, atlas, new Rect(.75f, row, .25f, 1f / 3f));
        }
        else
        {
            Image backing = capsule.gameObject.AddComponent<Image>();
            LagoonUI.Surface(backing, Lagoon);
            backing.raycastTarget = false;
            TMP_Text title = Label(medal, caption, .19f, 0, 1, 1, 22);
            title.color = Mint;
        }

        TMP_Text amount = Label(_resourceBar, "—", left + 65f / 666f, .197f, left + 201f / 666f, .833f, 35);
        amount.name = name + "Amount";
        TMP_FontAsset numberFont = TMP_Settings.defaultFontAsset;
        if (numberFont != null)
        {
            amount.font = numberFont;
        }
        amount.color = new Color32(255, 246, 217, 255);
        amount.fontStyle = FontStyles.Bold;
        amount.fontSizeMin = 4;
        amount.textWrappingMode = TextWrappingModes.NoWrap;
        return amount;
    }

    /// <summary>재화 줄 아래 왼쪽에 닉네임 첫 글자와 계정 이름을 배치한다.</summary>
    private void BuildProfile()
    {
        RectTransform profile = JellyPanel("UserProfile", _resourceBar, .006f, -.95f, .36f, -.09f, Ink);
        RectTransform portrait = JellyPanel("Monogram", profile, .025f, .12f, .215f, .88f, Mint);
        _profileInitial = Label(portrait, "·", .08f, .06f, .92f, .94f, 33);
        _profileInitial.color = Ink;
        _profileInitial.richText = false;
        _profileInitial.fontStyle = FontStyles.Bold;
        _profileName = Label(profile, "플레이어", .25f, .10f, .96f, .90f, 28);
        _profileName.richText = false;
        _profileName.alignment = TextAlignmentOptions.Left;
        _profileName.textWrappingMode = TextWrappingModes.NoWrap;
        _profileName.overflowMode = TextOverflowModes.Ellipsis;
        _profileName.fontStyle = FontStyles.Bold;
        _profileName.color = Pearl;
    }

    /// <summary><paramref name="identity"/>에 이미 로드된 닉네임을 프로필에 표시한다.</summary>
    private void RefreshProfile(IdentityLayer identity)
    {
        string nickname = null;
        if (identity != null && identity.Data != null)
        {
            nickname = identity.Data.nickname;
        }
        if (_shownNickname != nickname)
        {
            _shownNickname = nickname;
            bool named = !string.IsNullOrWhiteSpace(nickname);
            _profileName.text = named ? nickname : "플레이어";
            _profileInitial.text = named ? StringInfo.GetNextTextElement(nickname) : "·";
        }
    }

    /// <summary><paramref name="target"/>에 <paramref name="atlas"/>의 <paramref name="region"/>을 입력을 받지 않는 원화로 표시한다.</summary>
    private static void ResourceArt(RectTransform target, Texture2D atlas, Rect region)
    {
        RawImage art = target.gameObject.AddComponent<RawImage>();
        art.texture = atlas;
        art.uvRect = region;
        art.raycastTarget = false;
    }

    /// <summary>화면 안전 영역의 너비에 맞춰 메달을 정사각형으로 유지하고, 그 아래에 탭 페이지 공간을 확보한다.</summary>
    private void LayoutResourceBar()
    {
        Rect safe = Screen.safeArea;
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        Vector2 canvas = _root.rect.size;
        if (screen.x <= 0 || screen.y <= 0 || canvas.x <= 0f || canvas.y <= 0f)
        {
            return;
        }
        if (safe.width <= 0f || safe.height <= 0f)
        {
            safe = new Rect(0, 0, screen.x, screen.y);
        }
        if (_resourceScreenSize == screen && _resourceSafeArea == safe && _resourceCanvasSize == canvas)
        {
            return;
        }
        _resourceScreenSize = screen;
        _resourceSafeArea = safe;
        _resourceCanvasSize = canvas;

        float safeWidth = canvas.x * safe.width / screen.x;
        float safeHeight = canvas.y * safe.height / screen.y;
        float width = Mathf.Min(safeWidth * .924f, safeHeight * .72f);
        float height = width * .10f;
        float topMargin = height * .40f;
        Vector2 top = new Vector2(safe.center.x / screen.x, safe.yMax / screen.y);
        _resourceBar.anchorMin = top;
        _resourceBar.anchorMax = top;
        _resourceBar.sizeDelta = new Vector2(width, height);
        _resourceBar.anchoredPosition = new Vector2(0, -topMargin);

        float pageTop = top.y - (topMargin + height * 2.16f) / canvas.y;
        Rect safeCanvas = new Rect(safe.x * canvas.x / screen.x, safe.y * canvas.y / screen.y, safeWidth, safeHeight);
        LayoutPlacementArea(safeCanvas, canvas, pageTop * canvas.y);
        for (int i = 0; i < _pages.Length; i++)
        {
            if (_pages[i] != null)
            {
                _pages[i].anchorMax = new Vector2(1f, pageTop);
                _pageTops[i].sizeDelta = new Vector2(0, canvas.y * (1f - pageTop));
            }
        }
    }

    /// <summary>보상·소비와 계정 변경을 반영하고, 저장된 라이프 충전 시각은 초당 한 번만 확인한다.</summary>
    private void RefreshResources()
    {
        int gold = _field.Board.Gold;
        if (gold != _barGold)
        {
            _barGold = gold;
            _goldAmount.text = gold.ToString("N0", CultureInfo.InvariantCulture);
            _draw.interactable = gold >= LobbyDefenseBoard.DrawCost;
        }

        UserDataManager user = UserDataManager.Instance;
        RefreshProfile(user != null ? user.Identity : null);
        double now = Time.unscaledTimeAsDouble;
        if (now >= _nextLifeCheck)
        {
            _nextLifeCheck = now + 1d;
            int lives = PlayerLives.GetRemaining(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out _);
            if (lives != _barLives)
            {
                _barLives = lives;
                _lifeAmount.text = $"{lives}/{PlayerLives.Capacity}";
            }
        }

        long? gems = null;
        if (user != null && user.Identity != null && user.Identity.IsLoaded && user.Identity.Data != null)
        {
            gems = (long)user.Identity.Data.freeDia + user.Identity.Data.paidDia;
        }
        if (_barGems != gems)
        {
            _barGems = gems;
            _gemAmount.text = gems.HasValue ? gems.Value.ToString("N0", CultureInfo.InvariantCulture) : "—";
        }
    }

    /// <summary><paramref name="index"/> 페이지에 표제와 실제 배치·보유 정보 또는 옵션을 구성한다.</summary>
    private RectTransform BuildPage(int index)
    {
        RectTransform page = Panel("Page" + index, _root, 0, .10f, 1, 1, Ink);
        page.gameObject.AddComponent<CanvasGroup>();
        RectTransform top = Panel("TopBackground", page, 0, 1, 1, 1, Ink);
        top.pivot = new Vector2(.5f, 0);
        top.GetComponent<Image>().raycastTarget = false;
        _pageTops[index] = top;
        bool catalog = index < MainPage;
        float headerBottom = catalog ? .79f : .60f;
        RectTransform banner = Panel("Header", page, 0, headerBottom, 1, 1, Surface);
        Icon(banner, index, .65f, .51f);
        Label(banner, Names[index], .08f, .06f, .92f, .33f, 56);
        Panel("Rule", page, .08f, headerBottom - .004f, .92f, headerBottom, Gold);
        if (index == 0)
        {
            BuildShop(page);
        }
        else if (index == 1)
        {
            BuildInventoryList(page);
        }
        else if (index == 3)
        {
            Label(page, "준비 중", .1f, .30f, .9f, .48f, 36);
        }
        else
        {
            RectTransform row = Panel("Motion", page, .08f, .36f, .92f, .50f, Surface);
            Label(row, "화면 전환 애니메이션", .02f, .1f, .68f, .9f, 29);
            _motionLabel = Label(row, MotionText(), .69f, .1f, .98f, .9f, 28);
            _motionLabel.color = Gold;
            row.gameObject.AddComponent<Button>().onClick.AddListener(ToggleMotion);
        }
        return page;
    }

    /// <summary><paramref name="parent"/>에 상점 도감과 카드 뽑기, 페이지 이동 버튼을 구성한다.</summary>
    private void BuildShop(RectTransform parent)
    {
        CatalogPage page = _shop;
        _shopTitle = Label(parent, "", .04f, .715f, .51f, .775f, 26);
        RectTransform draw = Panel("Draw", parent, .53f, .71f, .95f, .779f, Surface);
        _draw = draw.gameObject.AddComponent<Button>();
        _draw.targetGraphic = draw.GetComponent<Image>();
        _draw.onClick.AddListener(DrawCard);
        Label(draw, $"카드 뽑기 · {LobbyDefenseBoard.DrawCost} 골드", 0, 0, 1, 1, 24);
        for (int i = 0; i < CardsPerPage; i++)
        {
            float left = .045f + .31f * (i % 3);
            float bottom = .464f - .272f * (i / 3);
            RectTransform rect = Panel("Unit" + i, parent, left, bottom, left + .289f, bottom + .234f, Surface);
            UnitCard card = new UnitCard();
            card.Rect = rect;
            page.Cards[i] = card;
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
        }
        page.Previous = CatalogButton(parent, "이전", .08f, () => TurnPage(-1));
        page.Next = CatalogButton(parent, "다음", .69f, () => TurnPage(1));
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

    /// <summary>상점 도감에서 <paramref name="offset"/>만큼 페이지를 이동한다.</summary>
    private void TurnPage(int offset)
    {
        int lastPage = (LobbyUnit.Catalog.Count - 1) / CardsPerPage;
        _shop.Index = Mathf.Clamp(_shop.Index + offset, 0, lastPage);
        RefreshCatalog(0);
    }

    /// <summary>뽑기에 성공하면 인벤토리를 열고 새로 획득한 카드까지 스크롤한다.</summary>
    private void DrawCard()
    {
        LobbyCard drawn = _field.DrawCard();
        if (drawn == null)
        {
            return;
        }
        _revealNewestCard = true;
        Select(1);
    }

    /// <summary>선택한 <paramref name="card"/>가 아직 배치되지 않았으면 전장으로 연결한다.</summary>
    private void Place(LobbyCard card)
    {
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
        if (index != 1 && _detailPanel.activeSelf)
        {
            HideCardDetail();
        }
        _requested = index;
        ShowSelectedTab(index);
        if (_current == _target && _current != index)
        {
            Begin();
        }
    }

    /// <summary><paramref name="index"/>에 해당하는 탭은 민트 받침과 짙은 글자로 표시한다.</summary>
    private void ShowSelectedTab(int index)
    {
        for (int i = 0; i < _marks.Length; i++)
        {
            bool selected = i == index;
            _marks[i].enabled = selected;
            _tabLabels[i].color = selected ? Ink : Pearl;
        }
    }

    /// <summary>다음 페이지 내용을 갱신하고 전환 동안 콘텐츠 입력을 막는다.</summary>
    private void Begin()
    {
        _target = _requested;
        RefreshBottomBar();
        _elapsed = 0f;
        _shield.SetActive(true);
        if (_target == MainPage)
        {
            _field.ShowField(true);
        }
        else
        {
            _pages[_target].gameObject.SetActive(true);
            if (_target < MainPage)
            {
                RefreshCatalog(_target);
            }
            _pages[_target].GetComponent<CanvasGroup>().blocksRaycasts = false;
        }
        if (_current != MainPage)
        {
            _pages[_current].GetComponent<CanvasGroup>().blocksRaycasts = false;
        }
        Slide(0f);
    }

    /// <summary><paramref name="tab"/>에 해당하는 도감 또는 보유 카드 목록을 최신 전장 정보로 갱신한다.</summary>
    private void RefreshCatalog(int tab)
    {
        _shownRosterVersion = _field.Board.RosterVersion;
        _shownGold = _field.Board.Gold;
        if (tab == 1)
        {
            RefreshInventoryList(true);
            return;
        }
        CatalogPage page = _shop;
        int count = LobbyUnit.Catalog.Count;
        int lastPage = (count - 1) / CardsPerPage;
        _shopTitle.text = $"보유 {_shownGold} 골드";
        _draw.interactable = _shownGold >= LobbyDefenseBoard.DrawCost;
        page.Previous.interactable = page.Index > 0;
        page.Next.interactable = page.Index < lastPage;
        page.PageNumber.text = $"{page.Index + 1} / {lastPage + 1}";
        for (int slot = 0; slot < CardsPerPage; slot++)
        {
            int index = page.Index * CardsPerPage + slot;
            UnitCard card = page.Cards[slot];
            bool occupied = index < count;
            card.Rect.gameObject.SetActive(occupied);
            if (!occupied)
            {
                continue;
            }
            LobbyUnit unit = LobbyUnit.Catalog[index];
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
            card.Trap.SetActive(unit == LobbyUnit.Trap);
            _field.SetPortrait(card.Portrait, unit);
        }
    }

    /// <summary>전환을 마친 뒤 가려진 전장 렌더링을 쉬고 마지막 탭 요청을 처리한다.</summary>
    private void Update()
    {
        if (_current == MainPage && _placementGlow != null && _placementGlow.isActiveAndEnabled)
        {
            float brightness = _motion ? .82f + .18f * Mathf.Cos(Time.unscaledTime * 4f) : 1f;
            _placementGlow.canvasRenderer.SetAlpha(brightness);
        }
        LayoutResourceBar();
        RefreshResources();
        if (_current == _target)
        {
            bool goldChanged = _current == 0 && _shownGold != _field.Board.Gold;
            if (_current < MainPage && (goldChanged || _shownRosterVersion != _field.Board.RosterVersion))
            {
                RefreshCatalog(_current);
            }
            if (_detailPanel.activeSelf)
            {
                UpdateCardDetail();
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
        RefreshBottomBar();
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

    /// <summary><paramref name="parent"/>의 지정 앵커 안에 <paramref name="name"/> 젤리 패널을 만들고 <paramref name="color"/>로 물들인다.</summary>
    private static RectTransform JellyPanel(string name, Transform parent, float left, float bottom, float right, float top, Color color)
    {
        RectTransform rect = Box(name, parent, left, bottom, right, top);
        LagoonUI.Surface(rect.gameObject.AddComponent<Image>(), color);
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

    /// <summary><paramref name="parent"/>의 <paramref name="center"/> 위치에 <paramref name="index"/> 탭 원화를 <paramref name="height"/> 높이로 배치한다.</summary>
    private RawImage Icon(RectTransform parent, int index, float center, float height)
    {
        RawImage icon = Square(parent, center, height);
        icon.texture = _icons;
        icon.uvRect = new Rect(index / 5f, 0, .2f, 1);
        return icon;
    }
}
