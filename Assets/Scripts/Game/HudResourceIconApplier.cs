using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Places resource / flavor art inside AuthoredHud yellow circles and sizes it to fit.
    /// </summary>
    public static class HudResourceIconApplier
    {
        public const string ChildName = "Icon";
        /// <summary>Fraction of the smaller circle axis used for the icon rect.</summary>
        public const float Fill = 0.68f;

        public static readonly string[] Keys =
        {
            "Soft", "Tough", "Solid", "Processed", "Cooked",
            "Spicy", "Cold", "Sour", "Magic"
        };

        public static int ApplyAll(Transform authoredHudRoot, GameArtLibrary art)
        {
            if (authoredHudRoot == null || art == null)
                return 0;

            int hits = 0;
            for (int i = 0; i < Keys.Length; i++)
            {
                string key = Keys[i];
                var circle = authoredHudRoot.Find("Circle_" + key);
                if (circle == null)
                    continue;
                if (ApplyToCircle(circle as RectTransform ?? circle.GetComponent<RectTransform>(), art.GetHudCounterIcon(key)))
                    hits++;
            }

            return hits;
        }

        public static bool ApplyToCircle(RectTransform circle, Sprite sprite)
        {
            if (circle == null || sprite == null)
                return false;

            var icon = EnsureChildIcon(circle);
            if (icon == null)
                return false;

            float side = Mathf.Min(circle.sizeDelta.x, circle.sizeDelta.y);
            if (side < 1f)
                side = 136f;
            float size = side * Fill;

            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.one;

            icon.sprite = sprite;
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.type = Image.Type.Simple;
            return true;
        }

        private static Image EnsureChildIcon(RectTransform circle)
        {
            var existing = circle.Find(ChildName);
            if (existing != null)
            {
                var img = existing.GetComponent<Image>();
                if (img != null)
                    return img;
            }

            var go = new GameObject(ChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(circle, false);
            return go.GetComponent<Image>();
        }
    }
}
