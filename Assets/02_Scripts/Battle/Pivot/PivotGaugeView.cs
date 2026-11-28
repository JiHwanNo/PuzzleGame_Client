using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

namespace Battle.Pivot
{
    /// <summary> 기존 게임 UI의 폰트를 사용해 피벗 상태와 게이지를 표시합니다. </summary>
    public sealed class PivotGaugeView
    {
        /// <summary> 이 표시 객체가 소유하는 UI 루트입니다. </summary>
        private readonly GameObject _root;
        /// <summary> 충전 또는 활성 상태를 표시하는 텍스트입니다. </summary>
        private readonly TextMeshProUGUI _label;
        private readonly TextMeshProUGUI _activationLabel;
        private Sequence _announcement;
        /// <summary> 상태에 따라 색상을 변경하는 채움 이미지입니다. </summary>
        private readonly Image _fillImage;
        /// <summary> 표시할 피벗 상태입니다. </summary>
        private readonly PivotMode _mode;
        /// <summary> 마지막으로 표시한 게이지 백분율입니다. </summary>
        private int _lastDisplayedPercent = -1;
        /// <summary> 마지막으로 표시한 활성 상태입니다. </summary>
        private bool _lastActive;

        /// <summary> 타이머의 배치와 폰트로 피벗 상태와 충전량을 표시합니다. </summary>
        public PivotGaugeView(TMP_Text template, PivotMode mode)
        {
            _mode = mode;
            RectTransform source = template.rectTransform;
            RectTransform root = CreateRect("PivotGauge", source.parent);
            _root = root.gameObject;
            root.anchorMin = source.anchorMin;
            root.anchorMax = source.anchorMax;
            root.pivot = source.pivot;
            root.anchoredPosition = source.anchoredPosition + new Vector2(0f, -source.rect.height - 8f);
            root.sizeDelta = new Vector2(340f, 60f);

            RectTransform labelRect = CreateRect("State", root);
            labelRect.anchorMin = new Vector2(0f, 0.57f);
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            _label.font = template.font;
            _label.fontSharedMaterial = template.fontSharedMaterial;
            _label.fontSize = 22f;
            _label.color = new Color32(216, 255, 246, 255);
            _label.alignment = TextAlignmentOptions.Left;
            _label.raycastTarget = false;

            RectTransform announcement = CreateRect("PivotActivation", root);
            announcement.anchorMin = Vector2.zero;
            announcement.anchorMax = Vector2.zero;
            announcement.pivot = new Vector2(0f, 1f);
            announcement.anchoredPosition = new Vector2(0f, -6f);
            announcement.sizeDelta = new Vector2(root.sizeDelta.x, 48f);
            _activationLabel = announcement.gameObject.AddComponent<TextMeshProUGUI>();
            _activationLabel.font = template.font;
            _activationLabel.fontSharedMaterial = template.fontSharedMaterial;
            _activationLabel.fontSize = 34f;
            _activationLabel.alignment = TextAlignmentOptions.Left;
            _activationLabel.color = new Color32(255, 221, 112, 255);
            _activationLabel.text = "피벗 발동!";
            _activationLabel.raycastTarget = false;
            _activationLabel.gameObject.SetActive(false);

            RectTransform track = CreateRect("Track", root);
            track.anchorMin = Vector2.zero;
            track.anchorMax = Vector2.zero;
            track.pivot = Vector2.zero;
            track.sizeDelta = new Vector2(264f, 34f);
            Image background = track.gameObject.AddComponent<Image>();
            background.sprite = AssetManager.Instance.LoadAsset<Sprite>("PivotGaugeFrame");
            background.raycastTarget = false;

            // 별 장식과 금색 테두리 안쪽에만 충전량을 표시합니다.
            RectTransform fill = CreateRect("Fill", track);
            fill.anchorMin = new Vector2(0.12f, 0.22f);
            fill.anchorMax = new Vector2(0.97f, 0.79f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            _fillImage = fill.gameObject.AddComponent<Image>();
            _fillImage.sprite = AssetManager.Instance.LoadAsset<Sprite>("PivotGaugeFill");
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fillImage.raycastTarget = false;
            Refresh();
        }

        /// <summary> 피벗 게이지와 활성 표시를 갱신합니다. 게임 종료 시 표시를 숨깁니다. </summary>
        public void Refresh()
        {
            if (_root == null)
            {
                return;
            }
            bool visible = _mode.IsEnabled && !_mode.IsStopped;
            _root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            float ratio = _mode.GaugeRatio;
            _fillImage.fillAmount = ratio;
            int percent = _mode.GaugePercent;
            if (percent != _lastDisplayedPercent || _mode.IsActive != _lastActive)
            {
                _lastDisplayedPercent = percent;
                if (_mode.IsActive && !_lastActive)
                {
                    PlayActivationLabel();
                }
                _lastActive = _mode.IsActive;
                string state = _mode.IsActive ? "피벗 모드" : "피벗 충전";
                _label.text = $"{state} · {percent}%";
                _fillImage.color = _mode.IsActive ? new Color32(255, 177, 61, 255) : new Color32(81, 238, 220, 255);
            }
        }

        /// <summary>게이지 아래에 발동 문구를 한 번 띄우고 잠시 유지한 뒤 지웁니다.</summary>
        private void PlayActivationLabel()
        {
            if (_announcement != null)
            {
                _announcement.Kill();
            }
            _activationLabel.gameObject.SetActive(true);
            _activationLabel.alpha = 1f;
            _activationLabel.rectTransform.localScale = Vector3.one * 0.75f;
            _announcement = DOTween.Sequence();
            _announcement.Append(_activationLabel.rectTransform.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
            _announcement.AppendInterval(0.55f);
            _announcement.Append(_activationLabel.DOFade(0f, 0.25f));
            _announcement.OnComplete(() => _activationLabel.gameObject.SetActive(false));
        }

        /// <summary> 이 표시 객체가 생성한 UI를 제거합니다. </summary>
        public void Dispose()
        {
            if (_announcement != null)
            {
                _announcement.Kill();
            }
            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        /// <summary> 지정한 부모 아래에 UI 사각 영역을 생성합니다. </summary>
        /// <param name="name">생성할 객체 이름</param>
        /// <param name="parent">배치할 부모 변환</param>
        /// <returns>생성한 UI 사각 영역</returns>
        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)created.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
