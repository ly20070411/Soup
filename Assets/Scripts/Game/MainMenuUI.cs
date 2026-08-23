using System.Collections.Generic;
using Soup.Jobs;
using Soup.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Independent title-screen UI (own scene). Start / Continue / Quit.
    /// New Game enters play immediately; gather / process starter picks happen in-play.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public const string CanvasName = "MainMenuCanvas";
        public const string FreeDrawName = "FreeDraw";
        public const string SystemHudName = "SystemHud";

        /// <summary>Title illustration paper color sampled from 开始.png (top wash).</summary>
        public static readonly Color TitlePaperColor = new Color(1f, 210f / 255f, 147f / 255f, 1f);

        private const int ProcessStarterOfferCount = 4;

        public static MainMenuUI Instance { get; private set; }

        /// <summary>Scene object you can parent Image / Text / buttons under. Never moved or destroyed at runtime.</summary>
        public Transform FreeDrawRoot
        {
            get
            {
                if (_canvas != null)
                    return FindNamed(_canvas.transform, FreeDrawName);
                var canvas = transform.Find(CanvasName);
                return canvas != null ? FindNamed(canvas, FreeDrawName) : null;
            }
        }

        private GameObject _root;
        private GameObject _buttonStack;
        private GameObject _gatherStarterPanel;
        private GameObject _processStarterPanel;
        private GameObject _levelTunePanel;
        private GameObject _victoryPanel;
        private GameObject _defeatPanel;
        private Transform _levelTuneListRoot;
        private Button _continueButton;
        private Text _statusText;
        private Text _levelTuneStatusText;
        private Text _victoryScoreText;
        private Text _victoryBodyText;
        private Text _defeatScoreText;
        private Text _defeatBodyText;
        private Text _defeatTitleText;
        private Canvas _canvas;
        private bool _transitioning;

        private readonly List<LevelTuneRow> _tuneRows = new List<LevelTuneRow>(8);

        private sealed class LevelTuneRow
        {
            public LevelItem Level;
            public InputField ScoreInput;
            public InputField TurnsInput;
        }

        private readonly Button[] _gatherButtons = new Button[2];
        private readonly Text[] _gatherLabels = new Text[2];
        private readonly List<JobItem> _gatherChoices = new List<JobItem>(2);

        private readonly Button[] _processButtons = new Button[ProcessStarterOfferCount];
        private readonly Text[] _processLabels = new Text[ProcessStarterOfferCount];
        private readonly List<JobItem> _processChoices = new List<JobItem>(ProcessStarterOfferCount);

        private string _pendingGatherStarterJobId;

        public bool IsOpen => _root != null && _root.activeSelf && !_transitioning;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureAuthoredCanvas(true);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            RefreshContinue();
            TryShowPendingResultPanels();
        }

        private void OnEnable()
        {
            RefreshContinue();
            TryShowPendingResultPanels();
        }

        private void TryShowPendingResultPanels()
        {
            if (GameSessionLaunch.ConsumePendingCampaignVictory())
            {
                ShowDefeatPanel(false);
                ShowVictoryPanel(true);
                return;
            }

            if (GameSessionLaunch.ConsumePendingLevelDefeat(out var defeat))
            {
                ShowVictoryPanel(false);
                ShowDefeatPanel(true, defeat);
            }
        }

        private bool AnyChoicePanelOpen()
        {
            return (_gatherStarterPanel != null && _gatherStarterPanel.activeSelf)
                   || (_processStarterPanel != null && _processStarterPanel.activeSelf)
                   || (_levelTunePanel != null && _levelTunePanel.activeSelf)
                   || (_victoryPanel != null && _victoryPanel.activeSelf)
                   || (_defeatPanel != null && _defeatPanel.activeSelf);
        }

        private void RefreshContinue()
        {
            bool hasSave = GameSaveService.HasSave();
            if (_continueButton != null)
                _continueButton.interactable = hasSave;
            if (_statusText != null && !AnyChoicePanelOpen())
                _statusText.text = hasSave ? string.Empty : "暂无存档";
        }

        private void OnStartGame()
        {
            if (_transitioning || BlockDissolveTransition.IsBusy) return;

            _pendingGatherStarterJobId = null;
            ShowGatherStarterPanel(false);
            ShowProcessStarterPanel(false);
            // Starter job picks are forced after entering the play scene.
            BeginNewGameTransition(null, null);
        }

        private void OnContinueGame()
        {
            if (_transitioning || BlockDissolveTransition.IsBusy) return;

            if (!GameSaveService.HasSave())
            {
                if (_statusText != null)
                    _statusText.text = "没有可读取的存档";
                RefreshContinue();
                return;
            }

            _transitioning = true;
            SetMenuInteractable(false);
            GameSessionLaunch.RequestContinue();
        }

        private bool TryLoadGatherChoices()
        {
            _gatherChoices.Clear();
            var db = Resources.Load<JobDatabase>(JobManager.ResourcesDatabasePath);
            if (db == null) return false;

            db.RebuildIndex();
            var pool = new List<JobItem>();
            var all = db.Jobs;
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
            var db = Resources.Load<JobDatabase>(JobManager.ResourcesDatabasePath);
            if (db == null) return false;

            db.RebuildIndex();
            var pool = new List<JobItem>();
            var all = db.Jobs;
            for (int i = 0; i < all.Count; i++)
            {
                var job = all[i];
                if (job == null || job.JobType != JobType.Process) continue;
                pool.Add(job);
            }

            if (pool.Count == 0) return false;

            // Stable order so the four methods always appear in a consistent layout.
            pool.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            int take = Mathf.Min(ProcessStarterOfferCount, pool.Count);
            for (int i = 0; i < take; i++)
                _processChoices.Add(pool[i]);

            return _processChoices.Count > 0;
        }

        private void ShowGatherStarterPanel(bool show)
        {
            if (_gatherStarterPanel != null)
                _gatherStarterPanel.SetActive(show);
            if (_buttonStack != null)
                _buttonStack.SetActive(!show && !AnyChoicePanelOpen());

            if (!show)
            {
                HoverTooltipHub.HideIfPresent();
                RefreshContinue();
                return;
            }

            if (_statusText != null)
                _statusText.text = string.Empty;

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

        private void ShowProcessStarterPanel(bool show)
        {
            if (_processStarterPanel != null)
                _processStarterPanel.SetActive(show);
            if (_buttonStack != null)
                _buttonStack.SetActive(!show && !AnyChoicePanelOpen());

            if (!show)
            {
                HoverTooltipHub.HideIfPresent();
                RefreshContinue();
                return;
            }

            if (_statusText != null)
                _statusText.text = string.Empty;

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

        private void OnGatherStarterChosen(int index)
        {
            if (_transitioning) return;
            if (index < 0 || index >= _gatherChoices.Count) return;

            var job = _gatherChoices[index];
            if (job == null) return;

            if (_gatherStarterPanel != null)
                _gatherStarterPanel.SetActive(false);

            ContinueAfterGatherSelection(job.Id);
        }

        private void ContinueAfterGatherSelection(string gatherJobId)
        {
            _pendingGatherStarterJobId = gatherJobId;

            if (!TryLoadProcessChoices())
            {
                BeginNewGameTransition(_pendingGatherStarterJobId, null);
                return;
            }

            ShowProcessStarterPanel(true);
        }

        private void OnProcessStarterChosen(int index)
        {
            if (_transitioning) return;
            if (index < 0 || index >= _processChoices.Count) return;

            var job = _processChoices[index];
            if (job == null) return;

            BeginNewGameTransition(_pendingGatherStarterJobId, job.Id);
        }

        private void OnGatherStarterBack()
        {
            if (_transitioning) return;
            ShowGatherStarterPanel(false);
        }

        private void OnProcessStarterBack()
        {
            if (_transitioning) return;
            if (_processStarterPanel != null)
                _processStarterPanel.SetActive(false);

            if (_gatherChoices.Count > 0)
                ShowGatherStarterPanel(true);
            else
                ShowGatherStarterPanel(false);
        }

        private void BeginNewGameTransition(
            string gatherStarterJobId,
            string processStarterJobId)
        {
            if (BlockDissolveTransition.IsBusy) return;

            _transitioning = true;
            SetMenuInteractable(false);

            if (_gatherStarterPanel != null)
                _gatherStarterPanel.SetActive(false);
            if (_processStarterPanel != null)
                _processStarterPanel.SetActive(false);
            if (_levelTunePanel != null)
                _levelTunePanel.SetActive(false);
            if (_victoryPanel != null)
                _victoryPanel.SetActive(false);

            GameSessionLaunch.RequestNewGame(gatherStarterJobId, processStarterJobId);
        }

        private void SetMenuInteractable(bool enabled)
        {
            if (_root == null) return;
            var raycaster = _root.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = enabled;
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

        private static void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Bind or create the main-menu canvas without destroying anything already in the scene.
        /// </summary>
        public void EnsureAuthoredCanvas(bool bindRuntimeListeners = true)
        {
            var canvasTf = transform.Find(CanvasName);
            if (canvasTf == null)
                canvasTf = CreateCanvasRoot();

            _canvas = canvasTf.GetComponent<Canvas>();
            _root = canvasTf.gameObject;

            EnsureEventSystem();
            var freeDraw = EnsureStretchLayer(canvasTf, FreeDrawName, asFirst: true);
            EnsureFreeDrawMarker(freeDraw);
            var systemHud = EnsureStretchLayer(canvasTf, SystemHudName, asFirst: false);

            if (freeDraw.childCount == 0 && GameObject.Find("开始") == null)
                CreateDefaultTitleChrome(freeDraw);

            BindExistingWidgets(canvasTf);
            EnsureMenuButtons(freeDraw, systemHud);
            RemoveDeprecatedMenuButtons(canvasTf);
            EnsureChoicePanels(systemHud);
            BindChoiceWidgets();

            if (bindRuntimeListeners)
                BindListeners(canvasTf);

            RefreshContinue();
        }

        private Transform CreateCanvasRoot()
        {
            var canvasGo = new GameObject(CanvasName);
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo.transform;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static Transform EnsureStretchLayer(Transform canvas, string name, bool asFirst)
        {
            var existing = canvas.Find(name);
            if (existing != null)
                return existing;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (asFirst)
                rect.SetAsFirstSibling();
            else
                rect.SetAsLastSibling();
            return rect;
        }

        private static MainMenuFreeDrawLayer EnsureFreeDrawMarker(Transform freeDraw)
        {
            if (freeDraw == null)
                return null;
            var marker = freeDraw.GetComponent<MainMenuFreeDrawLayer>();
            return marker != null ? marker : freeDraw.gameObject.AddComponent<MainMenuFreeDrawLayer>();
        }

        private void CreateDefaultTitleChrome(Transform freeDraw)
        {
            var art = GameArtLibrary.Load();

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(freeDraw, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.sprite = GameOverlayUI.SharedUiSprite();
            bgImage.color = TitlePaperColor;
            bgImage.raycastTarget = true;

            if (art == null || art.TitleBackground == null)
                return;

            var artGo = new GameObject("TitleArt");
            artGo.transform.SetParent(freeDraw, false);
            var artRect = artGo.AddComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0f, 0f);
            artRect.anchorMax = new Vector2(0f, 1f);
            artRect.pivot = new Vector2(0f, 0.5f);
            artRect.anchoredPosition = Vector2.zero;
            artRect.sizeDelta = Vector2.zero;
            var artImage = artGo.AddComponent<Image>();
            artImage.sprite = art.TitleBackground;
            artImage.color = Color.white;
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;
            var fitter = artGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            var spriteRect = art.TitleBackground.rect;
            fitter.aspectRatio = spriteRect.height > 0.01f
                ? spriteRect.width / spriteRect.height
                : 1f;
        }

        private void BindExistingWidgets(Transform canvas)
        {
            _buttonStack = FindNamed(canvas, "ButtonStack")?.gameObject;
            _continueButton = FindHud<Button>(canvas, "ContinueBtn");
            _statusText = FindHud<Text>(canvas, "StatusText");
            _gatherStarterPanel = FindNamed(canvas, "GatherStarterPanel")?.gameObject;
            _processStarterPanel = FindNamed(canvas, "ProcessStarterPanel")?.gameObject;
            _levelTunePanel = FindNamed(canvas, "LevelTunePanel")?.gameObject;
            _victoryPanel = FindNamed(canvas, "VictoryPanel")?.gameObject;
            _defeatPanel = FindNamed(canvas, "DefeatPanel")?.gameObject;
        }

        private void EnsureMenuButtons(Transform freeDraw, Transform systemHud)
        {
            var canvas = _root != null ? _root.transform : null;
            if (canvas == null) return;

            bool hasAuthoredStart = GameObject.Find("按钮1") != null;
            if (FindHud<Button>(canvas, "StartBtn") == null && !hasAuthoredStart)
                CreateDefaultButtonStack(freeDraw);

            var tuneHost = systemHud != null ? systemHud : freeDraw;

            if (_buttonStack == null)
                _buttonStack = FindNamed(canvas, "ButtonStack")?.gameObject;
            if (_continueButton == null)
                _continueButton = FindHud<Button>(canvas, "ContinueBtn");
            if (_statusText == null)
            {
                var host = _buttonStack != null ? _buttonStack.transform : tuneHost;
                _statusText = CreateStatusText(host);
            }
        }

        private void CreateDefaultButtonStack(Transform freeDraw)
        {
            var art = GameArtLibrary.Load();
            var stack = new GameObject("ButtonStack");
            stack.transform.SetParent(freeDraw, false);
            _buttonStack = stack;
            var stackRect = stack.AddComponent<RectTransform>();
            stackRect.anchorMin = new Vector2(1f, 0.5f);
            stackRect.anchorMax = new Vector2(1f, 0.5f);
            stackRect.pivot = new Vector2(1f, 0.5f);
            stackRect.anchoredPosition = new Vector2(-64f, -20f);
            stackRect.sizeDelta = new Vector2(420f, 480f);

            var layout = stack.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 16f;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateTitleButton(stack.transform, "StartBtn", "开始游戏",
                art != null ? art.TitleStartButton : null, null);
            _continueButton = CreateTitleButton(stack.transform, "ContinueBtn", "继续游戏",
                art != null ? art.TitleContinueButton : null, null);
            CreateTitleButton(stack.transform, "QuitBtn", "退出",
                art != null ? art.TitleQuitButton : null, null);
        }

        private void EnsureChoicePanels(Transform systemHud)
        {
            var leftoverRelicPanel = FindNamed(systemHud, "StartingRelicPanel");
            if (leftoverRelicPanel != null)
                leftoverRelicPanel.gameObject.SetActive(false);

            if (_gatherStarterPanel == null)
                BuildGatherStarterPanel(systemHud);
            if (_processStarterPanel == null)
                BuildProcessStarterPanel(systemHud);
            if (_levelTunePanel == null)
                BuildLevelTunePanel(systemHud);
            if (_victoryPanel == null)
                BuildVictoryPanel(systemHud);
            if (_defeatPanel == null)
                BuildDefeatPanel(systemHud);
        }

        private void BindChoiceWidgets()
        {
            for (int i = 0; i < _gatherButtons.Length; i++)
            {
                _gatherButtons[i] = FindHud<Button>(
                    _gatherStarterPanel != null ? _gatherStarterPanel.transform : null,
                    $"GatherOption{i}");
                _gatherLabels[i] = _gatherButtons[i] != null
                    ? _gatherButtons[i].transform.Find("Label")?.GetComponent<Text>()
                    : null;
            }

            for (int i = 0; i < _processButtons.Length; i++)
            {
                _processButtons[i] = FindHud<Button>(
                    _processStarterPanel != null ? _processStarterPanel.transform : null,
                    $"ProcessOption{i}");
                _processLabels[i] = _processButtons[i] != null
                    ? _processButtons[i].transform.Find("Label")?.GetComponent<Text>()
                    : null;
            }

            if (_levelTunePanel != null)
            {
                _levelTuneListRoot = FindNamed(_levelTunePanel.transform, "List");
                _levelTuneStatusText = FindHud<Text>(_levelTunePanel.transform, "Status");
            }

            if (_victoryPanel != null)
            {
                _victoryScoreText = FindHud<Text>(_victoryPanel.transform, "Score");
                _victoryBodyText = FindHud<Text>(_victoryPanel.transform, "Body");
            }

            if (_defeatPanel != null)
            {
                _defeatTitleText = FindHud<Text>(_defeatPanel.transform, "Title");
                _defeatScoreText = FindHud<Text>(_defeatPanel.transform, "Score");
                _defeatBodyText = FindHud<Text>(_defeatPanel.transform, "Body");
            }
        }

        private void BindListeners(Transform canvas)
        {
            BindClick(FindHud<Button>(canvas, "StartBtn"), OnStartGame);
            BindClick(FindHud<Button>(canvas, "ContinueBtn"), OnContinueGame);
            BindClick(FindHud<Button>(canvas, "QuitBtn"), OnQuit);

            BindClick(_gatherButtons[0], OnGatherOption0);
            BindClick(_gatherButtons[1], OnGatherOption1);
            if (_gatherStarterPanel != null)
                BindClick(FindHud<Button>(_gatherStarterPanel.transform, "BackBtn"), OnGatherStarterBack);

            BindClick(_processButtons[0], OnProcessOption0);
            BindClick(_processButtons[1], OnProcessOption1);
            BindClick(_processButtons[2], OnProcessOption2);
            BindClick(_processButtons[3], OnProcessOption3);
            if (_processStarterPanel != null)
                BindClick(FindHud<Button>(_processStarterPanel.transform, "BackBtn"), OnProcessStarterBack);

            if (_levelTunePanel != null)
            {
                BindClick(FindHud<Button>(_levelTunePanel.transform, "SaveBtn"), OnLevelTuneSave);
                BindClick(FindHud<Button>(_levelTunePanel.transform, "CloseBtn"), OnLevelTuneClose);
            }

            if (_victoryPanel != null)
                BindClick(FindHud<Button>(_victoryPanel.transform, "MenuBtn"), OnVictoryClose);

            if (_defeatPanel != null)
            {
                BindClick(FindHud<Button>(_defeatPanel.transform, "RetryBtn"), OnDefeatRetry);
                BindClick(FindHud<Button>(_defeatPanel.transform, "CloseBtn"), OnDefeatClose);
            }
        }

        private void OnGatherOption0() => OnGatherStarterChosen(0);
        private void OnGatherOption1() => OnGatherStarterChosen(1);
        private void OnProcessOption0() => OnProcessStarterChosen(0);
        private void OnProcessOption1() => OnProcessStarterChosen(1);
        private void OnProcessOption2() => OnProcessStarterChosen(2);
        private void OnProcessOption3() => OnProcessStarterChosen(3);

        private static void BindClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void RemoveDeprecatedMenuButtons(Transform canvas)
        {
            HideHudButton(canvas, "LevelTuneBtn");
        }

        private static void HideHudButton(Transform root, string name)
        {
            var tf = FindNamed(root, name);
            if (tf != null)
                tf.gameObject.SetActive(false);
        }

        private static T FindHud<T>(Transform root, string name) where T : Component
        {
            var t = FindNamed(root, name);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private void OnLevelTuneClicked()
        {
            if (_transitioning) return;
            ShowGatherStarterPanel(false);
            ShowProcessStarterPanel(false);
            ShowVictoryPanel(false);
            ShowDefeatPanel(false);
            ShowLevelTunePanel(true);
        }

        private void ShowLevelTunePanel(bool show)
        {
            if (_levelTunePanel != null)
                _levelTunePanel.SetActive(show);
            if (_buttonStack != null)
                _buttonStack.SetActive(!show && !AnyChoicePanelOpen());
            if (show)
                PopulateLevelTuneRows();
        }

        private void ShowVictoryPanel(bool show)
        {
            if (_victoryPanel != null)
                _victoryPanel.SetActive(show);
            if (_buttonStack != null)
                _buttonStack.SetActive(!show && !AnyChoicePanelOpen());
            if (show)
            {
                ApplyVictoryScoreLayout();

                int lastScore = 0;
                var levels = LevelManager.Instance;
                if (levels != null)
                    lastScore = levels.LastFinishedScore;

                if (_victoryPanel != null)
                {
                    var box = FindNamed(_victoryPanel.transform, "Box");
                    var scoreLabel = FindHud<Text>(box != null ? box : _victoryPanel.transform, "ScoreLabel");
                    if (scoreLabel != null)
                        scoreLabel.text = "第五关得分";
                }

                if (_victoryScoreText != null)
                    _victoryScoreText.text = lastScore.ToString();

                if (_victoryBodyText != null)
                {
                    int total = 0;
                    var db = Resources.Load<LevelDatabase>(LevelManager.ResourcesDatabasePath);
                    if (db != null)
                    {
                        LevelTuningStore.ApplySavedToDatabase(db);
                        total = db.GetOrdered().Count;
                    }

                    if (levels != null && levels.LevelsClearedCount > 0)
                        _victoryBodyText.text =
                            $"已完成全部 {Mathf.Max(total, levels.LevelsClearedCount)} 关\n恭喜通关！";
                    else
                        _victoryBodyText.text = total > 0
                            ? $"已完成全部 {total} 关\n恭喜通关！"
                            : "恭喜通关！";
                }
            }
        }

        private void ApplyVictoryScoreLayout()
        {
            if (_victoryPanel == null) return;
            var panelTf = _victoryPanel.transform;
            var box = FindNamed(panelTf, "Box");
            if (box == null) return;

            EnsureVictoryScoreLabel(box);
            EnsureVictoryScoreText(box);

            LayoutVictoryBoxText(FindNamed(box, "ScoreLabel"), new Vector2(0f, -96f), new Vector2(560f, 28f));
            LayoutVictoryBoxText(FindNamed(box, "Score"), new Vector2(0f, -128f), new Vector2(560f, 72f));

            if (_victoryBodyText == null)
                _victoryBodyText = FindHud<Text>(box, "Body");
            if (_victoryBodyText != null)
                LayoutVictoryBoxText(_victoryBodyText.transform, new Vector2(0f, -208f), new Vector2(560f, 72f));

            var menuBtn = FindNamed(box, "MenuBtn");
            if (menuBtn != null)
            {
                var menuRect = menuBtn.GetComponent<RectTransform>();
                if (menuRect != null)
                    menuRect.anchoredPosition = new Vector2(0f, 16f);
            }

            var boxRect = box.GetComponent<RectTransform>();
            if (boxRect != null)
                boxRect.sizeDelta = new Vector2(640f, 420f);
        }

        private static void EnsureVictoryScoreLabel(Transform box)
        {
            if (FindNamed(box, "ScoreLabel") != null) return;
            var label = CreatePanelText(box, "ScoreLabel",
                new Vector2(0f, -96f), new Vector2(560f, 28f), 20, FontStyle.Normal);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            label.text = "第五关得分";
        }

        private void EnsureVictoryScoreText(Transform box)
        {
            if (_victoryScoreText != null) return;
            _victoryScoreText = FindHud<Text>(box, "Score");
            if (_victoryScoreText != null) return;

            _victoryScoreText = CreatePanelText(box, "Score",
                new Vector2(0f, -128f), new Vector2(560f, 72f), 56, FontStyle.Bold);
            _victoryScoreText.alignment = TextAnchor.MiddleCenter;
            _victoryScoreText.color = new Color(1f, 0.92f, 0.45f, 1f);
        }

        private static void LayoutVictoryBoxText(Transform element, Vector2 anchoredPos, Vector2 size)
        {
            if (element == null) return;
            var rect = element.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
        }

        private void PopulateLevelTuneRows()
        {
            if (_levelTuneListRoot == null) return;

            for (int i = _levelTuneListRoot.childCount - 1; i >= 0; i--)
                Destroy(_levelTuneListRoot.GetChild(i).gameObject);
            _tuneRows.Clear();

            var db = Resources.Load<LevelDatabase>(LevelManager.ResourcesDatabasePath);
            if (db == null)
            {
                if (_levelTuneStatusText != null)
                    _levelTuneStatusText.text = "未找到 LevelDatabase";
                return;
            }

            LevelTuningStore.ApplySavedToDatabase(db);
            var ordered = db.GetOrdered();
            for (int i = 0; i < ordered.Count; i++)
            {
                var level = ordered[i];
                if (level == null) continue;
                _tuneRows.Add(CreateTuneRow(_levelTuneListRoot, level));
            }

            if (_levelTuneStatusText != null)
                _levelTuneStatusText.text = ordered.Count > 0
                    ? "修改后点「保存」生效（写入本机设置）"
                    : "关卡库为空";
        }

        private void OnLevelTuneSave()
        {
            for (int i = 0; i < _tuneRows.Count; i++)
            {
                var row = _tuneRows[i];
                if (row == null || row.Level == null) continue;
                int score = ParsePositive(row.ScoreInput != null ? row.ScoreInput.text : null, row.Level.TargetScore);
                int turns = ParsePositive(row.TurnsInput != null ? row.TurnsInput.text : null, row.Level.MaxTurns);
                row.Level.SetVictory(score, turns);
                if (row.ScoreInput != null) row.ScoreInput.text = score.ToString();
                if (row.TurnsInput != null) row.TurnsInput.text = turns.ToString();
            }

            var levels = new List<LevelItem>(_tuneRows.Count);
            for (int i = 0; i < _tuneRows.Count; i++)
            {
                if (_tuneRows[i]?.Level != null)
                    levels.Add(_tuneRows[i].Level);
            }

            LevelTuningStore.SaveAll(levels);
            LevelManager.Instance?.Database?.RebuildIndex();
            if (_levelTuneStatusText != null)
                _levelTuneStatusText.text = "已保存关卡回合与目标分数";
        }

        private void OnLevelTuneClose()
        {
            ShowLevelTunePanel(false);
            RefreshContinue();
        }

        private void OnVictoryClose()
        {
            ShowVictoryPanel(false);
            RefreshContinue();
        }

        private void OnDefeatClose()
        {
            ShowDefeatPanel(false);
            RefreshContinue();
        }

        private void OnDefeatRetry()
        {
            if (_transitioning || BlockDissolveTransition.IsBusy) return;

            ShowDefeatPanel(false);
            _transitioning = true;
            SetMenuInteractable(false);
            GameSessionLaunch.RequestNewGame();
        }

        private void ShowDefeatPanel(bool show, GameSessionLaunch.PendingLevelDefeatInfo info = default)
        {
            if (_defeatPanel != null)
                _defeatPanel.SetActive(show);
            if (_buttonStack != null)
                _buttonStack.SetActive(!show && !AnyChoicePanelOpen());
            if (!show) return;

            if (_defeatTitleText != null)
                _defeatTitleText.text = "游戏失败";
            if (_defeatScoreText != null)
                _defeatScoreText.text = info.Score.ToString();
            if (_defeatBodyText != null)
            {
                string levelName = string.IsNullOrWhiteSpace(info.LevelName) ? "本关" : info.LevelName;
                _defeatBodyText.text = info.TargetScore > 0
                    ? $"{levelName} 未达标\n目标 {info.TargetScore} 分"
                    : $"{levelName} 未达标";
            }
        }

        private static int ParsePositive(string raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw) || !int.TryParse(raw.Trim(), out int value))
                return Mathf.Max(1, fallback);
            return Mathf.Max(1, value);
        }

        private void BuildGatherStarterPanel(Transform parent)
        {
            var panelGo = new GameObject("GatherStarterPanel");
            panelGo.transform.SetParent(parent, false);
            _gatherStarterPanel = panelGo;

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
            boxRect.sizeDelta = new Vector2(780f, 420f);
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = GameOverlayUI.SharedUiSprite();
            boxImage.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            var title = CreatePanelText(box.transform, "Title",
                new Vector2(0f, -28f), new Vector2(700f, 48f), 32, FontStyle.Bold);
            title.text = "选择采集岗位";
            title.alignment = TextAnchor.MiddleCenter;

            var subtitle = CreatePanelText(box.transform, "Subtitle",
                new Vector2(0f, -78f), new Vector2(700f, 32f), 20, FontStyle.Normal);
            subtitle.text = "随机二选一，选定后激活并显示在空位上";
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.85f, 0.78f, 0.45f);

            float y = -140f;
            for (int i = 0; i < 2; i++)
            {
                var button = CreateWideChoiceButton(
                    box.transform,
                    $"GatherOption{i}",
                    $"岗位 {i + 1}",
                    new Vector2(0f, y),
                    new Vector2(700f, 100f),
                    null);
                _gatherButtons[i] = button;
                _gatherLabels[i] = button.transform.Find("Label")?.GetComponent<Text>();
                if (_gatherLabels[i] != null)
                {
                    _gatherLabels[i].fontSize = 20;
                    _gatherLabels[i].alignment = TextAnchor.MiddleLeft;
                    var lr = _gatherLabels[i].GetComponent<RectTransform>();
                    if (lr != null)
                    {
                        lr.offsetMin = new Vector2(18f, 8f);
                        lr.offsetMax = new Vector2(-18f, -8f);
                    }
                }

                y -= 120f;
            }

            CreateWideChoiceButton(
                box.transform,
                "BackBtn",
                "返回",
                new Vector2(0f, -360f),
                new Vector2(200f, 48f),
                null);

            panelGo.SetActive(false);
        }

        private void BuildProcessStarterPanel(Transform parent)
        {
            var panelGo = new GameObject("ProcessStarterPanel");
            panelGo.transform.SetParent(parent, false);
            _processStarterPanel = panelGo;

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
            boxRect.sizeDelta = new Vector2(780f, 640f);
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = GameOverlayUI.SharedUiSprite();
            boxImage.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            var title = CreatePanelText(box.transform, "Title",
                new Vector2(0f, -28f), new Vector2(700f, 48f), 32, FontStyle.Bold);
            title.text = "选择处理方法";
            title.alignment = TextAnchor.MiddleCenter;

            var subtitle = CreatePanelText(box.transform, "Subtitle",
                new Vector2(0f, -78f), new Vector2(700f, 32f), 20, FontStyle.Normal);
            subtitle.text = "四选一，选定后解锁并显示在处理区空位上";
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.color = new Color(0.85f, 0.78f, 0.45f);

            float y = -130f;
            for (int i = 0; i < ProcessStarterOfferCount; i++)
            {
                var button = CreateWideChoiceButton(
                    box.transform,
                    $"ProcessOption{i}",
                    $"处理 {i + 1}",
                    new Vector2(0f, y),
                    new Vector2(700f, 110f),
                    null);
                _processButtons[i] = button;
                _processLabels[i] = button.transform.Find("Label")?.GetComponent<Text>();
                if (_processLabels[i] != null)
                {
                    _processLabels[i].fontSize = 18;
                    _processLabels[i].alignment = TextAnchor.MiddleLeft;
                    var lr = _processLabels[i].GetComponent<RectTransform>();
                    if (lr != null)
                    {
                        lr.offsetMin = new Vector2(18f, 8f);
                        lr.offsetMax = new Vector2(-18f, -8f);
                    }
                }

                y -= 120f;
            }

            CreateWideChoiceButton(
                box.transform,
                "BackBtn",
                "返回",
                new Vector2(0f, -580f),
                new Vector2(200f, 48f),
                null);

            panelGo.SetActive(false);
        }

        private void BuildLevelTunePanel(Transform parent)
        {
            var panelGo = new GameObject("LevelTunePanel");
            panelGo.transform.SetParent(parent, false);
            _levelTunePanel = panelGo;

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
            boxRect.sizeDelta = new Vector2(920f, 720f);
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = GameOverlayUI.SharedUiSprite();
            boxImage.color = new Color(0.12f, 0.15f, 0.22f, 0.98f);

            var title = CreatePanelText(box.transform, "Title",
                new Vector2(0f, -24f), new Vector2(860f, 44f), 30, FontStyle.Bold);
            title.text = "关卡调节";
            title.alignment = TextAnchor.MiddleCenter;

            _levelTuneStatusText = CreatePanelText(box.transform, "Status",
                new Vector2(0f, -68f), new Vector2(860f, 28f), 18, FontStyle.Normal);
            _levelTuneStatusText.alignment = TextAnchor.MiddleCenter;
            _levelTuneStatusText.color = new Color(0.85f, 0.78f, 0.45f);

            var header = CreatePanelText(box.transform, "Header",
                new Vector2(0f, -104f), new Vector2(860f, 28f), 18, FontStyle.Normal);
            header.text = "关卡                    目标分数          回合数";
            header.alignment = TextAnchor.MiddleLeft;
            header.color = new Color(0.75f, 0.80f, 0.88f, 1f);

            var listGo = new GameObject("List");
            listGo.transform.SetParent(box.transform, false);
            var listRect = listGo.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = new Vector2(30f, 90f);
            listRect.offsetMax = new Vector2(-30f, -140f);
            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            _levelTuneListRoot = listGo.transform;

            CreateAnchoredButton(box.transform, "SaveBtn", "保存",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-120f, 28f), new Vector2(200f, 52f),
                null);
            CreateAnchoredButton(box.transform, "CloseBtn", "返回",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(120f, 28f), new Vector2(200f, 52f),
                null);

            panelGo.SetActive(false);
        }

        private void BuildVictoryPanel(Transform parent)
        {
            var panelGo = new GameObject("VictoryPanel");
            panelGo.transform.SetParent(parent, false);
            _victoryPanel = panelGo;

            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var dim = panelGo.AddComponent<Image>();
            dim.sprite = GameOverlayUI.SharedUiSprite();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true;

            var box = new GameObject("Box");
            box.transform.SetParent(panelGo.transform, false);
            var boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(640f, 420f);
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = GameOverlayUI.SharedUiSprite();
            boxImage.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

            var title = CreatePanelText(box.transform, "Title",
                new Vector2(0f, -40f), new Vector2(560f, 52f), 36, FontStyle.Bold);
            title.text = "游戏胜利！";
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.92f, 0.45f, 1f);

            var scoreLabel = CreatePanelText(box.transform, "ScoreLabel",
                new Vector2(0f, -96f), new Vector2(560f, 28f), 20, FontStyle.Normal);
            scoreLabel.alignment = TextAnchor.MiddleCenter;
            scoreLabel.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            scoreLabel.text = "第五关得分";

            _victoryScoreText = CreatePanelText(box.transform, "Score",
                new Vector2(0f, -128f), new Vector2(560f, 72f), 56, FontStyle.Bold);
            _victoryScoreText.alignment = TextAnchor.MiddleCenter;
            _victoryScoreText.color = new Color(1f, 0.92f, 0.45f, 1f);

            _victoryBodyText = CreatePanelText(box.transform, "Body",
                new Vector2(0f, -208f), new Vector2(560f, 72f), 22, FontStyle.Normal);
            _victoryBodyText.alignment = TextAnchor.MiddleCenter;
            _victoryBodyText.color = new Color(0.88f, 0.90f, 0.94f, 1f);

            CreateAnchoredButton(box.transform, "MenuBtn", "返回主菜单",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(280f, 56f),
                null);

            panelGo.SetActive(false);
        }

        private void BuildDefeatPanel(Transform parent)
        {
            var panelGo = new GameObject("DefeatPanel");
            panelGo.transform.SetParent(parent, false);
            _defeatPanel = panelGo;

            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var dim = panelGo.AddComponent<Image>();
            dim.sprite = GameOverlayUI.SharedUiSprite();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true;

            var box = new GameObject("Box");
            box.transform.SetParent(panelGo.transform, false);
            var boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(640f, 420f);
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = GameOverlayUI.SharedUiSprite();
            boxImage.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

            _defeatTitleText = CreatePanelText(box.transform, "Title",
                new Vector2(0f, -40f), new Vector2(560f, 52f), 36, FontStyle.Bold);
            _defeatTitleText.text = "游戏失败";
            _defeatTitleText.alignment = TextAnchor.MiddleCenter;
            _defeatTitleText.color = new Color(1f, 0.55f, 0.45f, 1f);

            var scoreLabel = CreatePanelText(box.transform, "ScoreLabel",
                new Vector2(0f, -96f), new Vector2(560f, 28f), 20, FontStyle.Normal);
            scoreLabel.alignment = TextAnchor.MiddleCenter;
            scoreLabel.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            scoreLabel.text = "本关得分";

            _defeatScoreText = CreatePanelText(box.transform, "Score",
                new Vector2(0f, -128f), new Vector2(560f, 72f), 56, FontStyle.Bold);
            _defeatScoreText.alignment = TextAnchor.MiddleCenter;
            _defeatScoreText.color = new Color(1f, 0.92f, 0.45f, 1f);

            _defeatBodyText = CreatePanelText(box.transform, "Body",
                new Vector2(0f, -208f), new Vector2(560f, 72f), 22, FontStyle.Normal);
            _defeatBodyText.alignment = TextAnchor.MiddleCenter;
            _defeatBodyText.color = new Color(0.88f, 0.90f, 0.94f, 1f);

            CreateAnchoredButton(box.transform, "RetryBtn", "重新开始",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-130f, 16f), new Vector2(220f, 56f),
                null);
            CreateAnchoredButton(box.transform, "CloseBtn", "留在主菜单",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(130f, 16f), new Vector2(220f, 56f),
                null);

            panelGo.SetActive(false);
        }

        private LevelTuneRow CreateTuneRow(Transform parent, LevelItem level)
        {
            var go = new GameObject($"Tune_{level.Id}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 64f;
            le.preferredHeight = 64f;

            var bg = go.AddComponent<Image>();
            bg.sprite = GameOverlayUI.SharedUiSprite();
            bg.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);

            var name = CreateChildText(go.transform, "Name",
                new Vector2(0f, 0f), new Vector2(0.42f, 1f),
                new Vector2(16f, 8f), new Vector2(-8f, -8f),
                20, TextAnchor.MiddleLeft);
            name.text = level.DisplayName;

            var scoreInput = CreateIntegerInput(go.transform, "ScoreInput",
                new Vector2(0.42f, 0.15f), new Vector2(0.68f, 0.85f),
                level.TargetScore.ToString());
            var turnsInput = CreateIntegerInput(go.transform, "TurnsInput",
                new Vector2(0.70f, 0.15f), new Vector2(0.96f, 0.85f),
                level.MaxTurns.ToString());

            return new LevelTuneRow
            {
                Level = level,
                ScoreInput = scoreInput,
                TurnsInput = turnsInput
            };
        }

        private static InputField CreateIntegerInput(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string value)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.sprite = GameOverlayUI.SharedUiSprite();
            image.color = new Color(0.10f, 0.12f, 0.16f, 1f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            var text = textGo.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.supportRichText = false;

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(go.transform, false);
            var phRect = placeholderGo.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(10f, 4f);
            phRect.offsetMax = new Vector2(-10f, -4f);
            var placeholder = placeholderGo.AddComponent<Text>();
            placeholder.font = GameOverlayUI.SharedUiFont();
            placeholder.fontSize = 18;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.alignment = TextAnchor.MiddleCenter;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.text = "0";

            var input = go.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 6;
            input.text = value ?? "1";
            return input;
        }

        private static Text CreateChildText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            int fontSize,
            TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var text = go.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateAnchoredButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = GameOverlayUI.FitArtButtonSize(size.x, size.y);

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            GameOverlayUI.ApplyArtButtonStyle(image, button);
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            var text = labelGo.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
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

        private static Button CreateWideChoiceButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = GameOverlayUI.FitArtButtonSize(size.x, size.y);

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            GameOverlayUI.ApplyArtButtonStyle(image, button);
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return button;
        }

        private static Button CreateTitleButton(
            Transform parent,
            string name,
            string fallbackLabel,
            Sprite sprite,
            UnityEngine.Events.UnityAction onClick)
        {
            if (sprite == null)
                return CreateMenuButton(parent, name, fallbackLabel, onClick);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var fitted = GameOverlayUI.FitSpriteAspectSize(sprite, 118f, 2f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = fitted.x;
            le.preferredHeight = fitted.y;
            le.minWidth = fitted.x;
            le.minHeight = fitted.y;

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            GameOverlayUI.ApplyTitleImageButtonStyle(image, button, sprite);
            if (onClick != null)
                button.onClick.AddListener(onClick);
            return button;
        }

        private static Button CreateMenuButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var fitted = GameOverlayUI.FitArtButtonSize(360f, 64f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = fitted.y;
            le.minHeight = fitted.y;

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            GameOverlayUI.ApplyArtButtonStyle(image, button);
            if (onClick != null)
                button.onClick.AddListener(onClick);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 28;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;

            return button;
        }

        private static Text CreateStatusText(Transform parent)
        {
            var go = new GameObject("StatusText");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(360f, 36f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;
            le.minHeight = 36f;
            le.preferredWidth = 360f;
            var text = go.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.32f, 0.16f, 0.06f);
            text.raycastTarget = false;
            return text;
        }
    }
}
