using Soup.Events;
using Soup.Jobs;
using Soup.Levels;
using UnityEngine;
using UnityEngine.UI;

namespace Soup.Game
{
    /// <summary>
    /// 关卡间独立界面（单独场景）：通关 hub / 失败页。
    /// 不叠在玩法 HUD 上。
    /// </summary>
    public sealed class InterLevelUI : MonoBehaviour
    {
        public const string CanvasName = "InterLevelCanvas";
        public const string FreeDrawName = "FreeDraw";
        public const string SystemHudName = "SystemHud";

        private GameObject _root;
        private Transform _authoredRoot;
        private GameObject _mainRoot;
        private GameObject _failRoot;
        private GameObject _victoryRoot;
        private Text _titleText;
        private Text _hintText;
        private Text _failTitleText;
        private Text _failBodyText;
        private Text _failScoreText;
        private Text _victoryTitleText;
        private Text _victoryBodyText;
        private Text _victoryScoreText;
        private Text _levelScoreText;
        private Text _toastText;
        private string _toast = string.Empty;
        private float _toastUntil;

        private Button _elfButton;
        private Text _elfButtonLabel;
        private Button _warehouseButton;
        private Text _warehouseButtonLabel;
        private Button _shopButton;
        private Text _shopButtonLabel;
        private Button _eventButton0;
        private Text _eventButton0Label;
        private Button _eventButton1;
        private Text _eventButton1Label;
        private Button _advanceGatherButton;
        private Button _advanceProcessButton;
        private Button _advanceCookButton;
        private Button _proceedButton;
        private Text _proceedButtonLabel;
        private Button _failRetryButton;
        private Button _failMenuButton;
        private Button _victoryMenuButton;

        private bool _leaving;

        /// <summary>Screen-space anchor Y for the level score readout (upper quarter, centered).</summary>
        private const float LevelScoreAnchorY = 0.75f;

        public Transform FreeDrawRoot
        {
            get
            {
                if (_root != null)
                    return FindNamed(_root.transform, FreeDrawName);
                var canvas = transform.Find(CanvasName);
                return canvas != null ? FindNamed(canvas, FreeDrawName) : null;
            }
        }

        private void Awake()
        {
            EnsureAuthoredCanvas(rebuildSystemUi: true);
        }

        private void OnEnable()
        {
            var levels = LevelManager.Instance;
            if (levels != null)
            {
                levels.ClearRewardsChanged -= OnRewardsChanged;
                levels.ClearRewardsChanged += OnRewardsChanged;
                levels.Changed -= OnLevelsChanged;
                levels.Changed += OnLevelsChanged;
            }

            var events = EventManager.Instance;
            if (events != null)
            {
                events.StageEventBatchCompleted -= OnStageEventsCompleted;
                events.StageEventBatchCompleted += OnStageEventsCompleted;
                events.PendingCleared -= OnEventPendingCleared;
                events.PendingCleared += OnEventPendingCleared;
            }

            var panel = EventPanelUI.Ensure(transform);
            panel.SetToastHandler(msg => ShowToast(msg, 3f));

            ShopPanelUI.Ensure(transform).SetToastHandler(msg => ShowToast(msg, 3f));

            Refresh();
        }

        private void OnDisable()
        {
            var levels = LevelManager.Instance;
            if (levels != null)
            {
                levels.ClearRewardsChanged -= OnRewardsChanged;
                levels.Changed -= OnLevelsChanged;
            }

            var events = EventManager.Instance;
            if (events != null)
            {
                events.StageEventBatchCompleted -= OnStageEventsCompleted;
                events.PendingCleared -= OnEventPendingCleared;
            }
        }

        private void Update()
        {
            if (_toastText != null)
                _toastText.text = Time.unscaledTime <= _toastUntil ? _toast : string.Empty;

            // 若状态已不需要本页（例如外部重置），回玩法或主菜单。
            if (_leaving) return;
            var levels = LevelManager.Instance;
            if (levels == null) return;
            if (levels.HasActiveClearRewards || levels.IsLost || levels.IsCampaignComplete) return;
            if (levels.IsInProgress)
                LeaveToPlay();
            else if (levels.IsWon && !levels.HasActiveClearRewards && !levels.IsCampaignComplete)
            {
                // 异常中间态（已通关但奖励会话未激活且未推进下一关）：
                // 兜底推进，避免玩家卡在关卡间页面无法前进。
                if (!levels.TryAdvanceToNextLevel())
                    LeaveToPlay();
            }
        }

        private void OnRewardsChanged() => Refresh();
        private void OnLevelsChanged() => Refresh();
        private void OnStageEventsCompleted() => Refresh();
        private void OnEventPendingCleared() => Refresh();

        public void ShowToast(string message, float seconds = 2.5f)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void Refresh()
        {
            if (_root == null) return;

            var levels = LevelManager.Instance;
            var session = levels != null ? levels.ClearRewards : null;
            bool showRewards = session != null && session.IsActive;
            bool showVictory = levels != null && levels.IsCampaignComplete && !showRewards;
            bool showFail = levels != null && levels.IsLost && !showRewards && !showVictory;

            // Authored hub art stays visible; programmatic MainHub toggles with reward state.
            if (_authoredRoot != null)
                _authoredRoot.gameObject.SetActive(true);
            if (_mainRoot != null)
                _mainRoot.SetActive(showRewards && _authoredRoot == null);
            if (_failRoot != null) _failRoot.SetActive(showFail);
            if (_victoryRoot != null) _victoryRoot.SetActive(showVictory);

            // Dim interactive authored buttons when not in reward hub.
            SetAuthoredInteractable(showRewards && !_leaving);

            if (showFail)
                RefreshFail(levels);
            if (showVictory)
                RefreshVictory(levels);

            RefreshLevelScoreCaption(levels, showRewards);

            if (!showRewards) return;

            string levelName = levels.Current != null ? levels.Current.DisplayName : "本关";
            if (_titleText != null)
                _titleText.text = "关卡间";
            if (_hintText != null)
                _hintText.text = $"{levelName} 通关 · 领取奖励后可进入下一关";

            SetClaimButton(_elfButton, _elfButtonLabel, "小精灵增加", session.ElvesClaimed);
            SetClaimButton(_warehouseButton, _warehouseButtonLabel, "仓库扩容", session.WarehouseClaimed);
            RefreshClaimHoverTooltips(session);

            RefreshAdvanceButton(_advanceGatherButton, MapZoneType.Gather, session);
            RefreshAdvanceButton(_advanceProcessButton, MapZoneType.Process, session);
            RefreshAdvanceButton(_advanceCookButton, MapZoneType.Cook, session);
            RefreshEventButtons(session);
            RefreshShopButton(session);

            bool canProceed = session.HubRewardsClaimed;
            if (_proceedButton != null)
                _proceedButton.interactable = canProceed && !_leaving;
            if (_proceedButtonLabel != null)
                _proceedButtonLabel.text = canProceed ? "进入下一关" : "请先领取精灵与仓库";
        }

        private void RefreshClaimHoverTooltips(LevelClearRewardSession session)
        {
            BindClaimHover(
                _elfButton,
                "小精灵增加",
                () =>
                {
                    int n = LevelClearRewardSession.ResolveElfRewardCount();
                    if (session != null && session.ElvesClaimed)
                        return $"本关已领取。\n通关奖励为小精灵 ×{n}。";
                    return $"点击领取通关奖励：获得小精灵 ×{n}。";
                });

            BindClaimHover(
                _warehouseButton,
                "仓库扩容",
                () =>
                {
                    int bonus = LevelClearRewardSession.WarehouseBonusAmount;
                    var store = ResourceStore.Instance;
                    string cap = store != null
                        ? (store.WarehouseCapacity <= 0 ? "不限" : store.WarehouseCapacity.ToString())
                        : "—";
                    if (session != null && session.WarehouseClaimed)
                        return $"本关已领取。\n仓库上限 +{bonus}（当前 {cap}）。";
                    return $"点击领取通关奖励：仓库上限 +{bonus}。\n当前仓库上限：{cap}";
                });
        }

        private static void BindClaimHover(Button button, string title, System.Func<string> body)
        {
            if (button == null) return;
            var tip = button.GetComponent<UiHoverTooltip>();
            if (tip == null)
                tip = button.gameObject.AddComponent<UiHoverTooltip>();
            tip.Bind(() => title, body);
        }

        private void RefreshVictory(LevelManager levels)
        {
            int total = 0;
            if (levels != null && levels.Database != null)
                total = levels.Database.GetOrdered().Count;
            if (total <= 0)
                total = levels != null ? levels.LevelsClearedCount : 0;

            if (_victoryTitleText != null)
                _victoryTitleText.text = "游戏胜利！";
            if (_victoryScoreText != null)
                _victoryScoreText.text = levels != null
                    ? levels.LastFinishedScore.ToString()
                    : "0";
            if (_victoryBodyText != null)
                _victoryBodyText.text = $"已完成全部 {total} 关\n恭喜通关！";
        }

        private void RefreshEventButtons(LevelClearRewardSession session)
        {
            bool claimed = session != null && session.EventsClaimed;
            bool busy = EventManager.Instance != null &&
                        (EventManager.Instance.HasPendingEvent || EventManager.Instance.HasStageEventBatch);
            bool canOpen = !_leaving && session != null && session.IsActive && !claimed;

            SetEventButton(_eventButton0, _eventButton0Label, 0, canOpen, claimed, busy);
            SetEventButton(_eventButton1, _eventButton1Label, 1, canOpen, claimed, busy);
        }

        private void RefreshShopButton(LevelClearRewardSession session)
        {
            bool purchased = session != null && session.ShopClaimed;
            bool shopOpen = IsShopLevel(session);
            bool eventBusy = EventManager.Instance != null &&
                             (EventManager.Instance.HasPendingEvent || EventManager.Instance.HasStageEventBatch);
            bool canOpen = !_leaving && shopOpen && !purchased && !eventBusy;
            if (_shopButton != null)
                _shopButton.interactable = canOpen;
            if (_shopButtonLabel != null)
            {
                if (!shopOpen)
                {
                    int interval = ResolveShopIntervalLevels();
                    _shopButtonLabel.text = interval > 1
                        ? $"商店（每{interval}关）"
                        : "商店（本关未开启）";
                }
                else
                    _shopButtonLabel.text = purchased ? "商店（已购买）" : "商店";
            }
        }

        private static int ResolveShopIntervalLevels()
        {
            var config = ResolveGameConfig();
            return config != null ? config.ShopIntervalLevels : 2;
        }

        private static GameConfig ResolveGameConfig()
        {
            var config = ResourceStore.Instance != null ? ResourceStore.Instance.Config : null;
            return config != null
                ? config
                : Resources.Load<GameConfig>(ResourceStore.ResourcesConfigPath);
        }

        private static bool IsShopLevel(LevelClearRewardSession session)
        {
            if (session == null || !session.IsActive) return false;
            var config = ResolveGameConfig();
            return config != null && config.IsShopLevel(session.LevelsClearedAtStart);
        }

        private static void SetEventButton(
            Button button,
            Text label,
            int index,
            bool canOpen,
            bool claimed,
            bool busy)
        {
            if (button != null)
                button.interactable = canOpen;
            if (label == null) return;
            if (claimed)
                label.text = $"事件 {index + 1}（已完成）";
            else if (busy)
                label.text = $"事件 {index + 1}（进行中）";
            else
                label.text = $"事件 {index + 1}";
        }

        private void RefreshAdvanceButton(Button button, MapZoneType zone, LevelClearRewardSession session)
        {
            if (button == null || session == null) return;
            bool canEnter = !_leaving && AdvancementVisit.CanEnter(zone, session);
            button.interactable = canEnter;

            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label == null) return;

            string zoneName = AdvancementVisit.ZoneDisplayName(zone);
            int charge = AdvancementVisit.ChargeFor(zone, session);
            if (canEnter)
            {
                label.text = $"{zoneName}进阶";
                return;
            }

            if (session.AdvancementClaimed || charge <= 0 && WasSpentThisSession(zone, session))
            {
                label.text = $"{zoneName}进阶（已完成）";
                return;
            }

            // 烹饪：本关按规则不发放次数
            if (zone == MapZoneType.Cook
                && !JobProgressionRules.GrantsCookAdvanceOnClear(session.LevelsClearedAtStart))
            {
                label.text = $"{zoneName}进阶（每两关一次）";
                return;
            }

            label.text = $"{zoneName}进阶（已完成）";
        }

        private static bool WasSpentThisSession(MapZoneType zone, LevelClearRewardSession session)
        {
            if (session == null || !session.IsActive) return false;
            // 本应发放却为 0 → 已用完；本就不发（烹饪奇数关）→ 不算「已完成」。
            JobProgressionRules.AdvanceChargesForClear(
                session.LevelsClearedAtStart,
                out int gather,
                out int process,
                out int cook);
            switch (zone)
            {
                case MapZoneType.Gather: return gather > 0 && session.GatherCharges <= 0;
                case MapZoneType.Process: return process > 0 && session.ProcessCharges <= 0;
                case MapZoneType.Cook: return cook > 0 && session.CookCharges <= 0;
                default: return false;
            }
        }

        private void RefreshFail(LevelManager levels)
        {
            string levelName = levels.Current != null ? levels.Current.DisplayName : "本关";
            int gained = levels.LastFinishedScore;
            int target = levels.TargetScore;
            if (_failTitleText != null)
                _failTitleText.text = $"{levelName} 失败";
            if (_failScoreText != null)
                _failScoreText.text = gained.ToString();
            if (_failBodyText != null)
            {
                var level = levels.Current;
                string challenge = level != null && level.HasChallengeScore
                    ? $"\n挑战 {level.ChallengeScore} 分"
                    : string.Empty;
                string ultimate = level != null && level.HasUltimateChallengeScore
                    ? $"\n终极挑战 {level.UltimateChallengeScore} 分"
                    : string.Empty;
                _failBodyText.text = $"第 {levels.MaxTurns} 回合已结束\n目标 {target} 分{challenge}{ultimate}";
            }
        }

        private void RefreshLevelScoreCaption(LevelManager levels, bool visible)
        {
            if (_levelScoreText == null) return;
            ApplyLevelScoreCaptionLayout();
            _levelScoreText.gameObject.SetActive(visible);
            if (!visible || levels == null)
                return;

            _levelScoreText.text = FormatLevelSettlementScore(levels);
        }

        private void ApplyLevelScoreCaptionLayout()
        {
            if (_levelScoreText == null) return;
            var rt = _levelScoreText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, LevelScoreAnchorY);
            rt.anchorMax = new Vector2(0.5f, LevelScoreAnchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static string FormatLevelSettlementScore(LevelManager levels)
        {
            int score = levels.LastFinishedScore;
            var level = levels.Current;
            if (level != null)
                return level.FormatSettlementCaption(score);
            return $"本关得分\n<size=64>{score}</size>";
        }

        private static void SetClaimButton(Button button, Text label, string readyText, bool claimed)
        {
            if (button != null)
                button.interactable = !claimed;
            if (label != null)
                label.text = claimed ? "已领取" : readyText;
        }

        private void OnElfClicked()
        {
            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive) return;
            session.TryClaimElves(out var msg);
            ShowToast(msg);
            Refresh();
        }

        private void OnWarehouseClicked()
        {
            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive) return;
            session.TryClaimWarehouse(out var msg);
            ShowToast(msg);
            Refresh();
        }

        private void OnShopClicked()
        {
            if (_leaving) return;
            var session = LevelManager.Instance?.ClearRewards;
            if (!IsShopLevel(session))
            {
                int interval = ResolveShopIntervalLevels();
                ShowToast(interval > 1
                    ? $"商店每 {interval} 关开启一次"
                    : "本关通关后商店未开启");
                Refresh();
                return;
            }

            var shop = ShopPanelUI.Ensure(transform);
            shop.SetToastHandler(msg => ShowToast(msg, 3f));
            shop.SetClosedHandler(Refresh);
            if (session != null && session.ShopClaimed)
            {
                ShowToast("本关商店已购买");
                Refresh();
                return;
            }

            if (!shop.Show(animate: true))
                Refresh();
        }

        private void OnEventClicked(int _)
        {
            if (_leaving) return;
            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive)
            {
                ShowToast("关卡间奖励未激活");
                return;
            }

            if (session.EventsClaimed)
            {
                ShowToast("本关事件已完成");
                Refresh();
                return;
            }

            var panel = EventPanelUI.Ensure(transform);
            panel.SetToastHandler(msg => ShowToast(msg, 3f));

            var events = EventManager.Instance;
            if (events != null && events.HasPendingEvent)
            {
                panel.Show(animate: true);
                Refresh();
                return;
            }

            if (!session.TryClaimEvents(out var msg))
            {
                ShowToast(msg);
                Refresh();
                return;
            }

            ShowToast(msg);
            if (events != null && events.HasPendingEvent)
                panel.Show(animate: true);
            Refresh();
        }

        private void OnAdvanceClicked(MapZoneType zone)
        {
            if (_leaving) return;
            var session = LevelManager.Instance?.ClearRewards;
            if (!AdvancementVisit.CanEnter(zone, session))
            {
                if (zone == MapZoneType.Cook
                    && session != null
                    && !session.AdvancementClaimed
                    && !JobProgressionRules.GrantsCookAdvanceOnClear(session.LevelsClearedAtStart))
                    ShowToast("烹饪进阶每两关一次，本关不可用");
                else
                    ShowToast($"{AdvancementVisit.ZoneDisplayName(zone)}本关已无法再进阶");
                Refresh();
                return;
            }

            AdvancementVisit.Begin(zone);
            // 进阶入口：无溶解转场（IsActive 跨场景保留，LaunchRunner 优先认 IsActive）。
            LeaveToPlay(useDissolve: false);
        }

        private void OnProceedClicked()
        {
            if (_leaving) return;
            var session = LevelManager.Instance?.ClearRewards;
            if (session == null || !session.IsActive) return;

            if (!session.TryProceedToNextLevel(out var msg))
            {
                ShowToast(msg);
                Refresh();
                return;
            }

            var levels = LevelManager.Instance;
            if (levels != null && levels.IsCampaignComplete)
            {
                ShowToast("游戏胜利！", 3f);
                Refresh();
                return;
            }

            ShowToast(msg, 2f);
            LeaveToPlay(useDissolve: true);
        }

        private void OnFailRetryClicked()
        {
            if (_leaving || BlockDissolveTransition.IsBusy) return;
            _leaving = true;
            // 与主菜单「开始游戏」一致：新局 + 玩法内采集/处理初始岗位选择。
            GameSessionLaunch.RequestNewGame();
        }

        private void OnFailMenuClicked()
        {
            if (_leaving) return;
            _leaving = true;
            GameSessionLaunch.ReturnToMainMenu();
        }

        private void OnVictoryMenuClicked()
        {
            if (_leaving) return;
            _leaving = true;
            GameSessionLaunch.ReturnToMainMenu();
        }

        private void LeaveToPlay(bool useDissolve = true)
        {
            if (_leaving) return;
            _leaving = true;
            if (useDissolve)
                GameSessionLaunch.ReturnToPlay(useDissolve: true);
            else
                GameSessionLaunch.ReturnToPlayForAdvancement();
        }

        /// <summary>
        /// Create/bind InterLevelCanvas + FreeDraw + SystemHud.
        /// FreeDraw is never wiped — place authored art there in the Editor.
        /// </summary>
        public void EnsureAuthoredCanvas(bool rebuildSystemUi)
        {
            var canvasTf = transform.Find(CanvasName);
            if (canvasTf == null)
                canvasTf = CreateCanvasRoot();

            _root = canvasTf.gameObject;
            EnsureEventSystem();
            EnsureStretchLayer(canvasTf, FreeDrawName, asFirst: true);
            EnsureFreeDrawMarker(FindNamed(canvasTf, FreeDrawName));
            EnsureStretchLayer(canvasTf, SystemHudName, asFirst: false);

            if (rebuildSystemUi)
                RebuildSystemHud(canvasTf);
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

        private static InterLevelFreeDrawLayer EnsureFreeDrawMarker(Transform freeDraw)
        {
            if (freeDraw == null)
                return null;
            var marker = freeDraw.GetComponent<InterLevelFreeDrawLayer>();
            return marker != null ? marker : freeDraw.gameObject.AddComponent<InterLevelFreeDrawLayer>();
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindNamed(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void RebuildSystemHud(Transform canvasTf)
        {
            var systemHud = canvasTf.Find(SystemHudName);
            if (systemHud == null)
                systemHud = EnsureStretchLayer(canvasTf, SystemHudName, asFirst: false);

            // Wipe only SystemHud — keep FreeDraw authored art intact.
            for (int i = systemHud.childCount - 1; i >= 0; i--)
                DestroyImmediateSafe(systemHud.GetChild(i).gameObject);

            _authoredRoot = null;
            _mainRoot = null;
            ClearButtonRefs();

            var freeDraw = canvasTf.Find(FreeDrawName);
            if (freeDraw != null)
            {
                var authored = freeDraw.Find("AuthoredHub");
                if (authored != null && TryBindAuthoredHub(authored))
                    _authoredRoot = authored;
            }

            if (_authoredRoot == null)
                BuildProgrammaticMainHub(systemHud, freeDraw);

            BuildFailVictoryToast(systemHud);
        }

        private void ClearButtonRefs()
        {
            _elfButton = null;
            _elfButtonLabel = null;
            _warehouseButton = null;
            _warehouseButtonLabel = null;
            _shopButton = null;
            _shopButtonLabel = null;
            _eventButton0 = null;
            _eventButton0Label = null;
            _eventButton1 = null;
            _eventButton1Label = null;
            _advanceGatherButton = null;
            _advanceProcessButton = null;
            _advanceCookButton = null;
            _proceedButton = null;
            _proceedButtonLabel = null;
            _titleText = null;
            _hintText = null;
            _levelScoreText = null;
        }

        private bool TryBindAuthoredHub(Transform authored)
        {
            if (authored == null) return false;

            _elfButton = BindAuthoredButton(authored, "ElfBtn", OnElfClicked, out _elfButtonLabel);
            _warehouseButton = BindAuthoredButton(authored, "WarehouseBtn", OnWarehouseClicked, out _warehouseButtonLabel);
            _shopButton = BindAuthoredButton(authored, "ShopBtn", OnShopClicked, out _shopButtonLabel);
            _eventButton0 = BindAuthoredButton(authored, "EventBtn0", () => OnEventClicked(0), out _eventButton0Label);
            _eventButton1 = BindAuthoredButton(authored, "EventBtn1", () => OnEventClicked(1), out _eventButton1Label);
            _advanceGatherButton = BindAuthoredButton(authored, "AdvanceGatherBtn",
                () => OnAdvanceClicked(MapZoneType.Gather), out _);
            _advanceProcessButton = BindAuthoredButton(authored, "AdvanceProcessBtn",
                () => OnAdvanceClicked(MapZoneType.Process), out _);
            _advanceCookButton = BindAuthoredButton(authored, "AdvanceCookBtn",
                () => OnAdvanceClicked(MapZoneType.Cook), out _);
            _proceedButton = BindAuthoredButton(authored, "ProceedBtn", OnProceedClicked, out _proceedButtonLabel);

            // Caption sits inside button art.
            PlaceLabelInside(_elfButtonLabel);
            PlaceLabelInside(_warehouseButtonLabel);
            PlaceLabelInside(_shopButtonLabel);
            PlaceLabelInside(_eventButton0Label);
            PlaceLabelInside(_eventButton1Label);
            PlaceLabelInside(_advanceGatherButton != null
                ? _advanceGatherButton.transform.Find("Label")?.GetComponent<Text>() : null);
            PlaceLabelInside(_advanceProcessButton != null
                ? _advanceProcessButton.transform.Find("Label")?.GetComponent<Text>() : null);
            PlaceLabelInside(_advanceCookButton != null
                ? _advanceCookButton.transform.Find("Label")?.GetComponent<Text>() : null);
            PlaceLabelInside(_proceedButtonLabel);

            return _elfButton != null
                   && _warehouseButton != null
                   && _shopButton != null
                   && _eventButton0 != null
                   && _eventButton1 != null
                   && _advanceGatherButton != null
                   && _advanceProcessButton != null
                   && _advanceCookButton != null
                   && _proceedButton != null;
        }

        private static void PlaceLabelInside(Text label)
        {
            if (label == null) return;
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 4f);
            rt.offsetMax = new Vector2(-6f, -4f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
        }

        private static Button BindAuthoredButton(
            Transform root,
            string name,
            UnityEngine.Events.UnityAction onClick,
            out Text label)
        {
            label = null;
            var tf = root.Find(name);
            if (tf == null) return null;

            var button = tf.GetComponent<Button>();
            if (button == null)
                button = tf.gameObject.AddComponent<Button>();
            var image = tf.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                button.targetGraphic = image;
            }

            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(onClick);

            label = tf.Find("Label")?.GetComponent<Text>();
            return button;
        }

        private void SetAuthoredInteractable(bool enabled)
        {
            if (_authoredRoot == null) return;
            SetButtonGate(_elfButton, enabled);
            SetButtonGate(_warehouseButton, enabled);
            SetButtonGate(_shopButton, enabled);
            SetButtonGate(_eventButton0, enabled);
            SetButtonGate(_eventButton1, enabled);
            SetButtonGate(_advanceGatherButton, enabled);
            SetButtonGate(_advanceProcessButton, enabled);
            SetButtonGate(_advanceCookButton, enabled);
            SetButtonGate(_proceedButton, enabled);
        }

        private static void SetButtonGate(Button button, bool enabled)
        {
            if (button == null) return;
            // Don't force interactable true — Refresh() sets the real claim/advance rules.
            if (!enabled)
                button.interactable = false;
        }

        private void BuildProgrammaticMainHub(Transform systemHud, Transform freeDraw)
        {
            // Soft paper behind buttons only if FreeDraw is still empty.
            if (freeDraw != null && freeDraw.childCount == 0)
            {
                var bg = new GameObject("Background");
                bg.transform.SetParent(freeDraw, false);
                var bgRect = bg.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                var bgImage = bg.AddComponent<Image>();
                bgImage.sprite = GameOverlayUI.SharedUiSprite();
                bgImage.color = new Color(0.08f, 0.10f, 0.14f, 1f);
                bgImage.raycastTarget = true;
            }

            _mainRoot = new GameObject("MainHub");
            _mainRoot.transform.SetParent(systemHud, false);
            var mainRect = _mainRoot.AddComponent<RectTransform>();
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            _titleText = CreateLabel(_mainRoot.transform, "InterLevelTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(480f, 56f),
                40, FontStyle.Bold, TextAnchor.MiddleCenter);
            _titleText.text = "关卡间";

            _hintText = CreateLabel(_mainRoot.transform, "InterLevelHint",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -92f), new Vector2(720f, 36f),
                20, FontStyle.Normal, TextAnchor.MiddleCenter);
            _hintText.color = new Color(0.78f, 0.82f, 0.9f, 1f);

            _elfButton = CreateAnchoredButton(_mainRoot.transform, "ElfBtn",
                "小精灵增加",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(48f, -140f), new Vector2(280f, 72f),
                OnElfClicked);
            _elfButtonLabel = _elfButton.transform.Find("Label")?.GetComponent<Text>();

            _warehouseButton = CreateAnchoredButton(_mainRoot.transform, "WarehouseBtn",
                "仓库扩容",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-48f, -140f), new Vector2(280f, 72f),
                OnWarehouseClicked);
            _warehouseButtonLabel = _warehouseButton.transform.Find("Label")?.GetComponent<Text>();

            float midY = -40f;
            float midGap = 300f;
            _advanceGatherButton = CreateAnchoredButton(_mainRoot.transform, "AdvanceGatherBtn", "采集区进阶",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-midGap, midY), new Vector2(260f, 80f),
                () => OnAdvanceClicked(MapZoneType.Gather));
            _advanceProcessButton = CreateAnchoredButton(_mainRoot.transform, "AdvanceProcessBtn", "处理区进阶",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, midY), new Vector2(260f, 80f),
                () => OnAdvanceClicked(MapZoneType.Process));
            _advanceCookButton = CreateAnchoredButton(_mainRoot.transform, "AdvanceCookBtn", "烹饪区进阶",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(midGap, midY), new Vector2(260f, 80f),
                () => OnAdvanceClicked(MapZoneType.Cook));

            _shopButton = CreateAnchoredButton(_mainRoot.transform, "ShopBtn", "商店",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(48f, 48f), new Vector2(200f, 64f),
                OnShopClicked);
            _shopButtonLabel = _shopButton.transform.Find("Label")?.GetComponent<Text>();

            float eventY = 160f;
            _eventButton0 = CreateAnchoredButton(_mainRoot.transform, "EventBtn0", "事件 1",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-180f, eventY), new Vector2(280f, 72f),
                () => OnEventClicked(0));
            _eventButton0Label = _eventButton0.transform.Find("Label")?.GetComponent<Text>();
            _eventButton1 = CreateAnchoredButton(_mainRoot.transform, "EventBtn1", "事件 2",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(180f, eventY), new Vector2(280f, 72f),
                () => OnEventClicked(1));
            _eventButton1Label = _eventButton1.transform.Find("Label")?.GetComponent<Text>();

            _proceedButton = CreateAnchoredButton(_mainRoot.transform, "ProceedBtn", "请先领取精灵与仓库",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(360f, 64f),
                OnProceedClicked);
            _proceedButtonLabel = _proceedButton.transform.Find("Label")?.GetComponent<Text>();

            _mainRoot.SetActive(true);
        }

        private void BuildFailVictoryToast(Transform systemHud)
        {
            _failRoot = BuildBox(systemHud, "FailBox", new Vector2(640f, 360f));
            _failTitleText = CreateLabel(_failRoot.transform, "FailTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(560f, 48f),
                34, FontStyle.Bold, TextAnchor.MiddleCenter);
            _failTitleText.text = "本关失败";
            var failScoreLabel = CreateLabel(_failRoot.transform, "FailScoreLabel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 72f), new Vector2(560f, 28f),
                20, FontStyle.Normal, TextAnchor.MiddleCenter);
            failScoreLabel.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            failScoreLabel.text = "本关得分";
            _failScoreText = CreateLabel(_failRoot.transform, "FailScore",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(560f, 96f),
                56, FontStyle.Bold, TextAnchor.MiddleCenter);
            _failScoreText.color = new Color(1f, 0.92f, 0.45f, 1f);
            _failBodyText = CreateLabel(_failRoot.transform, "FailBody",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -56f), new Vector2(560f, 48f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter);
            _failBodyText.color = new Color(0.85f, 0.82f, 0.75f, 1f);
            _failRetryButton = CreateAnchoredButton(_failRoot.transform, "FailRetryBtn", "重新开始",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-130f, 48f), new Vector2(220f, 56f),
                OnFailRetryClicked);
            _failMenuButton = CreateAnchoredButton(_failRoot.transform, "FailMenuBtn", "返回主菜单",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(130f, 48f), new Vector2(220f, 56f),
                OnFailMenuClicked);

            _victoryRoot = BuildBox(systemHud, "VictoryBox", new Vector2(640f, 360f));
            _victoryTitleText = CreateLabel(_victoryRoot.transform, "VictoryTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(560f, 48f),
                36, FontStyle.Bold, TextAnchor.MiddleCenter);
            _victoryTitleText.text = "游戏胜利！";
            _victoryTitleText.color = new Color(1f, 0.92f, 0.45f, 1f);
            var victoryScoreLabel = CreateLabel(_victoryRoot.transform, "VictoryScoreLabel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 72f), new Vector2(560f, 28f),
                20, FontStyle.Normal, TextAnchor.MiddleCenter);
            victoryScoreLabel.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            victoryScoreLabel.text = "本关得分";
            _victoryScoreText = CreateLabel(_victoryRoot.transform, "VictoryScore",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(560f, 96f),
                56, FontStyle.Bold, TextAnchor.MiddleCenter);
            _victoryScoreText.color = new Color(1f, 0.92f, 0.45f, 1f);
            _victoryBodyText = CreateLabel(_victoryRoot.transform, "VictoryBody",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -56f), new Vector2(560f, 72f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter);
            _victoryBodyText.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            _victoryMenuButton = CreateAnchoredButton(_victoryRoot.transform, "VictoryMenuBtn", "返回主菜单",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(280f, 56f),
                OnVictoryMenuClicked);

            _toastText = CreateLabel(systemHud, "Toast",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 100f), new Vector2(800f, 60f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter);
            _toastText.color = new Color(1f, 0.92f, 0.55f, 1f);

            EnsureLevelScoreCaption(systemHud);

            _failRoot.SetActive(false);
            _victoryRoot.SetActive(false);
        }

        private void EnsureLevelScoreCaption(Transform systemHud)
        {
            if (systemHud == null) return;

            var existing = FindNamed(systemHud, "LevelScoreCaption");
            if (existing != null)
            {
                _levelScoreText = existing.GetComponent<Text>();
                ApplyLevelScoreCaptionLayout();
                return;
            }

            _levelScoreText = CreateLabel(
                systemHud,
                "LevelScoreCaption",
                new Vector2(0.5f, LevelScoreAnchorY),
                new Vector2(0.5f, LevelScoreAnchorY),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(560f, 160f),
                48,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            _levelScoreText.color = new Color(1f, 0.92f, 0.45f, 1f);
            _levelScoreText.supportRichText = true;
            _levelScoreText.lineSpacing = 1.05f;
            _levelScoreText.raycastTarget = false;
            var outline = _levelScoreText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.04f, 0.02f, 0.92f);
            outline.effectDistance = new Vector2(2f, -2f);
            ApplyLevelScoreCaptionLayout();
            _levelScoreText.gameObject.SetActive(false);
        }

        private static void DestroyImmediateSafe(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }

        private static GameObject BuildBox(Transform parent, string name, Vector2 size)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            var rect = box.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = box.AddComponent<Image>();
            image.sprite = GameOverlayUI.SharedUiSprite();
            image.color = new Color(0.14f, 0.12f, 0.14f, 0.98f);
            return box;
        }

        private static Text CreateLabel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize,
            FontStyle style,
            TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);
            var text = labelGo.AddComponent<Text>();
            text.font = GameOverlayUI.SharedUiFont();
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;

            return button;
        }
    }
}
