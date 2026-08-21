using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Cross-scene floating toast: fade in → hold → fade out. Survives scene loads.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GameFloatingToast : MonoBehaviour
    {
        private const float FadeInSeconds = 0.2f;
        private const float FadeOutSeconds = 0.45f;
        private const float DefaultHoldSeconds = 2.4f;

        public static GameFloatingToast Instance { get; private set; }

        private CanvasGroup _group;
        private Text _label;
        private float _holdSeconds;
        private float _phaseElapsed;
        private enum Phase { Idle, FadeIn, Hold, FadeOut }
        private Phase _phase = Phase.Idle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(GameFloatingToast));
            DontDestroyOnLoad(go);
            go.AddComponent<GameFloatingToast>();
        }

        public static void Show(string message, float holdSeconds = DefaultHoldSeconds)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            EnsureExists();
            Instance.Present(message.Trim(), Mathf.Max(0.5f, holdSeconds));
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUi();
            SetVisible(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (_phase == Phase.Idle || _group == null) return;

            _phaseElapsed += Time.unscaledDeltaTime;
            switch (_phase)
            {
                case Phase.FadeIn:
                {
                    float t = FadeInSeconds <= 0f ? 1f : Mathf.Clamp01(_phaseElapsed / FadeInSeconds);
                    SetVisible(t);
                    if (t >= 1f)
                    {
                        _phase = Phase.Hold;
                        _phaseElapsed = 0f;
                    }

                    break;
                }
                case Phase.Hold:
                    SetVisible(1f);
                    if (_phaseElapsed >= _holdSeconds)
                    {
                        _phase = Phase.FadeOut;
                        _phaseElapsed = 0f;
                    }

                    break;
                case Phase.FadeOut:
                {
                    float t = FadeOutSeconds <= 0f ? 1f : Mathf.Clamp01(_phaseElapsed / FadeOutSeconds);
                    SetVisible(1f - t);
                    if (t >= 1f)
                    {
                        _phase = Phase.Idle;
                        if (_label != null)
                            _label.text = string.Empty;
                    }

                    break;
                }
            }
        }

        private void Present(string message, float holdSeconds)
        {
            if (_label != null)
                _label.text = message;
            _holdSeconds = holdSeconds;
            _phase = Phase.FadeIn;
            _phaseElapsed = 0f;
            SetVisible(0f);
        }

        private void SetVisible(float alpha)
        {
            if (_group == null) return;
            _group.alpha = Mathf.Clamp01(alpha);
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("FloatingToastCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.12f);
            panelRect.anchorMax = new Vector2(0.5f, 0.12f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(920f, 72f);
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = GameOverlayUI.SharedUiSprite();
            panelImage.color = new Color(0.08f, 0.10f, 0.14f, 0.88f);
            panelImage.raycastTarget = false;

            _group = panel.AddComponent<CanvasGroup>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(panel.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 8f);
            labelRect.offsetMax = new Vector2(-24f, -8f);
            _label = labelGo.AddComponent<Text>();
            _label.font = GameOverlayUI.SharedUiFont();
            _label.fontSize = 26;
            _label.fontStyle = FontStyle.Bold;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(1f, 0.92f, 0.55f, 1f);
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.raycastTarget = false;
        }
    }
}
