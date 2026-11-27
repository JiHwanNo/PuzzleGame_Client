using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>고정된 로비 전장 위에 선택한 페이지를 슬라이드하고 하단 탭과 전장 입력을 관리한다.</summary>
internal sealed class LobbyNavigation : MonoBehaviour
{
    private const int MainPage = 2;
    private const string MotionKey = "Lobby.TabMotion";
    private static readonly string[] Names = { "상점", "인벤토리", "메인", "미확정", "옵션" };
    private const int PieceCount = 3;
    private static readonly Color Ink = new Color32(18, 35, 49, 255);
    private static readonly Color Surface = new Color32(35, 61, 73, 255);
    private static readonly Color Gold = new Color32(255, 211, 132, 255);
    private readonly RectTransform[] _pages = new RectTransform[5];
    private readonly Image[] _marks = new Image[5];
    private readonly TMP_Text[] _prices = new TMP_Text[3];
    private readonly TMP_Text[] _counts = new TMP_Text[3];
    private TMP_Text _motionLabel;
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
        RectTransform banner = Panel("Header", page, 0, .60f, 1, 1, Surface);
        Icon(banner, index, .51f);
        Label(banner, Names[index], .08f, .06f, .92f, .33f, 56);
        Panel("Rule", page, .08f, .596f, .92f, .60f, Gold);
        if (index < MainPage)
        {
            bool shop = index == 0;
            Label(page, shop ? "전장 고용" : "전장에 배치된 유닛", .08f, .49f, .92f, .56f, 27);
            for (int i = 0; i < PieceCount; i++)
            {
                int slot = i;
                float left = .055f + .30f * i;
                RectTransform card = Panel("Unit" + i, page, left, .19f, left + .285f, .47f, Surface);
                UnitImage(card, i);
                Label(card, LobbyDefenseView.PieceName((LobbyDefenseBoard.Piece)(i + 1)), .02f, .25f, .98f, .43f, 27);
                TMP_Text value = Label(card, "", .02f, .05f, .98f, .23f, 23);
                value.color = Gold;
                if (shop)
                {
                    _prices[i] = value;
                    card.gameObject.AddComponent<Button>().onClick.AddListener(() => Place(slot));
                }
                else
                {
                    _counts[i] = value;
                }
            }
            if (shop)
            {
                Label(page, "유닛을 선택한 뒤 전장에서 배치를 확정하세요", .04f, .09f, .96f, .16f, 22);
            }
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

    /// <summary><paramref name="parent"/>에 <paramref name="slot"/> 유닛의 실제 초상 또는 트랩 표식을 표시한다.</summary>
    private void UnitImage(RectTransform parent, int slot)
    {
        if (slot == 2)
        {
            RawImage trap = Square(parent, .68f, .25f);
            trap.enabled = false;
            LobbyDefenseView.DrawTrap(Box("Gem", trap.transform, 0, 0, 1, 1));
            return;
        }
        RawImage image = Square(parent, .68f, .43f);
        _field.SetPortrait(image, (LobbyDefenseBoard.Piece)(slot + 1));
    }

    /// <summary><paramref name="slot"/> 유닛을 선택하여 메인의 기존 배치 흐름으로 돌아간다.</summary>
    private void Place(int slot)
    {
        _field.SelectPiece((LobbyDefenseBoard.Piece)(slot + 1));
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
            RefreshValues();
            _pages[_target].gameObject.SetActive(true);
            _pages[_target].GetComponent<CanvasGroup>().blocksRaycasts = false;
        }
        if (_current != MainPage)
        {
            _pages[_current].GetComponent<CanvasGroup>().blocksRaycasts = false;
        }
        Slide(0f);
    }

    /// <summary>현재 전장 모델에서 고용 비용과 배치 수량을 읽는다.</summary>
    private void RefreshValues()
    {
        for (int i = 0; i < PieceCount; i++)
        {
            LobbyDefenseBoard.Piece piece = (LobbyDefenseBoard.Piece)(i + 1);
            _prices[i].text = _field.Board.CostOf(piece) + " 골드";
            int count = 0;
            for (int cell = 0; cell < LobbyDefenseBoard.Width * LobbyDefenseBoard.Height; cell++)
            {
                if (_field.Board.PieceAt(cell) == piece)
                {
                    count++;
                }
            }
            _counts[i].text = count + "개";
        }
    }

    /// <summary>전환을 마친 뒤 가려진 전장 렌더링을 쉬고 마지막 탭 요청을 처리한다.</summary>
    private void Update()
    {
        if (_current == _target)
        {
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
