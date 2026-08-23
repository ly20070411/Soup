using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Floating name + description panel for relic bar hovers.
    /// Title and body are stacked with fixed spacing so they never overlap.
    /// </summary>
    public sealed class RelicHudTooltip : MonoBehaviour
    {
        private const float PanelWidth = 340f;
        private const float TitleHeight = 26f;
        private const float TitleBodyGap = 8f;
        private const float PadX = 14f;
        private const float PadTop = 12f;
        private const float PadBottom = 12f;

        private GameObject _root;
        private RectTransform _rect;
        private Text _title;
        private Text _body;
        private RectTransform _titleRect;
        private RectTransform _bodyRect;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        public void EnsureBuilt(Transform parent, Font font)
        {
            if (_root != null)
            {
                RebindParent(parent, font);
                return;
            }

            _root = new GameObject("RelicTooltip", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            _rect = _root.GetComponent<RectTransform>();
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0f);
            _rect.sizeDelta = new Vector2(PanelWidth, 120f);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.94f);
            bg.raycastTarget = false;

            var outline = _root.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.58f, 0.65f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Manual layout (no VerticalLayoutGroup) — title band then body below.
            _title = CreateText(_root.transform, "Title", font, 18, FontStyle.Bold, Color.white);
            _titleRect = _title.rectTransform;
            _titleRect.anchorMin = new Vector2(0f, 1f);
            _titleRect.anchorMax = new Vector2(1f, 1f);
            _titleRect.pivot = new Vector2(0.5f, 1f);
            _titleRect.anchoredPosition = new Vector2(0f, -PadTop);
            _titleRect.sizeDelta = new Vector2(-(PadX * 2f), TitleHeight);
            _title.alignment = TextAnchor.UpperLeft;
            _title.horizontalOverflow = HorizontalWrapMode.Wrap;
            _title.verticalOverflow = VerticalWrapMode.Truncate;

            _body = CreateText(
                _root.transform,
                "Body",
                font,
                16,
                FontStyle.Normal,
                new Color(0.85f, 0.88f, 0.92f, 1f));
            _bodyRect = _body.rectTransform;
            _bodyRect.anchorMin = new Vector2(0f, 0f);
            _bodyRect.anchorMax = new Vector2(1f, 1f);
            _bodyRect.pivot = new Vector2(0.5f, 1f);
            _body.alignment = TextAnchor.UpperLeft;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;

            _canvas = parent.GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : parent as RectTransform;

            var tipCanvas = _root.AddComponent<Canvas>();
            tipCanvas.overrideSorting = true;
            tipCanvas.sortingOrder = 5000;
            _root.AddComponent<GraphicRaycaster>().enabled = false;

            _root.SetActive(false);
        }

        /// <summary>Keep the floating panel above modal canvases (starter / unlock / visit).</summary>
        public void EnsureTopMost(int sortingOrder = 5000)
        {
            if (_root == null) return;
            var tipCanvas = _root.GetComponent<Canvas>();
            if (tipCanvas == null)
                tipCanvas = _root.AddComponent<Canvas>();
            tipCanvas.overrideSorting = true;
            tipCanvas.sortingOrder = sortingOrder;
            _root.transform.SetAsLastSibling();
        }

        /// <summary>Re-parent under a new canvas host (e.g. after scene / modal change).</summary>
        public void RebindParent(Transform parent, Font font)
        {
            if (parent == null) return;
            if (_root == null)
            {
                EnsureBuilt(parent, font);
                return;
            }

            _root.transform.SetParent(parent, false);
            _canvas = parent.GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : parent as RectTransform;
            EnsureTopMost();
        }

        public void Show(string title, string body, RectTransform anchor)
        {
            if (_root == null || _rect == null) return;

            ApplyContent(title, body);
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            Reflow();
            LayoutNear(anchor);
        }

        public void ShowAtScreen(string title, string body, Vector2 screenPos)
        {
            if (_root == null || _rect == null) return;

            ApplyContent(title, body);
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            Reflow();
            LayoutAtScreen(screenPos);
        }

        public void MoveToScreen(Vector2 screenPos)
        {
            if (_root == null || !_root.activeSelf) return;
            LayoutAtScreen(screenPos);
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        private void ApplyContent(string title, string body)
        {
            if (_title != null)
                _title.text = title ?? string.Empty;
            if (_body != null)
                _body.text = body ?? string.Empty;
        }

        private void Reflow()
        {
            // Force title width so preferredHeight wraps correctly.
            if (_titleRect != null)
                _titleRect.sizeDelta = new Vector2(-(PadX * 2f), TitleHeight);

            Canvas.ForceUpdateCanvases();

            float titleH = TitleHeight;
            if (_title != null && !string.IsNullOrEmpty(_title.text))
                titleH = Mathf.Max(TitleHeight, _title.preferredHeight);

            if (_titleRect != null)
                _titleRect.sizeDelta = new Vector2(-(PadX * 2f), titleH);

            float bodyTop = PadTop + titleH + TitleBodyGap;

            // Give body a known width before measuring preferred height.
            if (_bodyRect != null)
            {
                _bodyRect.anchorMin = new Vector2(0f, 1f);
                _bodyRect.anchorMax = new Vector2(1f, 1f);
                _bodyRect.pivot = new Vector2(0.5f, 1f);
                _bodyRect.anchoredPosition = new Vector2(0f, -bodyTop);
                _bodyRect.sizeDelta = new Vector2(-(PadX * 2f), 0f);
            }

            Canvas.ForceUpdateCanvases();

            float bodyH = 40f;
            if (_body != null && !string.IsNullOrEmpty(_body.text))
                bodyH = Mathf.Max(40f, _body.preferredHeight);

            if (_bodyRect != null)
                _bodyRect.sizeDelta = new Vector2(-(PadX * 2f), bodyH);

            float totalH = bodyTop + bodyH + PadBottom;
            _rect.sizeDelta = new Vector2(PanelWidth, Mathf.Clamp(totalH, 72f, 420f));
        }

        private void LayoutAtScreen(Vector2 screenPos)
        {
            if (_rect == null || _canvasRect == null) return;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPos,
                _canvas != null ? _canvas.worldCamera : null,
                out local);

            const float gap = 16f;
            float tipH = _rect.sizeDelta.y;
            float tipW = _rect.sizeDelta.x;
            float x = local.x;
            float y = local.y + gap;

            var bounds = _canvasRect.rect;
            x = Mathf.Clamp(x, bounds.xMin + tipW * 0.5f + 8f, bounds.xMax - tipW * 0.5f - 8f);
            if (y + tipH > bounds.yMax - 8f)
                y = local.y - gap - tipH;
            y = Mathf.Clamp(y, bounds.yMin + 8f, bounds.yMax - tipH - 8f);

            _rect.pivot = new Vector2(0.5f, 0f);
            _rect.anchoredPosition = new Vector2(x, y);
        }

        private void LayoutNear(RectTransform anchor)
        {
            if (_rect == null || anchor == null || _canvasRect == null) return;

            Vector3 world = anchor.TransformPoint(anchor.rect.center);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                RectTransformUtility.WorldToScreenPoint(_canvas != null ? _canvas.worldCamera : null, world),
                _canvas != null ? _canvas.worldCamera : null,
                out local);

            const float gap = 14f;
            float tipH = _rect.sizeDelta.y;
            float tipW = _rect.sizeDelta.x;
            // Prefer above the slot so the panel doesn't cover the gray relic bar.
            float y = local.y + anchor.rect.height * 0.5f + gap;
            float x = local.x;

            var bounds = _canvasRect.rect;
            x = Mathf.Clamp(x, bounds.xMin + tipW * 0.5f + 8f, bounds.xMax - tipW * 0.5f - 8f);
            if (y + tipH > bounds.yMax - 8f)
                y = local.y - anchor.rect.height * 0.5f - gap - tipH;
            y = Mathf.Clamp(y, bounds.yMin + 8f, bounds.yMax - tipH - 8f);

            _rect.anchoredPosition = new Vector2(x, y);
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            int size,
            FontStyle style,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.lineSpacing = 1.05f;
            return text;
        }
    }
}
