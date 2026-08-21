using System.Collections.Generic;
using Soup.Employees;
using Soup.Events;
using Soup.Jobs;
using Soup.Levels;
using Soup.Relics;
using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// Main play HUD. Scene-authored OverlayCanvas / FreeDraw is never destroyed;
    /// runtime only binds existing widgets and fills in missing system buttons.
    /// Prototype info / zone chrome is hidden while the authored gather scene is showing.
    /// </summary>
    public class GameOverlayUI : MonoBehaviour
    {
        public const string OverlayCanvasName = "OverlayCanvas";
        public const string FreeDrawName = "FreeDraw";
        public const string SystemHudName = "SystemHud";

        [SerializeField] private ZoneCameraController cameraController;
        [SerializeField] private GamePlayHud controlPanelHud;
        [SerializeField] private GatherZoneView gatherZone;
        [SerializeField] private ProcessZoneView processZone;
        [SerializeField] private CookZoneView cookZone;

        private Text _infoText;
        private Text _zoneText;
        private Text _toastText;
        private Canvas _overlayCanvas;
        private GraphicRaycaster _overlayRaycaster;
        private GameObject _settingsPanel;
        private GameObject _zoneFrame;
        private EventPanelUI _eventPanelUi;
        private Button _nextTurnButton;
        private Button _undoTurnButton;
        private Button _settleStageButton;
        private Button _zonePrevButton;
        private Button _zoneNextButton;
        private readonly List<GameObject> _playChrome = new List<GameObject>();
        private string _toast = string.Empty;
        private float _toastUntil;
        private bool _overlayHiddenForPanel;
        private static Sprite _uiSprite;
        private static Font _uiFont;
        private static GameArtLibrary _art;

        private bool UsingZoneArtHud =>
            cameraController != null
            && ((gatherZone != null && cameraController.CurrentZone == MapZoneType.Gather)
                || (processZone != null && cameraController.CurrentZone == MapZoneType.Process)
                || (cookZone != null && cameraController.CurrentZone == MapZoneType.Cook));

        private void Awake()
        {
            if (cameraController == null)
                cameraController = FindObjectOfType<ZoneCameraController>();
            if (controlPanelHud == null)
                controlPanelHud = FindObjectOfType<GamePlayHud>();
            if (gatherZone == null)
                gatherZone = FindObjectOfType<GatherZoneView>();
            if (processZone == null)
                processZone = FindObjectOfType<ProcessZoneView>();
            if (cookZone == null)
                cookZone = FindObjectOfType<CookZoneView>();

            EnsureAuthoredCanvas(true);
            _eventPanelUi = EventPanelUI.Ensure(transform);
            _eventPanelUi.SetToastHandler(msg => ShowToast(msg, 3.5f));
        }

        private void OnEnable()
        {
            BindEventManager(true);
            BindLevelManager(true);
        }

        private void OnDisable()
        {
            BindEventManager(false);
            BindLevelManager(false);
        }

        private void Start()
        {
            if (controlPanelHud != null)
                controlPanelHud.SetPanelMode(false);
            SetSettingsOpen(false);
            BindEventManager(true);
            BindLevelManager(true);
            if (_eventPanelUi == null)
            {
                _eventPanelUi = EventPanelUI.Ensure(transform);
                _eventPanelUi.SetToastHandler(msg => ShowToast(msg, 3.5f));
            }
        }

        private void Update()
        {
            RefreshInfo();
            SyncOverlayWithPanel();

            if (_settingsPanel != null && _settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape)
                && (_eventPanelUi == null || !_eventPanelUi.IsOpen)
                && (StarterJobSelectUI.Instance == null || !StarterJobSelectUI.Instance.IsOpen))
                SetSettingsOpen(false);

            if (_toastText != null)
                _toastText.text = Time.unscaledTime <= _toastUntil ? _toast : string.Empty;

            bool starterBusy = StarterJobSelectUI.Instance != null && StarterJobSelectUI.Instance.IsOpen;
            bool visitBusy = AdvancementVisit.IsActive;
            bool busy = starterBusy
                || visitBusy
                || (EventManager.Instance != null
                    && (EventManager.Instance.HasPendingEvent || EventManager.Instance.HasStageEventBatch));
            var levels = LevelManager.Instance;
            // 关卡间 / 失败已是独立场景；玩法场景内若仍短暂处于该状态则禁用操作。
            // 进阶巡视时 ClearRewards 仍活跃，但不能按「关卡间挂起」处理。
            bool interLevelPending = !visitBusy
                && levels != null
                && (levels.HasActiveClearRewards || levels.IsLost);
            bool canTurn = !busy && !interLevelPending && (levels == null || levels.CanAdvanceTurn);
            bool canSettle = !busy && !interLevelPending && (levels == null || !levels.HasLevels);
            bool canUndo = TurnManager.Instance != null && TurnManager.Instance.CanUndo && !busy && !interLevelPending
                && (levels == null || levels.Outcome != LevelOutcome.Lost);

            if (visitBusy)
                SetPlayHudVisible(false);
            else
            {
                SetPlayChromeVisible(!interLevelPending);
                ApplyReplacedOverlayVisibility(!interLevelPending);
            }

            if (_undoTurnButton != null)
                _undoTurnButton.interactable = canUndo;

            if (_nextTurnButton != null)
                _nextTurnButton.interactable = canTurn;

            if (_settleStageButton != null)
            {
                bool showSettle = !interLevelPending && (levels == null || !levels.HasLevels);
                _settleStageButton.gameObject.SetActive(showSettle);
                _settleStageButton.interactable = canSettle;
            }
        }

        private void BindLevelManager(bool bind)
        {
            var levels = LevelManager.Instance;
            if (levels == null) return;

            levels.LevelWon -= OnLevelWon;
            levels.LevelLost -= OnLevelLost;
            levels.LevelStarted -= OnLevelStarted;
            levels.CampaignCompleted -= OnCampaignCompleted;
            if (!bind) return;

            levels.LevelWon += OnLevelWon;
            levels.LevelLost += OnLevelLost;
            levels.LevelStarted += OnLevelStarted;
            levels.CampaignCompleted += OnCampaignCompleted;
        }

        private void OnLevelWon(LevelItem level)
        {
            string name = level != null ? level.DisplayName : "本关";
            var levels = LevelManager.Instance;
            int gained = levels != null ? levels.ScoreGainedInLevel : 0;
            int target = level != null ? level.TargetScore : 0;
            ShowToast($"{name} 通关！{gained}/{target} 分 → 进入关卡间", 2f);
        }

        private void OnLevelLost(LevelItem level)
        {
            string name = level != null ? level.DisplayName : "本关";
            var levels = LevelManager.Instance;
            int gained = levels != null ? levels.ScoreGainedInLevel : 0;
            int target = level != null ? level.TargetScore : 0;
            ShowToast($"{name} 失败：{gained}/{target} 分 → 结算页", 2f);
        }

        private void OnLevelStarted(LevelItem level)
        {
            if (level == null) return;
            ShowToast($"进入 {level.DisplayName}：第 1/{level.MaxTurns} 回合，目标 {level.TargetScore} 分", 3f);
        }

        private void OnCampaignCompleted()
        {
            ShowToast("游戏胜利！已完成全部关卡", 4f);
        }

        private void BindEventManager(bool bind)
        {
            var events = EventManager.Instance;
            if (events == null) return;

            events.EventPresented -= OnEventPresented;
            events.EventResolved -= OnEventResolved;
            events.PendingCleared -= OnEventPendingCleared;
            if (!bind) return;

            events.EventPresented += OnEventPresented;
            events.EventResolved += OnEventResolved;
            events.PendingCleared += OnEventPendingCleared;
        }

        private void OnEventPresented(EventItem _)
        {
            SetSettingsOpen(false);
            if (controlPanelHud != null && controlPanelHud.IsPanelOpen)
                controlPanelHud.SetPanelMode(false);
            if (_eventPanelUi == null)
            {
                _eventPanelUi = EventPanelUI.Ensure(transform);
                _eventPanelUi.SetToastHandler(msg => ShowToast(msg, 3.5f));
            }
        }

        private void OnEventResolved(EventItem _, int __)
        {
            var map = FindObjectOfType<JobWorldMap>();
            map?.RefreshLabels();
        }

        private void OnEventPendingCleared()
        {
            var map = FindObjectOfType<JobWorldMap>();
            map?.RefreshLabels();
        }

        /// <summary>
        /// Hide overlay HUD while the IMGUI control panel is open so uGUI buttons
        /// do not steal the Close click.
        /// </summary>
        private void SyncOverlayWithPanel()
        {
            bool panelOpen = controlPanelHud != null && controlPanelHud.IsPanelOpen;
            if (panelOpen && _settingsPanel != null && _settingsPanel.activeSelf)
                SetSettingsOpen(false);

            if (panelOpen == _overlayHiddenForPanel) return;
            _overlayHiddenForPanel = panelOpen;

            if (_overlayCanvas != null)
                _overlayCanvas.enabled = !panelOpen;
            if (_overlayRaycaster != null)
                _overlayRaycaster.enabled = !panelOpen;
        }

        public void ShowToast(string message, float seconds = 2.5f)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void RefreshInfo()
        {
            if (_infoText == null || !_infoText.gameObject.activeSelf) return;

            var store = ResourceStore.Instance;
            var elves = ElfManager.Instance;
            var turns = TurnManager.Instance;
            var zone = cameraController != null ? cameraController.CurrentZone : MapZoneType.Gather;

            if (_zoneText != null)
                _zoneText.text = ZoneLabel(zone);

            if (store == null)
            {
                _infoText.text = "资源未就绪";
                return;
            }

            int incentive = RelicManager.Instance != null
                ? RelicManager.Instance.CountOwnedId(RelicManager.IncentiveId)
                : 0;
            var em = EmployeeManager.Instance;
            int mushroom = em != null ? em.GetOwned(EmployeeManager.MushroomPersonId) : 0;
            int ghostOwned = em != null ? em.GetOwned(EmployeeManager.GhostId) : 0;
            int ghostFree = em != null && em.GhostType != null ? em.GetFree(em.GhostType) : 0;

            string levelLine = BuildLevelStatusLine();
            var levels = LevelManager.Instance;
            bool campaign = levels != null && levels.HasLevels;
            string flavorScores = turns != null
                ? turns.FormatFlavorScoreComposition()
                : "烹饪+0  热辣+0  酸涩+0  寒冷+0  鲜美+0";
            // 有关卡时以「当前回合号」为准（1..MaxTurns），避免与 TurnIndex（已完成数）混用。
            string turnHeader = campaign
                ? $"回合 {levels.LevelTurnIndex}/{levels.MaxTurns}   得分 {(turns != null ? turns.Score : 0)}   {flavorScores}"
                : $"回合 {(turns != null ? turns.TurnIndex : 0)}   阶段 {(turns != null ? turns.StageIndex : 1)}   总分 {(turns != null ? turns.Score : 0)}   {flavorScores}";

            _infoText.text =
                turnHeader + "\n" +
                levelLine +
                $"精灵 闲{(elves != null ? elves.FreeCount : 0)}/总{(elves != null ? elves.TotalCount : 0)}   " +
                $"蘑菇人 {mushroom}   幽灵 闲{ghostFree}/总{ghostOwned}   激励 {incentive}\n" +
                $"柔软 {store.Soft}  强韧 {store.Tough}  坚固 {store.Solid}\n" +
                $"热辣 {store.Spicy}  酸涩 {store.Sour}  寒冷 {store.Cold}  鲜美 {store.Magic}\n" +
                $"已处理 {store.Processed}  已烹饪 {store.Cooked}  本关烹饪 {(turns != null ? turns.StageCooked : 0)}\n" +
                $"仓库 {store.TotalRaw}/{(store.WarehouseCapacity <= 0 ? "∞" : store.WarehouseCapacity.ToString())}";
        }

        private static string BuildLevelStatusLine()
        {
            var levels = LevelManager.Instance;
            if (levels == null || !levels.HasLevels || levels.Current == null)
                return string.Empty;

            var level = levels.Current;
            string status = levels.Outcome switch
            {
                LevelOutcome.Won when levels.IsCampaignComplete => "游戏胜利",
                LevelOutcome.Won => "已通关·结算中",
                LevelOutcome.Lost => "失败",
                _ => "进行中"
            };

            return
                $"关卡 {level.DisplayName}  得分 {levels.ScoreGainedInLevel}/{level.TargetScore}  [{status}]\n";
        }

        /// <summary>Scene object you can parent Image / Text / buttons under. Never moved or destroyed at runtime.</summary>
        public Transform FreeDrawRoot
        {
            get
            {
                if (_overlayCanvas != null)
                    return FindNamed(_overlayCanvas.transform, FreeDrawName);
                var canvas = transform.Find(OverlayCanvasName);
                return canvas != null ? FindNamed(canvas, FreeDrawName) : null;
            }
        }

        /// <summary>
        /// Bind or create the play HUD canvas without destroying anything already in the scene.
        /// </summary>
        public void EnsureAuthoredCanvas(bool bindRuntimeListeners = true)
        {
            BuildCanvas(bindRuntimeListeners);
        }

        private void BuildCanvas(bool bindRuntimeListeners)
        {
            var canvasTf = transform.Find(OverlayCanvasName);
            if (canvasTf == null)
                canvasTf = CreateOverlayCanvasRoot();

            _overlayCanvas = canvasTf.GetComponent<Canvas>();
            if (canvasTf.GetComponent<GraphicRaycaster>() == null)
                canvasTf.gameObject.AddComponent<GraphicRaycaster>();
            _overlayRaycaster = canvasTf.GetComponent<GraphicRaycaster>();
            // AssetRipper / bad scene fixes sometimes put EventSystem on the canvas.
            StripMisplacedEventSystem(canvasTf.gameObject);
            EnsureEventSystem();
            EnsureFreeDrawMarker(EnsureStretchLayer(canvasTf, FreeDrawName, asFirst: true));
            var systemHud = EnsureStretchLayer(canvasTf, SystemHudName, asFirst: false);

            BindExistingWidgets(canvasTf);
            EnsureSystemWidgets(systemHud);
            StripLegacyRuntimePanels(canvasTf);
            EnsureAuthoredHud(FindNamed(canvasTf, FreeDrawName));

            if (bindRuntimeListeners)
                BindListeners(canvasTf);

            CachePlayChrome(canvasTf);
            ApplyReplacedOverlayVisibility(true);
            RefreshZoneChrome();
        }

        private static void EnsureAuthoredHud(Transform freeDraw)
        {
            if (freeDraw == null) return;
            var root = FindNamed(freeDraw, PlayAuthoredHud.RootName);
            if (root == null) return;
            if (root.GetComponent<PlayAuthoredHud>() == null)
                root.gameObject.AddComponent<PlayAuthoredHud>();
        }

        private Transform CreateOverlayCanvasRoot()
        {
            var canvasGo = new GameObject(OverlayCanvasName);
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

        private static void DestroyComponentSafe(Object component)
        {
            if (component == null) return;
            if (Application.isPlaying)
                Object.Destroy(component);
            else
                Object.DestroyImmediate(component);
        }

        private static void DestroyGameObjectSafe(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }

        private static void StripMisplacedEventSystem(GameObject host)
        {
            if (host == null) return;
            var misplaced = host.GetComponents<UnityEngine.EventSystems.EventSystem>();
            for (int i = 0; i < misplaced.Length; i++)
                DestroyComponentSafe(misplaced[i]);

            var modules = host.GetComponents<UnityEngine.EventSystems.BaseInputModule>();
            for (int i = 0; i < modules.Length; i++)
                DestroyComponentSafe(modules[i]);
        }

        private static void EnsureEventSystem()
        {
            var systems = FindObjectsOfType<UnityEngine.EventSystems.EventSystem>();
            UnityEngine.EventSystems.EventSystem keep = null;
            for (int i = 0; i < systems.Length; i++)
            {
                var es = systems[i];
                if (es == null) continue;
                // Prefer a dedicated EventSystem object, not one stuck on OverlayCanvas.
                if (es.gameObject.name == "EventSystem" || es.GetComponent<Canvas>() == null)
                {
                    keep = es;
                    break;
                }
            }

            if (keep == null && systems.Length > 0)
                keep = systems[0];

            if (keep == null)
            {
                var go = new GameObject("EventSystem");
                keep = go.AddComponent<UnityEngine.EventSystems.EventSystem>();
                go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                return;
            }

            if (keep.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
                keep.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            for (int i = 0; i < systems.Length; i++)
            {
                var es = systems[i];
                if (es == null || es == keep) continue;
                DestroyGameObjectSafe(es.gameObject);
            }
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

        private static PlayHudFreeDrawLayer EnsureFreeDrawMarker(Transform freeDraw)
        {
            if (freeDraw == null)
                return null;
            var marker = freeDraw.GetComponent<PlayHudFreeDrawLayer>();
            return marker != null ? marker : freeDraw.gameObject.AddComponent<PlayHudFreeDrawLayer>();
        }

        private void BindExistingWidgets(Transform canvas)
        {
            _infoText = FindHud<Text>(canvas, "InfoText");
            _zoneText = FindHud<Text>(canvas, "ZoneTitle");
            _toastText = FindHud<Text>(canvas, "Toast");
            _settingsPanel = FindNamed(canvas, "SettingsPanel")?.gameObject;
            _nextTurnButton = FindHud<Button>(canvas, "NextTurnBtn");
            _undoTurnButton = FindHud<Button>(canvas, "UndoTurnBtn");
            _settleStageButton = FindHud<Button>(canvas, "SettleStageBtn");
            _zonePrevButton = FindHud<Button>(canvas, "ZoneSidePrev");
            _zoneNextButton = FindHud<Button>(canvas, "ZoneSideNext");
            _zoneFrame = FindNamed(canvas, "ZoneFrame")?.gameObject;
        }

        private void EnsureSystemWidgets(Transform systemHud)
        {
            if (_toastText == null)
            {
                _toastText = CreateText(systemHud, "Toast",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f),
                    new Vector2(800f, 60f), 22, TextAnchor.LowerCenter, FontStyle.Normal);
                _toastText.color = new Color(1f, 0.92f, 0.55f);
            }

            if (FindNamed(systemHud.parent, "SettingsBtn") == null)
            {
                CreateButton(systemHud, "SettingsBtn", "设置",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f),
                    new Vector2(140f, 56f));
            }

            if (FindNamed(systemHud.parent, "ControlPanelBtn") == null)
            {
                CreateButton(systemHud, "ControlPanelBtn", "操控面板",
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-168f, -16f),
                    new Vector2(180f, 56f));
            }

            if (_nextTurnButton == null)
            {
                _nextTurnButton = CreateButton(systemHud, "NextTurnBtn", "下一回合",
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f),
                    new Vector2(200f, 64f));
            }

            if (_undoTurnButton == null)
            {
                _undoTurnButton = CreateButton(systemHud, "UndoTurnBtn", "撤回上一回合",
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-240f, 24f),
                    new Vector2(200f, 64f));
            }

            if (_settleStageButton == null)
            {
                _settleStageButton = CreateButton(systemHud, "SettleStageBtn", "大关结算",
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-456f, 24f),
                    new Vector2(200f, 64f));
            }

            if (_zonePrevButton == null || _zoneNextButton == null)
                BuildZoneSideButtons(systemHud);

            if (_settingsPanel == null)
                BuildSettingsPanel(systemHud);

            BindExistingWidgets(systemHud.parent);
        }

        private static void StripLegacyRuntimePanels(Transform canvas)
        {
            var legacyEvent = FindNamed(canvas, "EventPanel");
            if (legacyEvent != null)
                DestroyGo(legacyEvent.gameObject);

            var reward = FindNamed(canvas, "RewardPanel");
            if (reward != null)
                DestroyGo(reward.gameObject);
        }

        private static void DestroyGo(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        private void BindListeners(Transform canvas)
        {
            BindClick(FindHud<Button>(canvas, "SettingsBtn"), OnSettingsClicked);
            BindClick(FindHud<Button>(canvas, "ControlPanelBtn"), OnControlPanelClicked);
            BindClick(_nextTurnButton, OnNextTurnClicked);
            BindClick(_undoTurnButton, OnUndoClicked);
            BindClick(_settleStageButton, OnSettleClicked);
            BindClick(_zonePrevButton, OnZonePrevClicked);
            BindClick(_zoneNextButton, OnZoneNextClicked);
            BindClick(FindHud<Button>(canvas, "SaveBtn"), OnSaveClicked);
            BindClick(FindHud<Button>(canvas, "LoadBtn"), OnLoadClicked);
            BindClick(FindHud<Button>(canvas, "QuitBtn"), QuitGame);
            BindClick(FindHud<Button>(canvas, "CloseSettingsBtn"), OnCloseSettingsClicked);
        }

        private void OnSettingsClicked() => SetSettingsOpen(true);

        private void OnCloseSettingsClicked() => SetSettingsOpen(false);

        private void OnControlPanelClicked()
        {
            SetSettingsOpen(false);
            if (controlPanelHud != null)
                controlPanelHud.TogglePanelMode();
        }

        private void OnZonePrevClicked()
        {
            cameraController?.CycleZone(-1);
            ApplyZoneCamera();
            RefreshZoneChrome();
        }

        private void OnZoneNextClicked()
        {
            cameraController?.CycleZone(+1);
            ApplyZoneCamera();
            RefreshZoneChrome();
        }

        private void ApplyZoneCamera()
        {
            if (cameraController == null) return;

            var zone = cameraController.CurrentZone;
            switch (zone)
            {
                case MapZoneType.Gather when gatherZone != null:
                    cameraController.ConfigureZone(
                        zone,
                        gatherZone.RecommendedOrthographicSize(),
                        gatherZone.RecommendedCameraCenterY());
                    break;
                case MapZoneType.Process when processZone != null:
                    cameraController.ConfigureZone(
                        zone,
                        processZone.RecommendedOrthographicSize(),
                        processZone.RecommendedCameraCenterY());
                    break;
                case MapZoneType.Cook when cookZone != null:
                    float cookSize = cookZone.RecommendedOrthographicSize();
                    if (cookSize < 0.5f && gatherZone != null)
                        cookSize = gatherZone.RecommendedOrthographicSize();
                    cameraController.ConfigureZone(
                        zone,
                        cookSize,
                        cookZone.RecommendedCameraCenterY());
                    break;
            }

            cameraController.SetZone(zone);
        }

        private void OnSaveClicked()
        {
            GameSaveService.TrySave(out var msg);
            ShowToast(msg);
        }

        private void OnLoadClicked()
        {
            if (GameSaveService.TryLoad(out var msg))
            {
                ShowToast(msg);
                SetSettingsOpen(false);
            }
            else
            {
                ShowToast(msg);
            }
        }

        private void OnNextTurnClicked()
        {
            if (EventManager.Instance != null && EventManager.Instance.HasPendingEvent)
            {
                ShowToast("请先选择事件选项");
                return;
            }

            var levels = LevelManager.Instance;
            if (levels != null && !levels.CanAdvanceTurn)
            {
                if (levels.IsWon)
                    ShowToast(levels.IsCampaignComplete
                        ? "全部关卡已通关"
                        : "本关已通关，请在关卡间领取奖励");
                else if (levels.IsLost)
                    ShowToast("本关失败，请重置局重试");
                else
                    ShowToast("正在结算本关（酸涩换分）…");
                return;
            }

            var turns = TurnManager.Instance;
            if (turns == null)
            {
                ShowToast("TurnManager 未就绪");
                return;
            }

            var result = turns.NextTurn();
            ShowToast(result != null ? result.ToString() : "回合完成");
            var map = FindObjectOfType<JobWorldMap>();
            map?.RefreshLabels();
        }

        private void OnUndoClicked()
        {
            if (EventManager.Instance != null && EventManager.Instance.HasPendingEvent)
            {
                ShowToast("请先选择事件选项");
                return;
            }

            var turns = TurnManager.Instance;
            if (turns == null)
            {
                ShowToast("TurnManager 未就绪");
                return;
            }

            if (!turns.TryUndoPreviousTurn())
            {
                ShowToast("没有可撤回的回合");
                return;
            }

            ShowToast("已撤回上一回合");
            var map = FindObjectOfType<JobWorldMap>();
            map?.RefreshLabels();
        }

        private void OnSettleClicked()
        {
            if (EventManager.Instance != null && EventManager.Instance.HasPendingEvent)
            {
                ShowToast("请先选择事件选项");
                return;
            }

            var levels = LevelManager.Instance;
            if (levels != null && levels.HasLevels && !levels.CanSettleAndAdvance)
            {
                if (levels.IsLost)
                    ShowToast("本关已失败，无法结算");
                else if (levels.IsCampaignComplete)
                    ShowToast("全部关卡已通关");
                else
                    ShowToast($"尚未通关：还需 {levels.ScoreRemaining} 分");
                return;
            }

            var turns = TurnManager.Instance;
            if (turns == null)
            {
                ShowToast("TurnManager 未就绪");
                return;
            }

            var settle = turns.SettleStage();
            ShowToast(settle != null ? settle.ToString() : "大关已结算");
            var map = FindObjectOfType<JobWorldMap>();
            map?.RefreshLabels();
        }

        private static void BindClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static T FindHud<T>(Transform canvas, string name) where T : Component
        {
            var t = FindNamed(canvas, name);
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

        private void BuildZoneFrame(Transform parent)
        {
            var art = GetArt();
            var frame = new GameObject("ZoneFrame");
            frame.transform.SetParent(parent, false);
            frame.transform.SetAsFirstSibling();
            _zoneFrame = frame;
            var frameRect = frame.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            // Inset so the frame sits around the playable area, clear of corner HUD.
            const float insetL = 72f;
            const float insetR = 72f;
            const float insetT = 88f;
            const float insetB = 96f;
            const float thickness = 22f;

            CreateFrameBar(frame.transform, "Top", art != null ? art.DividerHorizontal : null,
                stretchX: true,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -insetT),
                new Vector2(-(insetL + insetR), thickness));

            CreateFrameBar(frame.transform, "Bottom", art != null ? art.DividerHorizontal : null,
                stretchX: true,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, insetB),
                new Vector2(-(insetL + insetR), thickness));

            CreateFrameBar(frame.transform, "Left", art != null ? art.DividerVertical : null,
                stretchX: false,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(insetL, 0f),
                new Vector2(thickness, -(insetT + insetB)));

            CreateFrameBar(frame.transform, "Right", art != null ? art.DividerVertical : null,
                stretchX: false,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-insetR, 0f),
                new Vector2(thickness, -(insetT + insetB)));
        }

        private static void CreateFrameBar(
            Transform parent,
            string name,
            Sprite sprite,
            bool stretchX,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            var image = go.AddComponent<Image>();
            if (sprite != null)
            {
                image.sprite = sprite;
                bool sliced = sprite.border.sqrMagnitude > 0.01f;
                image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                image.color = Color.white;
                image.preserveAspect = false;
            }
            else
            {
                image.sprite = GetUiSprite();
                image.type = Image.Type.Simple;
                image.color = new Color(0.75f, 0.78f, 0.85f, 0.55f);
            }

            image.raycastTarget = false;
            _ = stretchX;
        }

        private void BuildZoneSideButtons(Transform parent)
        {
            var art = GetArt();
            Sprite leftSprite = art != null ? art.ZoneSwitchLeft : null;
            Sprite rightSprite = art != null ? art.ZoneSwitchRight : null;
            // Match gather-zone world keys (~1.4×2.5 world → ~82×148 at 1080p).
            Vector2 switchSize = FitSpriteAspectSize(leftSprite != null ? leftSprite : rightSprite, 148f, 290f / 512f);

            if (_zonePrevButton == null)
            {
                _zonePrevButton = CreateImageButton(
                    parent,
                    "ZoneSidePrev",
                    leftSprite,
                    "‹",
                    new Vector2(0f, 0.65f),
                    new Vector2(0f, 0.65f),
                    new Vector2(0f, 0.5f),
                    new Vector2(28f, 0f),
                    switchSize,
                    null);
            }

            if (_zoneNextButton == null)
            {
                _zoneNextButton = CreateImageButton(
                    parent,
                    "ZoneSideNext",
                    rightSprite,
                    "›",
                    new Vector2(1f, 0.65f),
                    new Vector2(1f, 0.65f),
                    new Vector2(1f, 0.5f),
                    new Vector2(-28f, 0f),
                    switchSize,
                    null);
            }
        }

        private void RefreshZoneChrome()
        {
            if (_zoneText != null && cameraController != null)
                _zoneText.text = ZoneLabel(cameraController.CurrentZone);
            RefreshZoneSwitchButtons(playActive: true);
        }

        /// <summary>
        /// Hide prototype overlay chrome that the authored gather scene already covers
        /// (resource dump, zone title, frame). Zone page buttons stay on every area,
        /// except the dead ends: no left on Gather, no right on Cook.
        /// </summary>
        private void ApplyReplacedOverlayVisibility(bool playActive)
        {
            bool showPrototype = playActive && !UsingZoneArtHud;
            SetActiveIfChanged(_infoText != null ? _infoText.gameObject : null, showPrototype);
            SetActiveIfChanged(_zoneText != null ? _zoneText.gameObject : null, showPrototype);
            SetActiveIfChanged(_zoneFrame, showPrototype);

            RefreshZoneSwitchButtons(playActive);
        }

        private void RefreshZoneSwitchButtons(bool playActive)
        {
            var zone = cameraController != null ? cameraController.CurrentZone : MapZoneType.Gather;
            bool showPrev = playActive && _zonePrevButton != null && zone != MapZoneType.Gather;
            bool showNext = playActive && _zoneNextButton != null && zone != MapZoneType.Cook;
            SetActiveIfChanged(_zonePrevButton != null ? _zonePrevButton.gameObject : null, showPrev);
            SetActiveIfChanged(_zoneNextButton != null ? _zoneNextButton.gameObject : null, showNext);

            // World-space gather keys (if any): never show the left dead-end key.
            bool useWorldSwitches = playActive && _zonePrevButton == null && _zoneNextButton == null;
            gatherZone?.SetWorldSwitchVisible(useWorldSwitches);
        }

        private static Button CreateImageButton(
            Transform parent,
            string name,
            Sprite sprite,
            string fallbackLabel,
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
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }
            else
            {
                image.sprite = GetUiSprite();
                image.color = new Color(0.18f, 0.22f, 0.30f, 0.95f);
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.75f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.52f, 0.65f);
            button.colors = colors;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            if (sprite == null)
            {
                var textGo = new GameObject("Label");
                textGo.transform.SetParent(go.transform, false);
                var textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                var text = textGo.AddComponent<Text>();
                text.font = GetUiFont();
                text.fontSize = 42;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = fallbackLabel;
                text.raycastTarget = false;
            }

            return button;
        }

        private void CachePlayChrome(Transform canvasRoot)
        {
            _playChrome.Clear();
            if (canvasRoot == null) return;

            string[] names =
            {
                "NextTurnBtn", "UndoTurnBtn", "ControlPanelBtn", PlayAuthoredHud.RootName
            };
            for (int i = 0; i < names.Length; i++)
            {
                var child = FindNamed(canvasRoot, names[i]);
                if (child != null)
                    _playChrome.Add(child.gameObject);
            }
        }

        private void SetPlayChromeVisible(bool visible)
        {
            for (int i = 0; i < _playChrome.Count; i++)
            {
                var go = _playChrome[i];
                if (go != null)
                    go.SetActive(visible);
            }
        }

        private static void SetActiveIfChanged(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private void BuildSettingsPanel(Transform parent)
        {
            var panelGo = new GameObject("SettingsPanel");
            panelGo.transform.SetParent(parent, false);
            _settingsPanel = panelGo;

            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var dim = panelGo.AddComponent<Image>();
            dim.sprite = GetUiSprite();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.raycastTarget = true;

            var box = new GameObject("Box");
            box.transform.SetParent(panelGo.transform, false);
            var boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(420f, 360f);
            var boxImage = box.AddComponent<Image>();
            boxImage.sprite = GetUiSprite();
            boxImage.color = new Color(0.14f, 0.17f, 0.24f, 0.98f);

            var title = CreateText(box.transform, "SettingsTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                new Vector2(360f, 40f), 28, TextAnchor.UpperCenter, FontStyle.Bold);
            title.text = "设置";
            title.alignment = TextAnchor.MiddleCenter;

            float y = -90f;
            CreatePanelButton(box.transform, "SaveBtn", "保存进度", new Vector2(0f, y));
            y -= 70f;
            CreatePanelButton(box.transform, "LoadBtn", "读取进度", new Vector2(0f, y));
            y -= 70f;
            CreatePanelButton(box.transform, "QuitBtn", "退出游戏", new Vector2(0f, y));
            y -= 70f;
            CreatePanelButton(box.transform, "CloseSettingsBtn", "关闭", new Vector2(0f, y));

            panelGo.SetActive(false);
        }

        private void SetSettingsOpen(bool open)
        {
            if (_settingsPanel == null) return;
            _settingsPanel.SetActive(open);
            if (open && controlPanelHud != null && controlPanelHud.IsPanelOpen)
                controlPanelHud.SetPanelMode(false);
        }

        /// <summary>Show or hide the in-run HUD (used by the title menu).</summary>
        public void SetPlayHudVisible(bool visible)
        {
            if (_overlayCanvas == null) return;
            _overlayCanvas.gameObject.SetActive(visible);
            if (visible)
            {
                _overlayCanvas.enabled = true;
                if (_overlayRaycaster != null)
                    _overlayRaycaster.enabled = true;
                _overlayHiddenForPanel = false;
            }
            else
            {
                SetSettingsOpen(false);
                if (controlPanelHud != null)
                    controlPanelHud.SetPanelMode(false);
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void CreatePanelButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos)
        {
            CreateButton(parent, name, label,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), anchoredPos,
                new Vector2(280f, 56f));
        }

        private static string ZoneLabel(MapZoneType zone)
        {
            switch (zone)
            {
                case MapZoneType.Gather: return "采集";
                case MapZoneType.Process: return "处理";
                case MapZoneType.Cook: return "烹饪";
                default: return zone.ToString();
            }
        }

        public static Font SharedUiFont()
        {
            return GetUiFont();
        }

        public static Sprite SharedUiSprite()
        {
            return GetUiSprite();
        }

        public static Sprite SharedButtonSprite()
        {
            return GetButtonSprite();
        }

        /// <summary>
        /// Art 按钮.png native aspect (512×190). Used when sizing UI so the bevel
        /// isn't crushed; width may still stretch via 9-slice.
        /// </summary>
        public const float ArtButtonAspect = 512f / 190f;

        /// <summary>Title-screen image button (text is baked into the sprite).</summary>
        public static void ApplyTitleImageButtonStyle(Image image, Button button, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite != null ? sprite : GetButtonSprite();
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            if (button == null) return;

            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.82f, 1f);
            colors.pressedColor = new Color(0.82f, 0.70f, 0.42f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.58f, 0.55f, 0.50f, 0.72f);
            button.colors = colors;
        }

        /// <summary>Apply art 按钮.png chrome to a UI Image/Button (replaces solid-color placeholders).</summary>
        public static void ApplyArtButtonStyle(Image image, Button button = null)
        {
            if (image == null) return;
            var sprite = GetButtonSprite();
            image.sprite = sprite;
            // 9-slice keeps corner bevels when width/height differ from art aspect.
            bool sliced = sprite != null && sprite.border.sqrMagnitude > 0.01f;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = !sliced;
            if (button == null) return;

            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.78f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.58f, 0.7f);
            button.colors = colors;
        }

        /// <summary>
        /// Fit a text-button rect to the art chrome. Keeps intended width; clamps
        /// height into a range that reads well with 按钮.png (taller card rows pass through).
        /// </summary>
        public static Vector2 FitArtButtonSize(float width, float height)
        {
            const float minH = 52f;
            const float maxH = 100f;
            // Tall card rows (shop relics etc.) keep authored height.
            if (height >= 120f)
                return new Vector2(width, height);

            float h = Mathf.Clamp(height, minH, maxH);
            // Ultra-wide CTAs: bump height so the bevel doesn't look paper-thin.
            // Never shrink an already-taller authored height.
            if (width > 1f && width / h > 5.5f)
                h = Mathf.Max(h, Mathf.Min(maxH, width / 5.2f));
            // Very narrow: don't go below ~natural aspect height for that width.
            if (width > 1f && width / h < 1.8f)
                h = Mathf.Max(minH, width / ArtButtonAspect);
            return new Vector2(width, h);
        }

        /// <summary>Size a preserve-aspect image button from sprite ratio and target height.</summary>
        public static Vector2 FitSpriteAspectSize(Sprite sprite, float targetHeight, float fallbackAspect = 0.6f)
        {
            float aspect = fallbackAspect;
            if (sprite != null && sprite.rect.height > 0.01f)
                aspect = sprite.rect.width / sprite.rect.height;
            return new Vector2(targetHeight * aspect, targetHeight);
        }

        private static bool UsesArtButton(Transform buttonRoot)
        {
            if (buttonRoot == null) return false;
            var image = buttonRoot.GetComponent<Image>();
            var art = GetArt();
            return art != null
                   && art.ButtonBackground != null
                   && image != null
                   && image.sprite == art.ButtonBackground
                   && image.type == Image.Type.Sliced;
        }

        private static GameArtLibrary GetArt()
        {
            if (_art != null) return _art;
            _art = GameArtLibrary.Load();
            return _art;
        }

        private static Font GetUiFont()
        {
            if (_uiFont != null && SafeUiFont.IsUsable(_uiFont))
                return _uiFont;
            _uiFont = SafeUiFont.Get(24);
            return _uiFont;
        }

        private static Sprite GetUiSprite()
        {
            if (_uiSprite != null) return _uiSprite;
            var tex = Texture2D.whiteTexture;
            _uiSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _uiSprite;
        }

        private static Sprite GetButtonSprite()
        {
            var art = GetArt();
            if (art != null && art.ButtonBackground != null)
                return art.ButtonBackground;
            return GetUiSprite();
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize,
            TextAnchor anchor,
            FontStyle style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            if (anchorMin.x > 0.5f)
                rect.pivot = new Vector2(1f, anchorMin.y > 0.5f ? 1f : 0f);
            else if (Mathf.Approximately(anchorMin.x, 0.5f) && Mathf.Approximately(anchorMin.y, 1f))
                rect.pivot = new Vector2(0.5f, 1f);
            else if (Mathf.Approximately(anchorMin.x, 0.5f) && Mathf.Approximately(anchorMin.y, 0f))
                rect.pivot = new Vector2(0.5f, 0f);
            else
                rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = GetUiFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;

            if (name == "InfoText")
            {
                var bg = new GameObject("InfoBg");
                bg.transform.SetParent(go.transform, false);
                bg.transform.SetAsFirstSibling();
                var bgRect = bg.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = new Vector2(-10f, -10f);
                bgRect.offsetMax = new Vector2(10f, 10f);
                var image = bg.AddComponent<Image>();
                image.sprite = GetUiSprite();
                image.type = Image.Type.Simple;
                image.color = new Color(0f, 0f, 0f, 0.55f);
                image.raycastTarget = false;
            }

            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            if (anchorMin.x > 0.5f)
                rect.pivot = new Vector2(1f, anchorMin.y > 0.5f ? 1f : 0f);
            else if (Mathf.Approximately(anchorMin.x, 0.5f) && Mathf.Approximately(anchorMin.y, 1f))
                rect.pivot = new Vector2(0.5f, 1f);
            else if (Mathf.Approximately(anchorMin.x, 0.5f))
                rect.pivot = new Vector2(0.5f, 0f);
            else
                rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = FitArtButtonSize(size.x, size.y);

            var image = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            ApplyArtButtonStyle(image, button);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = GetUiFont();
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return button;
        }
    }
}
