using UnityEngine;
using UnityEngine.UI;

/// <summary>보유 카드와 상세 화면의 사각 프레임 안에서 전장 캐릭터의 대기·특수 동작을 재생한다.</summary>
[RequireComponent(typeof(RectTransform))]
internal sealed class LobbyCardPortrait : MonoBehaviour
{
    private const float SpriteSize = 82f;
    private const float SpecialDuration = 0.72f;
    private static readonly int _idleState = Animator.StringToHash("Base Layer.Idle_SE");
    private static readonly int _specialState = Animator.StringToHash("Base Layer.Special_SE");

    private LobbyUnit _unit;
    private RectTransform _actor;
    private Animator _animator;
    private int _pendingState;
    private RectTransform _sprite;
    private RawImage _rabbit;
    private Rect _idleRegion;
    private Image _gem;
    private Color _gemColor;
    private float _breathTime;
    private float _specialLeft;

    /// <summary>초상 프레임에 남아 있는 정지 이미지가 배우 뒤에 그려지지 않도록 숨긴다.</summary>
    private void Awake()
    {
        if (TryGetComponent(out RawImage background))
        {
            background.enabled = false;
        }
    }

    /// <summary><paramref name="unit"/>의 전장 배우를 표시하고 시트 초상은 <paramref name="field"/>에서 가져온다.</summary>
    internal void Bind(LobbyUnit unit, LobbyDefenseView field)
    {
        if (_unit == unit)
        {
            return;
        }
        if (_actor != null)
        {
            _actor.gameObject.SetActive(false);
            Destroy(_actor.gameObject);
        }
        _actor = null;
        _animator = null;
        _sprite = null;
        _rabbit = null;
        _gem = null;
        _breathTime = 0f;
        _specialLeft = 0f;
        _pendingState = 0;
        _unit = unit;
        if (unit == null)
        {
            return;
        }

        if (unit.PrefabPath != null)
        {
            GameObject prefab = AssetManager.Instance.LoadResource<GameObject>(unit.PrefabPath);
            if (prefab != null)
            {
                _actor = (RectTransform)Instantiate(prefab, transform, false).transform;
                _animator = _actor.GetComponent<Animator>();
                _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                _animator.keepAnimatorStateOnDisable = true;
                _pendingState = _idleState;
            }
        }
        if (_actor == null)
        {
            CreateSprite(field);
        }
        CanvasGroup group = _actor.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        _actor.gameObject.SetActive(enabled);
        FitActor();
        ApplyPendingState();
    }

    /// <summary><paramref name="field"/>에서 초상을 받아 토끼의 시트 또는 전장과 같은 보석을 만든다.</summary>
    private void CreateSprite(LobbyDefenseView field)
    {
        _actor = CreateSquare("Portrait", transform, SpriteSize);
        if (_unit == LobbyUnit.Trap)
        {
            _sprite = CreateSquare("Gem", _actor, 45f);
            LobbyDefenseView.DrawTrap(_sprite);
            _gem = _sprite.GetComponent<Image>();
            _gemColor = _gem.color;
            return;
        }
        _sprite = CreateSquare("Art", _actor, SpriteSize);
        RawImage icon = _sprite.gameObject.AddComponent<RawImage>();
        icon.raycastTarget = false;
        field.SetPortrait(icon, _unit);
        if (_unit == LobbyUnit.Rabbit)
        {
            _rabbit = icon;
            _idleRegion = icon.uvRect;
        }
    }

    /// <summary><paramref name="parent"/> 중앙에 한 변이 <paramref name="size"/>인 <paramref name="name"/> 사각형을 만든다.</summary>
    private static RectTransform CreateSquare(string name, Transform parent, float size)
    {
        RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(size, size);
        return rect;
    }

    /// <summary>선택한 카드의 특수 동작을 처음부터 한 번 재생한다.</summary>
    internal void PlaySpecial()
    {
        if (_animator != null)
        {
            _pendingState = _specialState;
            ApplyPendingState();
            return;
        }
        _specialLeft = SpecialDuration;
        if (_rabbit != null)
        {
            Rect region = _idleRegion;
            region.y = 0f;
            _rabbit.uvRect = region;
        }
    }

    /// <summary>숨겨진 동안 요청된 동작은 배우가 활성화된 뒤 첫 포즈부터 적용한다.</summary>
    private void ApplyPendingState()
    {
        if (_pendingState == 0 || !_animator.isActiveAndEnabled)
        {
            return;
        }
        _animator.Play(_pendingState, 0, 0f);
        _animator.Update(0f);
        _pendingState = 0;
    }

    /// <summary>가려졌던 배우와 보류된 동작을 다시 표시한다.</summary>
    private void OnEnable()
    {
        if (_actor != null && !_actor.gameObject.activeSelf)
        {
            _actor.gameObject.SetActive(true);
        }
        ApplyPendingState();
    }

    /// <summary>비활성 프레임의 프리팹 애니메이터를 함께 멈춘다.</summary>
    private void OnDisable()
    {
        if (_actor != null && gameObject.activeInHierarchy)
        {
            _actor.gameObject.SetActive(false);
        }
    }

    /// <summary>카드 레이아웃이 바뀌면 배우의 원본 가로세로 비율로 다시 맞춘다.</summary>
    private void OnRectTransformDimensionsChange() => FitActor();

    /// <summary>프레임에 들어가는 최대 배율을 배우의 원본 사각형에 적용한다.</summary>
    private void FitActor()
    {
        if (_actor == null)
        {
            return;
        }
        Rect frame = ((RectTransform)transform).rect;
        Rect art = _actor.rect;
        float scale = Mathf.Min(frame.width / art.width, frame.height / art.height);
        _actor.localScale = Vector3.one * Mathf.Max(0f, scale);
    }

    /// <summary>활성 초상의 시트 호흡과 보석의 반짝임을 시간 배율과 독립적으로 재생한다.</summary>
    private void Update()
    {
        if (_pendingState != 0)
        {
            ApplyPendingState();
        }
        if (_sprite == null)
        {
            return;
        }
        _breathTime += Time.unscaledDeltaTime;
        float breath = Mathf.Sin(_breathTime * 3.5f);
        float pulse = 0f;
        if (_specialLeft > 0f)
        {
            _specialLeft = Mathf.Max(0f, _specialLeft - Time.unscaledDeltaTime);
            pulse = Mathf.Sin(Mathf.PI * _specialLeft / SpecialDuration);
            if (_specialLeft == 0f && _rabbit != null)
            {
                _rabbit.uvRect = _idleRegion;
            }
        }
        _sprite.anchoredPosition = new Vector2(0f, breath * 0.65f + pulse * 3f);
        _sprite.localScale = new Vector3(1f - breath * 0.012f + pulse * 0.07f, 1f + breath * 0.018f + pulse * 0.07f, 1f);
        if (_gem != null)
        {
            _gem.color = Color.Lerp(_gemColor, Color.white, (breath + 1f) * 0.09f + pulse * 0.55f);
        }
    }
}
