using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Shared floating tooltip used by HUD, shop, events, job picks, and world stations.
    /// Same panel style as relic hovers (<see cref="RelicHudTooltip"/>).
    /// </summary>
    public sealed class HoverTooltipHub : MonoBehaviour
    {
        private const int TipSortingOrder = 5000;

        private static HoverTooltipHub _instance;

        private RelicHudTooltip _tooltip;
        private RectTransform _fallbackAnchor;

        public static HoverTooltipHub Instance
        {
            get
            {
                if (_instance != null) return _instance;
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
            EnsureTooltip(anchor);
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
            EnsureTooltip(null);
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

        private void EnsureTooltip(RectTransform preferredAnchor)
        {
            Transform host = ResolveHost(preferredAnchor);
            if (host == null) return;

            if (_tooltip == null)
                _tooltip = GetComponent<RelicHudTooltip>();
            if (_tooltip == null)
                _tooltip = gameObject.AddComponent<RelicHudTooltip>();

            _tooltip.EnsureBuilt(host, GameOverlayUI.SharedUiFont());
            _tooltip.EnsureTopMost(TipSortingOrder);

            if (_fallbackAnchor == null)
            {
                var go = new GameObject("TooltipAnchor", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _fallbackAnchor = go.GetComponent<RectTransform>();
                _fallbackAnchor.anchorMin = _fallbackAnchor.anchorMax = new Vector2(0.5f, 0.5f);
                _fallbackAnchor.sizeDelta = new Vector2(40f, 40f);
                _fallbackAnchor.anchoredPosition = Vector2.zero;
            }
        }

        private static Transform ResolveHost(RectTransform preferredAnchor)
        {
            if (preferredAnchor != null)
            {
                var canvas = preferredAnchor.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
                    return root.transform;
                }
            }

            var eventPanel = FindObjectOfType<EventPanelUI>();
            if (eventPanel != null && eventPanel.IsOpen)
            {
                var eventCanvas = eventPanel.transform.Find(EventPanelUI.CanvasName);
                if (eventCanvas != null)
                    return eventCanvas;
            }

            var starter = FindObjectOfType<StarterJobSelectUI>();
            if (starter != null)
            {
                var canvas = starter.GetComponentInChildren<Canvas>(true);
                if (canvas != null && canvas.gameObject.activeInHierarchy)
                    return canvas.transform;
            }

            var mainMenu = FindObjectOfType<MainMenuUI>();
            if (mainMenu != null)
            {
                var canvasTf = mainMenu.transform.Find(MainMenuUI.CanvasName);
                if (canvasTf != null && canvasTf.gameObject.activeInHierarchy)
                    return canvasTf;
            }

            var visit = FindObjectOfType<AdvancementVisitUI>();
            if (visit != null)
            {
                var canvas = visit.GetComponentInChildren<Canvas>(true);
                if (canvas != null && canvas.gameObject.activeInHierarchy)
                    return canvas.transform;
            }

            var authored = FindObjectOfType<PlayAuthoredHud>();
            if (authored != null)
            {
                var canvas = authored.GetComponentInParent<Canvas>();
                if (canvas != null)
                    return (canvas.rootCanvas != null ? canvas.rootCanvas : canvas).transform;
                return authored.transform;
            }

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
