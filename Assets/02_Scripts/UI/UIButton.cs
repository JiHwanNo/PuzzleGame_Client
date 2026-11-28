using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유니티 버튼 컴포넌트를 기반으로 특정 객체(root)에 메시지를 전달하는 UI 클래스입니다.
/// 버튼 클릭 시 설정된 콜백 메서드를 실행합니다.
/// </summary>
public class UIButton : MonoBehaviour
{
    /// <summary> 클릭 시 줄어드는 스케일 비율입니다. </summary>
    private const float ClickScale = 0.9f;

    /// <summary> 클릭 트윈의 눌림 시간입니다. </summary>
    private const float ClickDownDuration = 0.06f;

    /// <summary> 클릭 트윈의 복귀 시간입니다. </summary>
    private const float ClickUpDuration = 0.12f;

    /// <summary> 유니티 버튼 컴포넌트 </summary>
    [SerializeField] 
    private Button _unityButton;

    /// <summary> 버튼 뷰 스프라이트를 표시할 이미지 </summary>
    [SerializeField]
    private Image _buttonImage;

    [Space(10f)]

    /// <summary> 메시지를 수신할 대상 스크립트 </summary>
    [SerializeField] 
    private MonoBehaviour _root;

    /// <summary> 실행할 콜백 메서드의 이름 </summary>
    [SerializeField] 
    private string _callbackName;

    /// <summary> 콜백 메서드에 전달할 인자값 (선택 사항) </summary>
    [SerializeField] 
    private string _callbackValue;

    /// <summary> 현재 실행 중인 클릭 트윈 시퀀스 </summary>
    private Sequence _clickSequence;

    /// <summary> 클릭 트윈 시작 기준 스케일 </summary>
    private Vector3 _originScale;

    /// <summary> 기준 스케일 초기화 여부 </summary>
    private bool _hasOriginScale;

    /// <summary>
    /// 버튼 클릭 이벤트가 발생했을 때 호출되어 root 객체에 메시지를 전송합니다.
    /// </summary>
    public void OnClickEvent()
    {
        PlayClickTween();

        if (_root == null || _unityButton == null || string.IsNullOrEmpty(_callbackName))
        {
            Debug.LogError($"UIButton_{gameObject.name}: Root, Unity Button, or Callback Name is not set.");
            return;
        }

        // 인자값 유무에 따라 SendMessage 호출 분기 (로직 보존)
        if (!string.IsNullOrEmpty(_callbackValue))
        {
            _root.SendMessage(_callbackName, _callbackValue);
        }
        else
        {
            _root.SendMessage(_callbackName);
        }
    }

    /// <summary>
    /// 오브젝트 파괴 시 클릭 트윈을 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (_clickSequence != null)
        {
            _clickSequence.Kill();
            _clickSequence = null;
        }
    }

    /// <summary>
    /// 버튼 이미지를 전달받은 스프라이트로 변경합니다.
    /// </summary>
    /// <param name="sr">변경할 스프라이트입니다.</param>
    public void SetSprite(Sprite sr)
    {
        if (_buttonImage == null || sr == null)
        {
            return;
        }

        _buttonImage.sprite = sr;
    }

    /// <summary>
    /// 코드로 생성한 버튼의 콜백 대상/메서드/인자를 설정합니다.
    /// </summary>
    /// <param name="root">메시지를 수신할 대상 스크립트입니다.</param>
    /// <param name="callbackName">실행할 콜백 메서드 이름입니다.</param>
    /// <param name="callbackValue">콜백에 전달할 인자값입니다.</param>
    public void SetCallback(MonoBehaviour root, string callbackName, string callbackValue)
    {
        _root = root;
        _callbackName = callbackName;
        _callbackValue = callbackValue;
    }

    /// <summary>
    /// 버튼 이미지를 아이콘 스프라이트로 설정합니다(원본 비율 유지, Simple 타입).
    /// </summary>
    /// <param name="sr">표시할 아이콘 스프라이트입니다.</param>
    public void SetIconSprite(Sprite sr)
    {
        if (_buttonImage == null || sr == null)
        {
            return;
        }

        _buttonImage.sprite = sr;
        _buttonImage.type = Image.Type.Simple;
        _buttonImage.preserveAspect = true;
    }

    /// <summary>
    /// 버튼 클릭 피드백 트윈을 재생합니다.
    /// </summary>
    private void PlayClickTween()
    {
        if (!_hasOriginScale)
        {
            _originScale = transform.localScale;
            _hasOriginScale = true;
        }

        if (_clickSequence != null)
        {
            _clickSequence.Kill();
        }

        transform.localScale = _originScale;
        _clickSequence = DOTween.Sequence();
        _clickSequence.Append(transform.DOScale(_originScale * ClickScale, ClickDownDuration).SetEase(Ease.OutQuad));
        _clickSequence.Append(transform.DOScale(_originScale, ClickUpDuration).SetEase(Ease.OutBack));
        _clickSequence.OnKill(() => _clickSequence = null);
    }
}

/// <summary>라군 UI 패널과 배지에 같은 둥근 모서리와 윗면의 빛을 그린다.</summary>
internal static class LagoonUI
{
    private static readonly System.Collections.Generic.Dictionary<Color32, Sprite> _surfaces =
        new System.Collections.Generic.Dictionary<Color32, Sprite>();

    /// <summary><paramref name="image"/>에 모서리가 늘어나지 않는 젤리 표면과 <paramref name="tint"/> 색을 적용한다.</summary>
    internal static void Surface(Image image, Color tint)
    {
        Color32 color = tint;
        if (!_surfaces.TryGetValue(color, out Sprite surface) || surface == null)
        {
            surface = CreateSurfaceSprite(color);
            _surfaces[color] = surface;
        }
        image.sprite = surface;
        image.type = Image.Type.Sliced;
        Canvas canvas = image.GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            image.pixelsPerUnitMultiplier = canvas.referencePixelsPerUnit / surface.pixelsPerUnit;
        }
        image.color = Color.white;
    }

    /// <summary><paramref name="anchor"/> 바로 뒤에 <paramref name="name"/> 배경을 만들고 <paramref name="tint"/> 색과 양쪽 <paramref name="padding"/> 여백을 적용한다.</summary>
    internal static Image Backplate(RectTransform anchor, string name, Color tint, Vector2 padding)
    {
        GameObject plate = new GameObject(name, typeof(RectTransform), typeof(Image));
        plate.layer = anchor.gameObject.layer;
        RectTransform rect = (RectTransform)plate.transform;
        rect.SetParent(anchor.parent, false);
        rect.SetSiblingIndex(anchor.GetSiblingIndex());
        rect.anchorMin = anchor.anchorMin;
        rect.anchorMax = anchor.anchorMax;
        rect.pivot = anchor.pivot;
        rect.offsetMin = anchor.offsetMin - padding;
        rect.offsetMax = anchor.offsetMax + padding;
        rect.localRotation = anchor.localRotation;
        rect.localScale = anchor.localScale;
        Image background = plate.GetComponent<Image>();
        background.raycastTarget = false;
        Surface(background, tint);
        return background;
    }

    /// <summary><paramref name="tint"/>마다 한 번만 9분할 표면을 구워 밝은 테두리와 반사광의 색을 보존한다.</summary>
    private static Sprite CreateSurfaceSprite(Color tint)
    {
        const int size = 64;
        const float radius = 18f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Lagoon Panel";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color lowerFace = Color.Lerp(tint, new Color(0.02f, 0.19f, 0.24f), 0.13f);
        Color upperFace = Color.Lerp(tint, Color.white, 0.14f);
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 모서리 원호와의 거리로 윤곽을 부드럽게 하고, 반사광을 왼쪽 위 고정 영역 안에 담는다.
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float inset = radius - Mathf.Sqrt(dx * dx + dy * dy);
                float height = y / (size - 1f);
                Color face = Color.Lerp(lowerFace, upperFace, height);
                Color rim = Color.Lerp(lowerFace * 0.70f, Color.Lerp(tint, Color.white, 0.56f), height);
                Color pixel = Color.Lerp(rim, face, Mathf.Clamp01((inset - 1f) / 3f));
                float glintX = (x - 12f) / 5.2f;
                float glintY = (y - 55f - (x - 12f) * 0.42f) / 1.9f;
                float glint = Mathf.Clamp01(1.5f - glintX * glintX - glintY * glintY);
                pixel = Color.Lerp(pixel, Color.white, 0.46f * glint);
                pixel.a = tint.a * Mathf.Clamp01(inset + 0.5f);
                pixels[y * size + x] = pixel;
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
        sprite.name = texture.name;
        return sprite;
    }
}
