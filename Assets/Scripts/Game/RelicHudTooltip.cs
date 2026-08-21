using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Floating name + description panel for relic bar hovers.
    /// </summary>
    public sealed class RelicHudTooltip : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _rect;
        private Text _title;
        private Text _body;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        public void EnsureBuilt(Transform parent, Font font)
        {
            if (_root != null) return;

            _root = new GameObject("RelicTooltip", typeof(RectTransform));
            _root.transform.SetParent(parent, false);
            _rect = _root.GetComponent<RectTransform>();
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0f);
            _rect.sizeDelta = new Vector2(320f, 120f);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.12f, 0.94f);
            bg.raycastTarget = false;

            var outline = _root.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.58f, 0.65f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);

            var layout = _root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            _title = CreateText(_root.transform, font, 18, FontStyle.Bold, Color.white);
            _body = CreateText(_root.transform, font, 16, FontStyle.Normal, new Color(0.85f, 0.88f, 0.92f, 1f));

            var bodyLayout = _body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.minHeight = 40f;
            bodyLayout.flexibleHeight = 1f;

            _canvas = parent.GetComponentInParent<Canvas>();
            _canvasRect = _canvas != null ? _canvas.transform as RectTransform : parent as RectTransform;

            var tipCanvas = _root.AddComponent<Canvas>();
            tipCanvas.overrideSorting = true;
            tipCanvas.sortingOrder = 200;
            _root.AddComponent<GraphicRaycaster>().enabled = false;

            _root.SetActive(false);
        }

        public void Show(string title, string body, RectTransform anchor)
        {
            if (_root == null || _rect == null) return;

            _title.text = title ?? string.Empty;
            _body.text = body ?? string.Empty;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();

            Canvas.ForceUpdateCanvases();
            float bodyH = _body != null ? _body.preferredHeight : 48f;
            _rect.sizeDelta = new Vector2(340f, Mathf.Clamp(bodyH + 52f, 72f, 420f));
            LayoutNear(anchor);
        }

        public void ShowAtScreen(string title, string body, Vector2 screenPos)
        {
            if (_root == null || _rect == null) return;

            _title.text = title ?? string.Empty;
            _body.text = body ?? string.Empty;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();

            Canvas.ForceUpdateCanvases();
            float bodyH = _body != null ? _body.preferredHeight : 48f;
            _rect.sizeDelta = new Vector2(340f, Mathf.Clamp(bodyH + 52f, 72f, 420f));
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

            const float gap = 12f;
            float tipH = _rect.sizeDelta.y;
            float tipW = _rect.sizeDelta.x;
            float y = local.y + anchor.rect.height * 0.5f + gap;
            float x = local.x;

            var bounds = _canvasRect.rect;
            x = Mathf.Clamp(x, bounds.xMin + tipW * 0.5f + 8f, bounds.xMax - tipW * 0.5f - 8f);
            if (y + tipH > bounds.yMax - 8f)
                y = local.y - anchor.rect.height * 0.5f - gap - tipH;
            y = Mathf.Clamp(y, bounds.yMin + 8f, bounds.yMax - tipH - 8f);

            _rect.anchoredPosition = new Vector2(x, y);
        }

        private static Text CreateText(Transform parent, Font font, int size, FontStyle style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
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
            return text;
        }
    }
}
