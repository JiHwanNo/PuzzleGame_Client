using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>보유 카드의 그림과 실제 능력치를 조합하고 인벤토리에서 같은 카드를 크게 살펴보게 한다.</summary>
internal sealed partial class LobbyNavigation
{
    private const int InventoryColumns = 3;
    private const float InventoryGap = 12f;
    private const float CardAspect = .75f;
    private readonly List<OwnedSlot> _ownedSlots = new List<OwnedSlot>();
    private ScrollRect _inventoryScroll;
    private RectTransform _inventoryViewport;
    private RectTransform _inventoryContent;
    private Vector2 _inventorySize;
    private int _firstInventoryRow = -1;
    private bool _revealNewestCard;
    private const float DetailZoomSeconds = .28f;
    private Texture2D _cardFrame;
    private Texture2D _cardIcons;
    private TMP_Text _inventoryTitle;
    private GameObject _detailPanel;
    private InventoryArt _detailArt;
    private LobbyCard _detailCard;
    private int _detailCell;
    private Button _detailPlace;
    private Button _detailClose;
    private OwnedSlot _inspectedSlot;
    private RectTransform _detailBounds;
    private Image _detailShade;
    private CanvasGroup _detailControls;
    private float _detailProgress;
    private bool _detailClosing;
    private bool _placeOnClose;
    private CanvasGroup _tabInput;
    private RectTransform _placementBar;
    private RectTransform _placementSafeArea;
    private TMP_Text _placementHint;
    private Vector2 _placementViewportSize;
    private float _placementStep;
    private readonly List<LobbyCard> _availableCards = new List<LobbyCard>();
    private readonly List<PlacementSlot> _placementSlots = new List<PlacementSlot>();
    private ScrollRect _placementScroll;
    private TMP_Text _emptyPlacement;
    private int _placementVersion = -1;
    private LobbyCard _highlightedCard;
    private Image _placementGlow;

    /// <summary>가로 목록에서 재사용할 카드 그림과 선택 테두리를 보관한다.</summary>
    private sealed class PlacementSlot
    {
        public RectTransform Rect;
        public Image Border;
        public InventoryArt Art;
        public LobbyCard Card;
    }

    /// <summary>하단 탭과 교대할 전체 너비의 배치 패널에 안내, 완료 버튼과 가로 카드 목록을 만든다.</summary>
    private void BuildPlacementInventory()
    {
        _placementBar = JellyPanel("PlacementMode", _root, 0, 0, 1, 0, Lagoon);
        _placementBar.pivot = new Vector2(.5f, 0);
        _placementSafeArea = Box("SafeArea", _placementBar, 0, 0, 1, 1);
        RectTransform strip = Box("PlacementGuide", _placementSafeArea, 0, .80f, 1, 1);
        _placementHint = Label(strip, "", .025f, .08f, .78f, .92f, 25);
        _placementHint.color = Pearl;
        RectTransform doneArea = JellyPanel("완료", strip, .80f, .08f, .98f, .92f, Lemon);
        Button done = doneArea.gameObject.AddComponent<Button>();
        done.targetGraphic = doneArea.GetComponent<Image>();
        TMP_Text doneLabel = Label(doneArea, "완료", .06f, .08f, .94f, .92f, 27);
        doneLabel.color = Ink;
        doneLabel.fontStyle = FontStyles.Bold;
        done.onClick.AddListener(_field.CancelPlacement);
        RectTransform tray = Box("PlacementInventory", _placementSafeArea, 0, 0, 1, .80f);
        RectTransform viewport = Panel("Viewport", tray, .012f, .015f, .988f, .985f, Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        _placementScroll = tray.gameObject.AddComponent<ScrollRect>();
        _placementScroll.viewport = viewport;
        _placementScroll.content = Box("Cards", viewport, 0, 0, 0, 1);
        _placementScroll.content.pivot = new Vector2(0, .5f);
        _placementScroll.horizontal = true;
        _placementScroll.vertical = false;
        _placementScroll.movementType = ScrollRect.MovementType.Clamped;
        _placementScroll.scrollSensitivity = 48f;
        _placementScroll.onValueChanged.AddListener(position => BindPlacementSlots());
        _emptyPlacement = Label(tray, "미배치 카드가 없습니다", .05f, .25f, .95f, .75f, 28);
        _emptyPlacement.color = Pearl;
    }

    /// <summary><paramref name="safe"/> 안에 목록을 배치하고 <paramref name="canvas"/>의 중앙 기준으로 <paramref name="pageTop"/>까지 남은 영역을 전장에 전달한다.</summary>
    private void LayoutPlacementArea(Rect safe, Vector2 canvas, float pageTop)
    {
        float barHeight = safe.yMin + Mathf.Min(safe.width * .44f, safe.height * .25f);
        _placementBar.sizeDelta = new Vector2(0f, barHeight);
        _placementSafeArea.offsetMin = safe.min;
        _placementSafeArea.offsetMax = new Vector2(safe.xMax - canvas.x, 0f);
        float gap = Mathf.Min(safe.width, safe.height) * .018f;
        Rect bounds = Rect.MinMaxRect(safe.xMin + gap, barHeight + gap, safe.xMax - gap, pageTop - gap);
        bounds.center -= canvas * .5f;
        _field.SetPlacementBounds(bounds);
    }

    /// <summary>배치 중인 메인 화면에서 하단 탭을 카드 목록으로 교체하고 선택 안내를 갱신한다.</summary>
    private void RefreshBottomBar()
    {
        bool placing = _field.IsPlacing && _current == MainPage && _target == MainPage;
        _tabInput.gameObject.SetActive(!placing);
        _placementBar.gameObject.SetActive(placing);
        if (placing)
        {
            _placementHint.text = _field.GetPlacementHint();
            RefreshPlacementCards();
        }
    }

    /// <summary>미배치 카드와 화면 크기에 맞춰 카드 폭을 정하고 선택한 카드가 보이도록 스크롤한다.</summary>
    private void RefreshPlacementCards()
    {
        LobbyCard selected = _field.SelectedCard;
        bool rosterChanged = _placementVersion != _field.Board.RosterVersion;
        Vector2 viewport = _placementScroll.viewport.rect.size;
        if (viewport.x <= 0f || viewport.y <= 0f || (!rosterChanged && selected == _highlightedCard && viewport == _placementViewportSize))
        {
            return;
        }
        bool resized = viewport != _placementViewportSize;
        float firstCard = _placementStep > 0f ? -_placementScroll.content.anchoredPosition.x / _placementStep : 0f;
        _placementViewportSize = viewport;
        float cardWidth = viewport.y * .94f * CardAspect;
        _placementStep = cardWidth + Mathf.Min(12f, cardWidth * .08f);
        if (rosterChanged)
        {
            _placementVersion = _field.Board.RosterVersion;
            _availableCards.Clear();
            foreach (LobbyCard card in _field.Board.Cards)
            {
                if (card.Cell < 0)
                {
                    _availableCards.Add(card);
                }
            }
        }
        float width = viewport.x;
        RectTransform content = _placementScroll.content;
        float extent = Mathf.Max(width, _availableCards.Count * _placementStep);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, extent);
        float offset = Mathf.Clamp(firstCard * _placementStep, 0f, extent - width);
        if (selected != null && (selected != _highlightedCard || resized))
        {
            int index = _availableCards.IndexOf(selected);
            if (index >= 0)
            {
                offset = Mathf.Clamp(offset, (index + 1) * _placementStep - width, index * _placementStep);
            }
        }
        _placementScroll.StopMovement();
        content.anchoredPosition = new Vector2(-offset, 0f);
        _highlightedCard = selected;
        _emptyPlacement.gameObject.SetActive(_availableCards.Count == 0);
        BindPlacementSlots();
    }

    /// <summary>스크롤 영역에 보이는 카드와 양끝의 부분 카드만 슬롯에 연결한다.</summary>
    private void BindPlacementSlots()
    {
        _placementGlow = null;
        if (_placementStep <= 0f)
        {
            return;
        }
        int visible = Mathf.CeilToInt(_placementScroll.viewport.rect.width / _placementStep) + 1;
        int count = Mathf.Min(visible, _availableCards.Count);
        int first = Mathf.Clamp(Mathf.FloorToInt(-_placementScroll.content.anchoredPosition.x / _placementStep), 0, _availableCards.Count - count);
        while (_placementSlots.Count < count)
        {
            _placementSlots.Add(BuildPlacementSlot());
        }
        for (int i = count; i < _placementSlots.Count; i++)
        {
            _placementSlots[i].Rect.gameObject.SetActive(false);
        }
        // 스크롤로 한 칸 이동하면 새로 들어온 카드의 초상만 교체한다.
        for (int i = 0; i < count; i++)
        {
            int index = first + i;
            PlacementSlot slot = _placementSlots[index % count];
            slot.Rect.gameObject.SetActive(true);
            LobbyCard card = _availableCards[index];
            float height = _placementViewportSize.y * .94f;
            slot.Rect.sizeDelta = new Vector2(height * CardAspect, height);
            slot.Rect.anchoredPosition = new Vector2((index + .5f) * _placementStep, 0);
            if (slot.Card != card)
            {
                slot.Card = card;
                BindOwnedArt(slot.Art, card);
            }
            slot.Border.enabled = card == _highlightedCard;
            if (slot.Border.enabled)
            {
                _placementGlow = slot.Border;
            }
        }
    }

    /// <summary>공용 카드 원화 뒤에 밝은 금색 테두리와 번짐을 넣고 클릭을 전장 선택으로 연결한다.</summary>
    private PlacementSlot BuildPlacementSlot()
    {
        PlacementSlot slot = new PlacementSlot();
        slot.Rect = Panel("PlacementCard", _placementScroll.content, 0, .5f, 0, .5f, Color.clear);
        Button button = slot.Rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => _field.SelectCard(slot.Card));
        slot.Border = JellyPanel("GoldBorder", slot.Rect, 0, 0, 1, 1, new Color32(255, 210, 55, 255)).GetComponent<Image>();
        slot.Border.raycastTarget = false;
        Outline glow = slot.Border.gameObject.AddComponent<Outline>();
        glow.effectColor = new Color32(255, 177, 27, 155);
        glow.effectDistance = new Vector2(3f, 3f);
        slot.Art = BuildOwnedArt(Box("Face", slot.Rect, .035f, .026f, .965f, .974f), false);
        return slot;
    }

    /// <summary>한 보유 카드의 초상, 등급, 이름, 능력치와 배치 상태를 표시한다.</summary>
    private sealed class InventoryArt
    {
        public LobbyCardPortrait Portrait;
        public readonly RawImage[] Stars = new RawImage[3];
        public TMP_Text Name;
        public TMP_Text Stats;
        public TMP_Text Action;
    }

    /// <summary>스크롤할 때 보이는 카드 한 장에 재사용하는 그림과 상세 보기 입력을 보관한다.</summary>
    private sealed class OwnedSlot
    {
        public RectTransform Rect;
        public InventoryArt Art;
        public Button Inspect;
        public CanvasGroup Visibility;
        public LobbyCard Card;
    }

    /// <summary><paramref name="page"/> 안에 보유 카드 수와 손가락·휠로 움직이는 세로 목록을 만든다.</summary>
    private void BuildInventoryList(RectTransform page)
    {
        _inventoryTitle = Label(page, "보유 카드", .04f, .715f, .96f, .775f, 29);
        RectTransform scrollArea = Box("InventoryScroll", page, .035f, .018f, .98f, .705f);
        _inventoryScroll = scrollArea.gameObject.AddComponent<ScrollRect>();
        _inventoryViewport = Panel("Viewport", scrollArea, 0, 0, .962f, 1, Color.clear);
        _inventoryViewport.gameObject.AddComponent<RectMask2D>();
        _inventoryContent = Box("Content", _inventoryViewport, 0, 1, 1, 1);
        _inventoryContent.pivot = new Vector2(.5f, 1f);
        _inventoryScroll.viewport = _inventoryViewport;
        _inventoryScroll.content = _inventoryContent;
        _inventoryScroll.horizontal = false;
        _inventoryScroll.vertical = true;
        _inventoryScroll.movementType = ScrollRect.MovementType.Clamped;
        _inventoryScroll.scrollSensitivity = 42f;
        RectTransform rail = Panel("Scrollbar", scrollArea, .973f, 0, 1, 1, Surface);
        RectTransform handle = Panel("Handle", rail, 0, 0, 1, 1, new Color32(105, 162, 167, 255));
        Scrollbar scrollbar = rail.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _inventoryScroll.verticalScrollbar = scrollbar;
        _inventoryScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        _inventoryScroll.onValueChanged.AddListener(position => RefreshInventoryList(false));
    }

    /// <summary>첫 표시 행을 계산하고 <paramref name="refresh"/>면 카드의 보유 정보와 목록 높이도 갱신한다.</summary>
    private void RefreshInventoryList(bool refresh)
    {
        Vector2 size = _inventoryViewport.rect.size;
        float width = (size.x - (InventoryColumns + 1) * InventoryGap) / InventoryColumns;
        if (width <= 0f || size.y <= 0f)
        {
            return;
        }
        int count = _field.Board.Cards.Count;
        int rows = Mathf.CeilToInt((float)count / InventoryColumns);
        float height = width / CardAspect;
        float step = height + InventoryGap;
        bool layoutChanged = size != _inventorySize;
        if (refresh || layoutChanged)
        {
            _inventorySize = size;
            _inventoryTitle.text = $"보유 카드 {count}장";
            float contentHeight = Mathf.Max(size.y, rows * step + InventoryGap);
            _inventoryContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
            float bottom = contentHeight - size.y;
            float offset = Mathf.Clamp(_inventoryContent.anchoredPosition.y, 0f, bottom);
            if (_revealNewestCard)
            {
                _inventoryScroll.StopMovement();
                offset = bottom;
                _revealNewestCard = false;
            }
            _inventoryContent.anchoredPosition = new Vector2(0f, offset);
        }

        int poolRows = Mathf.Min(rows, Mathf.CeilToInt(size.y / step) + 1);
        int first = Mathf.FloorToInt((_inventoryContent.anchoredPosition.y - InventoryGap) / step);
        first = Mathf.Clamp(first, 0, rows - poolRows);
        if (!refresh && !layoutChanged && first == _firstInventoryRow)
        {
            return;
        }
        _firstInventoryRow = first;
        int poolSize = Mathf.Min(count, poolRows * InventoryColumns);
        while (_ownedSlots.Count < poolSize)
        {
            _ownedSlots.Add(BuildOwnedSlot(_ownedSlots.Count));
        }
        for (int i = poolSize; i < _ownedSlots.Count; i++)
        {
            _ownedSlots[i].Rect.gameObject.SetActive(false);
        }
        // 줄 번호에 따라 슬롯을 순환시켜 한 줄 스크롤할 때 화면에 남은 배우와 애니메이션을 유지한다.
        int firstIndex = first * InventoryColumns;
        for (int i = 0; i < poolSize; i++)
        {
            int index = firstIndex + i;
            OwnedSlot slot = _ownedSlots[index % poolSize];
            slot.Rect.gameObject.SetActive(index < count);
            if (index < count)
            {
                LobbyCard owned = _field.Board.Cards[index];
                if (refresh || layoutChanged || slot.Card != owned)
                {
                    BindInventorySlot(slot, owned, index, width);
                }
            }
        }
    }

    /// <summary><paramref name="slot"/>을 <paramref name="index"/> 위치에 <paramref name="width"/> 너비로 배치하고 <paramref name="owned"/>의 보유 정보를 표시한다.</summary>
    private void BindInventorySlot(OwnedSlot slot, LobbyCard owned, int index, float width)
    {
        int column = index % InventoryColumns;
        int row = index / InventoryColumns;
        float height = width / CardAspect;
        slot.Card = owned;
        slot.Rect.sizeDelta = new Vector2(width, height);
        slot.Rect.anchoredPosition = new Vector2(InventoryGap + column * (width + InventoryGap), -InventoryGap - row * (height + InventoryGap));
        BindOwnedArt(slot.Art, owned);
        bool inspecting = owned == _detailCard;
        slot.Visibility.alpha = inspecting ? 0f : 1f;
        if (inspecting)
        {
            _inspectedSlot = slot;
        }
    }

    /// <summary><paramref name="index"/>번째 재사용 슬롯의 입력을 현재 연결된 보유 카드에 전달한다.</summary>
    private OwnedSlot BuildOwnedSlot(int index)
    {
        OwnedSlot slot = new OwnedSlot();
        slot.Rect = Panel("Unit" + index, _inventoryContent, 0, 1, 0, 1, Color.clear);
        slot.Rect.pivot = new Vector2(0, 1);
        slot.Visibility = slot.Rect.gameObject.AddComponent<CanvasGroup>();
        slot.Inspect = slot.Rect.gameObject.AddComponent<Button>();
        slot.Inspect.targetGraphic = slot.Rect.GetComponent<Image>();
        slot.Inspect.onClick.AddListener(() => InspectCard(slot));
        RectTransform face = FitCard("Face", slot.Rect);
        slot.Art = BuildOwnedArt(face, false);
        CardIcon(Box("InspectIcon", face, .805f, .834f, .95f, .943f), 2);
        return slot;
    }

    /// <summary><paramref name="parent"/>에 <paramref name="name"/>의 세로형 카드를 중앙 정렬하고 원화 비율을 유지한다.</summary>
    private static RectTransform FitCard(string name, RectTransform parent)
    {
        RectTransform face = Box(name, parent, 0, 0, 1, 1);
        AspectRatioFitter fit = face.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectRatio = CardAspect;
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        return face;
    }

    /// <summary><paramref name="face"/>에 공용 원화와 캐릭터 창을 배치하며 <paramref name="large"/>면 상세용 글자 크기를 사용한다.</summary>
    private InventoryArt BuildOwnedArt(RectTransform face, bool large)
    {
        RawImage frame = face.gameObject.AddComponent<RawImage>();
        frame.texture = _cardFrame;
        frame.raycastTarget = false;
        if (_cardFrame == null)
        {
            frame.color = Surface;
        }
        RectTransform window = Box("PortraitWindow", face, .09f, .39f, .91f, .90f);
        RectTransform square = Box("Character", window, 0, 0, 1, 1);
        AspectRatioFitter fit = square.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectRatio = 1f;
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        InventoryArt art = new InventoryArt();
        art.Portrait = square.gameObject.AddComponent<LobbyCardPortrait>();
        RectTransform grade = Box("Grade", face, .052f, .844f, .427f, .938f);
        for (int i = 0; i < art.Stars.Length; i++)
        {
            RectTransform star = Box("Star" + (i + 1), grade, i / 3f, 0, (i + 1) / 3f, 1);
            art.Stars[i] = CardIcon(star, 0);
        }
        art.Name = Label(face, "", .075f, .246f, .925f, .354f, large ? 60 : 30);
        art.Name.fontStyle = FontStyles.Bold;
        art.Stats = Label(face, "", .06f, .14f, .94f, .238f, large ? 42 : 22);
        art.Action = Label(face, "", .08f, .045f, .92f, .132f, large ? 36 : 20);
        return art;
    }

    /// <summary><paramref name="art"/>를 <paramref name="owned"/>에 연결해 카드에 저장된 등급, 능력치와 배치 상태를 표시한다.</summary>
    private void BindOwnedArt(InventoryArt art, LobbyCard owned)
    {
        art.Portrait.Bind(owned.Unit, _field);
        art.Name.text = owned.Unit.Name;
        art.Stats.text = owned.Stats;
        int starCount = owned.StarCount;
        Rect starRegion = IconRegion(owned.HasGoldStars ? 1 : 0);
        for (int i = 0; i < art.Stars.Length; i++)
        {
            art.Stars[i].uvRect = starRegion;
            art.Stars[i].gameObject.SetActive(i < starCount);
        }
        art.Action.text = owned.Cell < 0 ? "미배치" : "배치 중";
        art.Action.color = owned.Cell < 0 ? Gold : new Color32(160, 206, 200, 255);
    }

    /// <summary><paramref name="rect"/>에 아틀라스의 <paramref name="index"/>번째 아이콘을 표시하고 입력은 카드에 전달한다.</summary>
    private RawImage CardIcon(RectTransform rect, int index)
    {
        RawImage icon = rect.gameObject.AddComponent<RawImage>();
        icon.texture = _cardIcons;
        icon.uvRect = IconRegion(index);
        icon.raycastTarget = false;
        icon.enabled = _cardIcons != null;
        return icon;
    }

    /// <summary>은별·금별·돋보기가 나란한 아틀라스의 <paramref name="index"/>번째 영역을 반환한다.</summary>
    private static Rect IconRegion(int index) => new Rect(index / 3f, 0, 1f / 3f, 1);

    /// <summary><paramref name="parent"/>의 <paramref name="area"/>에 <paramref name="name"/> 버튼을 만들고 <paramref name="caption"/>을 <paramref name="size"/> 크기로 <paramref name="color"/> 바탕에 표시한다.</summary>
    private Button CardButton(Transform parent, string name, string caption, Rect area, int size, Color color)
    {
        RectTransform rect = Panel(name, parent, area.xMin, area.yMin, area.xMax, area.yMax, color);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        Label(rect, caption, .04f, .04f, .96f, .96f, size);
        return button;
    }

    /// <summary>인벤토리 위에 한 장의 그림과 특수 동작을 보여주는 상세 화면을 준비한다.</summary>
    private void BuildCardDetail()
    {
        RectTransform overlay = Panel("CardDetail", _root, 0, 0, 1, 1, Color.clear);
        _detailPanel = overlay.gameObject;
        _detailShade = overlay.GetComponent<Image>();
        _detailBounds = Box("DetailCard", overlay, .13f, .18f, .87f, .87f);
        _detailArt = BuildOwnedArt(FitCard("Face", _detailBounds), true);
        RectTransform controls = Box("Controls", overlay, 0, 0, 1, 1);
        _detailControls = controls.gameObject.AddComponent<CanvasGroup>();
        RectTransform closeArea = Panel("Close", controls, .83f, .865f, .96f, .968f, Color.clear);
        Image closeIcon = Box("Icon", closeArea, .23f, .18f, .77f, .82f).gameObject.AddComponent<Image>();
        closeIcon.sprite = AssetManager.Instance.LoadAsset<Sprite>("LobbyCardBack");
        closeIcon.preserveAspect = true;
        closeIcon.raycastTarget = false;
        _detailClose = closeArea.gameObject.AddComponent<Button>();
        _detailClose.targetGraphic = closeIcon;
        _detailClose.onClick.AddListener(() => CloseCardDetail(false));
        Button replay = CardButton(controls, "Replay", "다시 재생", new Rect(.13f, .065f, .34f, .085f), 31, Surface);
        replay.onClick.AddListener(_detailArt.Portrait.PlaySpecial);
        _detailPlace = CardButton(controls, "Place", "배치하기", new Rect(.51f, .065f, .36f, .085f), 31, new Color32(31, 111, 129, 255));
        _detailPlace.onClick.AddListener(() => CloseCardDetail(true));
        _detailPanel.SetActive(false);
    }

    /// <summary>목록의 <paramref name="slot"/>을 누른 자리에서 같은 보유 카드의 상세 팝업을 확대한다.</summary>
    private void InspectCard(OwnedSlot slot)
    {
        if (_detailPanel.activeSelf)
        {
            return;
        }
        _inventoryScroll.StopMovement();
        _inspectedSlot = slot;
        _detailCard = slot.Card;
        slot.Visibility.alpha = 0f;
        CanvasGroup inventory = _pages[1].GetComponent<CanvasGroup>();
        inventory.interactable = false;
        inventory.blocksRaycasts = false;
        _tabInput.interactable = false;
        _detailPanel.SetActive(true);
        BindDetailState();
        _detailProgress = 0f;
        _detailClosing = false;
        _placeOnClose = false;
        _detailControls.interactable = false;
        ApplyDetailZoom(0f);
        if (!_motion)
        {
            UpdateCardDetail();
        }
    }

    /// <summary>상세 카드와 보유 상태를 동기화하고 확대·축소가 끝난 시점에 입력 또는 배치 동작을 연결한다.</summary>
    private void UpdateCardDetail()
    {
        if (_detailCard.Cell != _detailCell)
        {
            BindDetailState();
        }
        float target = _detailClosing ? 0f : 1f;
        if (!_detailClosing && _detailProgress == 1f)
        {
            return;
        }
        _detailProgress = _motion ? Mathf.MoveTowards(_detailProgress, target, Time.unscaledDeltaTime / DetailZoomSeconds) : target;
        ApplyDetailZoom(Mathf.SmoothStep(0f, 1f, _detailProgress));
        if (_detailProgress != target)
        {
            return;
        }
        if (_detailClosing)
        {
            LobbyCard selected = _detailCard;
            bool place = _placeOnClose;
            HideCardDetail();
            if (place)
            {
                Place(selected);
            }
            return;
        }
        _detailControls.interactable = true;
        _detailClose.Select();
        _detailArt.Portrait.PlaySpecial();
    }

    /// <summary><paramref name="amount"/>에 맞춰 목록 카드의 화면상 중심과 너비에서 상세 크기까지 보간한다.</summary>
    private void ApplyDetailZoom(float amount)
    {
        RectTransform overlay = _detailShade.rectTransform;
        RectTransform source = _inspectedSlot.Rect;
        Vector2 sourceCenter = overlay.InverseTransformPoint(source.TransformPoint(source.rect.center));
        Vector2 centerAnchor = (_detailBounds.anchorMin + _detailBounds.anchorMax) * .5f;
        Vector2 endCenter = overlay.rect.min + Vector2.Scale(overlay.rect.size, centerAnchor);
        float sourceWidth = overlay.InverseTransformVector(source.TransformVector(Vector3.right * source.rect.width)).magnitude;
        float endWidth = Mathf.Min(_detailBounds.rect.width, _detailBounds.rect.height * CardAspect);
        float scale = endWidth > 0f ? sourceWidth / endWidth : 1f;
        _detailBounds.anchoredPosition = Vector2.Lerp(sourceCenter - endCenter, Vector2.zero, amount);
        _detailBounds.localScale = Vector3.one * Mathf.Lerp(scale, 1f, amount);
        _detailShade.color = new Color(.035f, .075f, .105f, .78f * amount);
        _detailControls.alpha = Mathf.InverseLerp(.46f, 1f, amount);
    }

    /// <summary>상세 화면이 가리키는 보유 카드의 현재 배치 상태를 반영한다.</summary>
    private void BindDetailState()
    {
        _detailCell = _detailCard.Cell;
        BindOwnedArt(_detailArt, _detailCard);
        _detailPlace.interactable = _detailCell < 0;
    }

    /// <summary>카드를 원래 자리로 축소한 뒤 <paramref name="place"/>가 참이면 전장 배치로 연결한다.</summary>
    private void CloseCardDetail(bool place)
    {
        _placeOnClose = place;
        _detailClosing = true;
        _detailControls.interactable = false;
        if (!_motion)
        {
            UpdateCardDetail();
        }
    }

    /// <summary>팝업을 닫고 같은 스크롤 위치의 카드와 목록 입력을 복원한다.</summary>
    private void HideCardDetail()
    {
        _detailPanel.SetActive(false);
        _detailCard = null;
        _inspectedSlot.Visibility.alpha = 1f;
        CanvasGroup inventory = _pages[1].GetComponent<CanvasGroup>();
        inventory.interactable = true;
        inventory.blocksRaycasts = true;
        _tabInput.interactable = true;
        _inspectedSlot.Inspect.Select();
    }
}
