using TMPro;
using UnityEngine;
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
        /// <summary> 게이지 비율만큼 가로로 늘어나는 채움 영역입니다. </summary>
        private readonly RectTransform _fill;
        /// <summary> 상태에 따라 색상을 변경하는 채움 이미지입니다. </summary>
        private readonly Image _fillImage;
        /// <summary> 표시할 피벗 상태입니다. </summary>
        private readonly PivotMode _mode;
        /// <summary> 마지막으로 표시한 0.1점 단위 게이지입니다. </summary>
        private int _lastDisplayedTenths = -1;
        /// <summary> 마지막으로 표시한 활성 상태입니다. </summary>
        private bool _lastActive;

        /// <summary> 타이머 아래에 피벗 상태와 게이지 표시를 생성합니다. </summary>
        /// <param name="template">배치와 한글 폰트를 참조할 기존 타이머 텍스트</param>
        /// <param name="mode">표시할 피벗 상태</param>
        public PivotGaugeView(TMP_Text template, PivotMode mode)
        {
            _mode = mode;
            RectTransform source = template.rectTransform;
            RectTransform root = CreateRect("PivotGauge", source.parent);
            _root = root.gameObject;
            root.anchorMin = source.anchorMin;
            root.anchorMax = source.anchorMax;
            root.pivot = source.pivot;
            root.anchoredPosition = source.anchoredPosition + new Vector2(0f, -60f);
            root.sizeDelta = new Vector2(source.sizeDelta.x, 55f);

            RectTransform labelRect = CreateRect("State", root);
            labelRect.anchorMin = new Vector2(0f, 0.4f);
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            _label.font = template.font;
            _label.fontSharedMaterial = template.fontSharedMaterial;
            _label.fontSize = 24f;
            _label.alignment = TextAlignmentOptions.Left;
            _label.raycastTarget = false;

            RectTransform track = CreateRect("Track", root);
            track.anchorMin = Vector2.zero;
            track.anchorMax = new Vector2(1f, 0.25f);
            track.offsetMin = Vector2.zero;
            track.offsetMax = Vector2.zero;
            Image background = track.gameObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);
            background.raycastTarget = false;

            _fill = CreateRect("Fill", track);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
            _fillImage = _fill.gameObject.AddComponent<Image>();
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

            float ratio = _mode.GaugePoints / _mode.Capacity;
            _fill.anchorMax = new Vector2(ratio, 1f);
            int displayedTenths = _mode.GaugeUnits / 10;
            if (displayedTenths != _lastDisplayedTenths || _mode.IsActive != _lastActive)
            {
                _lastDisplayedTenths = displayedTenths;
                _lastActive = _mode.IsActive;
                string state = _mode.IsActive ? "피벗 모드" : "피벗 충전";
                _label.text = $"{state} · {displayedTenths / 10f:0.0} / {_mode.Capacity}";
                _fillImage.color = _mode.IsActive ? new Color(1f, 0.65f, 0.15f) : new Color(0.2f, 0.75f, 1f);
            }
        }

        /// <summary> 이 표시 객체가 생성한 UI를 제거합니다. </summary>
        public void Dispose()
        {
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
