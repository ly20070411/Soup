using System.Collections.Generic;
using Soup.Jobs;
using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// In-play opening setup: gather 2-pick then process 4-pick.
    /// Modal; no back — must finish before the run can proceed.
    /// </summary>
    public sealed class StarterJobSelectUI : MonoBehaviour
    {
        private const int ProcessStarterOfferCount = 4;

        public static StarterJobSelectUI Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _gatherPanel;
        private GameObject _processPanel;
        private readonly Button[] _gatherButtons = new Button[2];
        private readonly Text[] _gatherLabels = new Text[2];
        private readonly List<JobItem> _gatherChoices = new List<JobItem>(2);
        private readonly Button[] _processButtons = new Button[ProcessStarterOfferCount];
        private readonly Text[] _processLabels = new Text[ProcessStarterOfferCount];
        private readonly List<JobItem> _processChoices = new List<JobItem>(ProcessStarterOfferCount);
        private bool _built;
        private bool _open;

        public bool IsOpen => _open;

        public static StarterJobSelectUI Ensure(Transform parent = null)
        {
            if (Instance != null)
            {
                Instance.EnsureBuilt();
                return Instance;
            }

            var existing = FindObjectOfType<StarterJobSelectUI>();
            if (existing != null)
            {
                existing.EnsureBuilt();
                return existing;
            }

            var host = parent;
            if (host == null)
            {
                var go = new GameObject(nameof(StarterJobSelectUI));
                return go.AddComponent<StarterJobSelectUI>();
            }

            var child = new GameObject(nameof(StarterJobSelectUI));
            child.transform.SetParent(host, false);
            return child.AddComponent<StarterJobSelectUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Shows gather then process starter panels when the run still needs them.
        /// Returns true if a panel was opened.
        /// </summary>
        public bool BeginIfNeeded()
        {
            EnsureBuilt();
            var progression = JobProgressionManager.Instance;
            if (progression == null) return false;

            progression.BootstrapDefaults();

            if (progression.NeedsGatherStarterPick)
            {
                if (!TryLoadGatherChoices())
                {
                    progression.MarkGatherStarterComplete();
                }
                else
                {
                    ShowGatherPanel(true);
                    return true;
                }
            }

            if (progression.NeedsProcessStarterPick)
            {
                if (!TryLoadProcessChoices())
                {
                    progression.MarkProcessStarterComplete();
                    FinishSetup();
                    return false;
                }

                ShowProcessPanel(true);
                return true;
            }

            return false;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var canvasGo = new GameObject("StarterJobSelectCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 520;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildGatherPanel(canvasGo.transform);
            BuildProcessPanel(canvasGo.transform);
            ShowGatherPanel(false);
            ShowProcessPanel(false);
        }

        private bool TryLoadGatherChoices()
        {
            _gatherChoices.Clear();
            var jobs = JobManager.Instance;
            if (jobs == null) return false;

            var pool = new List<JobItem>();
            var all = jobs.All;
            for (int i = 0; i < all.Count; i++)
            {
                var job = all[i];
                if (job == null || job.JobType != JobType.Gather) continue;
                if (job.Id == JobProgressionRules.StartingGatherJobId) continue;
                pool.Add(job);
            }

            if (pool.Count == 0) return false;

            Shuffle(pool);
            int take = Mathf.Min(JobProgressionRules.GatherNewJobOfferCount, pool.Count);
            for (int i = 0; i < take; i++)
                _gatherChoices.Add(pool[i]);

            return _gatherChoices.Count > 0;
        }

        private bool TryLoadProcessChoices()
        {
            _processChoices.Clear();
            var jobs = JobManager.Instance;
            if (jobs == null) return false;

            var pool = new List<JobItem>();
            var all = jobs.All;
            for (int i = 0; i < all.Count; i++)
            {
                var job = all[i];
                if (job == null || job.JobType != JobType.Process) continue;
                pool.Add(job);
            }

            if (pool.Count == 0) return false;

            pool.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            int take = Mathf.Min(ProcessStarterOfferCount, pool.Count);
            for (int i = 0; i < take; i++)
                _processChoices.Add(pool[i]);

            return _processChoices.Count > 0;
        }

        private void ShowGatherPanel(bool show)
        {
            if (_gatherPanel != null)
                _gatherPanel.SetActive(show);
            _open = show || (_processPanel != null && _processPanel.activeSelf);

            if (!show)
            {
                HoverTooltipHub.HideIfPresent();
                return;
            }

            for (int i = 0; i < _gatherButtons.Length; i++)
            {
                bool has = i < _gatherChoices.Count && _gatherChoices[i] != null;
                if (_gatherButtons[i] != null)
                    _gatherButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                var job = _gatherChoices[i];
                if (_gatherLabels[i] != null)
                    _gatherLabels[i].text = job.DisplayName;
                BindJobOptionHover(_gatherButtons[i], job);
            }
        }

        private void ShowProcessPanel(bool show)
        {
            if (_processPanel != null)
                _processPanel.SetActive(show);
            _open = show || (_gatherPanel != null && _gatherPanel.activeSelf);

            if (!show)
            {
                HoverTooltipHub.HideIfPresent();
                return;
            }

            for (int i = 0; i < _processButtons.Length; i++)
            {
                bool has = i < _processChoices.Count && _processChoices[i] != null;
                if (_processButtons[i] != null)
                    _processButtons[i].gameObject.SetActive(has);
                if (!has) continue;

                var job = _processChoices[i];
                if (_processLabels[i] != null)
                    _processLabels[i].text = job.DisplayName;
                BindJobOptionHover(_processButtons[i], job);
            }
        }

        private static void BindJobOptionHover(Button button, JobItem job)
        {
            if (button == null || job == null) return;
            HoverTooltipText.JobStation(job, out string title, out string body);
            var tip = button.GetComponent<UiHoverTooltip>();
            if (tip == null)
                tip = button.gameObject.AddComponent<UiHoverTooltip>();
            tip.Bind(title, body);
        }

        private void OnGatherChosen(int index)
        {
            if (!_open) return;
            if (index < 0 || index >= _gatherChoices.Count) return;

            var job = _gatherChoices[index];
            if (job == null) return;

            var progression = JobProgressionManager.Instance;
            if (progression == null || !progression.TryPickGatherStarter(job))
                return;

            ShowGatherPanel(false);
            RefreshWorldMap();
            ShowToast($"采集：{job.DisplayName}");
            ContinueAfterGatherSelection();
        }

        private void ContinueAfterGatherSelection()
        {
            var progression = JobProgressionManager.Instance;
            if (progression != null && !progression.NeedsProcessStarterPick)
            {
                FinishSetup();
                return;
            }

            if (!TryLoadProcessChoices())
            {
                progression?.MarkProcessStarterComplete();
                FinishSetup();
                return;
            }

            ShowProcessPanel(true);
        }

        private void OnProcessChosen(int index)
        {
            if (!_open) return;
            if (index < 0 || index >= _processChoices.Count) return;

            var job = _processChoices[index];
            if (job == null) return;

            var progression = JobProgressionManager.Instance;
            if (progression == null || !progression.TryPickProcessStarter(job))
                return;

            ShowProcessPanel(false);
            RefreshWorldMap();
            ShowToast($"处理：{job.DisplayName}");
            FinishSetup();
        }

        private void FinishSetup()
        {
            ShowGatherPanel(false);
            ShowProcessPanel(false);
            _open = false;
            RefreshWorldMap();
        }

        private static void RefreshWorldMap()
        {
            var map = FindObjectOfType<JobWorldMap>();
            if (map == null) return;
            map.RebuildStations();
            map.RefreshLabels();
        }

        private static void ShowToast(string message)
        {
            var overlay = FindObjectOfType<GameOverlayUI>();
            overlay?.ShowToast(message, 3.5f);
        }

        private void BuildGatherPanel(Transform parent)
        {
            _gatherPanel = BuildModalPanel(parent, "GatherStarterPanel", 780f, 420f,
                "选择采集岗位",
                "随机二选一，选定后激活并显示在空位上");

            var box = _gatherPanel.transform.Find("Box");
            float y = -140f;
            for (int i = 0; i < 2; i++)
            {
                int captured = i;
                var button = CreateWideChoiceButton(
                    box,
                    $"GatherOption{i}",
                    $"岗位 {i + 1}",
                    new Vector2(0f, y),
                    new Vector2(700f, 100f));
                button.onClick.AddListener(() => OnGatherChosen(captured));
                _gatherButtons[i] = button;
                _gatherLabels[i] = button.transform.Find("Label")?.GetComponent<Text>();
                StyleChoiceLabel(_gatherLabels[i]);
                y -= 120f;
            }
        }

        private void BuildProcessPanel(Transform parent)
        {
            _processPanel = BuildModalPanel(parent, "ProcessStarterPanel", 780f, 640f,
                "选择处理方法",
                "四选一，选定后解锁并显示在处理区空位上");

            var box = _processPanel.transform.Find("Box");
            float y = -130f;
            for (int i = 0; i < ProcessStarterOfferCount; i++)
            {
                int captured = i;
                var button = CreateWideChoiceButton(
                    box,
                    $"ProcessOption{i}",
                    $"处理 {i + 1}",
                    new Vector2(0f, y),
                    new Vector2(700f, 110f));
                button.onClick.AddListener(() => OnProcessChosen(captured));
                _processButtons[i] = button;
                _processLabels[i] = button.transform.Find("Label")?.GetComponent<Text>();
                StyleChoiceLabel(_processLabels[i]);
                y -= 120f;
            }
        }

        private static GameObject BuildModalPanel(
            Transform parent,
            string name,
            float boxWidth,
            float boxHeight,
            string titleText,
            string subtitleText)
        {
            var panelGo = new GameObject(name);
            panelGo.transform.SetParent(parent, false);

            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var dim = panelGo.AddComponent<Image>();
            dim.sprite = GameOverlayUI.SharedUiSprite();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.raycastTarget = true;

            var box = new GameObject("Box");
            box.transform.SetParent(panelGo.transform, false);
            var boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(boxWidth, boxHeight);
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = GameOverlayUI.SharedUiSprite();
            boxImage.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            var title = CreatePanelText(box.transform, "Title",
                new Vector2(0f, -28f), new Vector2(700f, 48f), 32, FontStyle.Bold);
            title.text = titleText;
            title.alignment = TextAnchor.MiddleCenter;

            var subtitle = CreatePanelText(box.transform, "Subtitle",
                new Vector2(0f, -78f), new Vector2(700f, 32f), 20, FontStyle.Normal);
            subtitle.text = subtitleText;
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.85f, 0.78f, 0.45f);

            panelGo.SetActive(false);
            return panelGo;
        }

        private static void StyleChoiceLabel(Text label)
        {
            if (label == null) return;
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleLeft;
            var lr = label.GetComponent<RectTransform>();
            if (lr != null)
            {
                lr.offsetMin = new Vector2(18f, 8f);
                lr.offsetMax = new Vector2(-18f, -8f);
            }
        }

        private static Button CreateWideChoiceButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.sprite = GameOverlayUI.SharedButtonSprite();
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);
            var text = textGo.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.text = label;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return button;
        }

        private static Text CreatePanelText(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize,
            FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Shuffle(List<JobItem> list)
        {
            if (list == null || list.Count <= 1) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
