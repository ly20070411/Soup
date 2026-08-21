using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Shared floating tooltip used by HUD, shop, events, and world stations.
    /// </summary>
    public sealed class HoverTooltipHub : MonoBehaviour
    {
        private static HoverTooltipHub _instance;

        private RelicHudTooltip _tooltip;
        private RectTransform _fallbackAnchor;

        public static HoverTooltipHub Instance
        {
            get
            {
                if (_instance != null) return _instance;
                // Never spawn hubs while a scene is tearing down.
                if (!Application.isPlaying) return null;
                _instance = FindObjectOfType<HoverTooltipHub>();
                if (_instance != null) return _instance;

                var go = new GameObject(nameof(HoverTooltipHub));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HoverTooltipHub>();
                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        public static void HideIfPresent()
        {
            if (_instance != null)
                _instance.Hide();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public void Show(string title, string body, RectTransform anchor)
        {
            EnsureTooltip();
            if (_tooltip == null) return;
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            {
                Hide();
                return;
            }

            _tooltip.Show(title ?? string.Empty, body ?? string.Empty, anchor != null ? anchor : _fallbackAnchor);
        }

        public void ShowAtScreen(string title, string body, Vector2 screenPos)
        {
            EnsureTooltip();
            if (_tooltip == null) return;
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
            {
                Hide();
                return;
            }

            _tooltip.ShowAtScreen(title ?? string.Empty, body ?? string.Empty, screenPos);
        }

        public void MoveToScreen(Vector2 screenPos)
        {
            if (_tooltip == null) return;
            _tooltip.MoveToScreen(screenPos);
        }

        public void Hide() => _tooltip?.Hide();

        private void EnsureTooltip()
        {
            if (_tooltip != null) return;

            var host = ResolveHost();
            if (host == null) return;

            _tooltip = host.GetComponent<RelicHudTooltip>();
            if (_tooltip == null)
                _tooltip = host.gameObject.AddComponent<RelicHudTooltip>();
            _tooltip.EnsureBuilt(host, GameOverlayUI.SharedUiFont());

            if (_fallbackAnchor == null)
            {
                var go = new GameObject("TooltipAnchor", typeof(RectTransform));
                go.transform.SetParent(host, false);
                _fallbackAnchor = go.GetComponent<RectTransform>();
                _fallbackAnchor.anchorMin = _fallbackAnchor.anchorMax = new Vector2(0.5f, 0.5f);
                _fallbackAnchor.sizeDelta = new Vector2(40f, 40f);
                _fallbackAnchor.anchoredPosition = Vector2.zero;
            }
        }

        private static Transform ResolveHost()
        {
            var authored = FindObjectOfType<PlayAuthoredHud>();
            if (authored != null) return authored.transform;

            var overlay = FindObjectOfType<GameOverlayUI>();
            if (overlay != null)
            {
                var canvas = overlay.GetComponentInChildren<Canvas>(true);
                if (canvas != null) return canvas.transform;
                return overlay.transform;
            }

            var inter = FindObjectOfType<InterLevelUI>();
            if (inter != null)
            {
                var canvas = inter.GetComponentInChildren<Canvas>(true);
                if (canvas != null) return canvas.transform;
                return inter.transform;
            }

            var canvasAny = FindObjectOfType<Canvas>();
            return canvasAny != null ? canvasAny.transform : null;
        }
    }
}
