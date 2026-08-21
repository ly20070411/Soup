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
        private GameObject _root;
        private GameObject _mainRoot;
        private GameObject _failRoot;
        private GameObject _victoryRoot;
        private Text _titleText;
        private Text _hintText;
        private Text _failTitleText;
        private Text _failBodyText;
        private Text _victoryTitleText;
        private Text _victoryBodyText;
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

        private void Awake()
        {
            Build();
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

            if (_mainRoot != null) _mainRoot.SetActive(showRewards);
            if (_failRoot != null) _failRoot.SetActive(showFail);
            if (_victoryRoot != null) _victoryRoot.SetActive(showVictory);

            if (showFail)
                RefreshFail(levels);
            if (showVictory)
                RefreshVictory(levels);

            if (!showRewards) return;

            string levelName = levels.Current != null ? levels.Current.DisplayName : "本关";
            if (_titleText != null)
                _titleText.text = "关卡间";
            if (_hintText != null)
                _hintText.text = $"{levelName} 通关 · 领取奖励后可进入下一关";

            SetClaimButton(_elfButton, _elfButtonLabel,
                $"获得小精灵 ×{LevelClearRewardSession.ResolveElfRewardCount()}",
                session.ElvesClaimed);

            SetClaimButton(_warehouseButton, _warehouseButtonLabel,
                $"仓库上限 +{LevelClearRewardSession.WarehouseBonusAmount}",
                session.WarehouseClaimed);

            RefreshAdvanceButton(_advanceGatherButton, MapZoneType.Gather, session);
            RefreshAdvanceButton(_advanceProcessButton, MapZoneType.Process, session);
            RefreshAdvanceButton(_advanceCookButton, MapZoneType.Cook, session);
            RefreshEventButtons(session);
            RefreshShopButton();

            bool canProceed = session.HubRewardsClaimed;
            if (_proceedButton != null)
                _proceedButton.interactable = canProceed && !_leaving;
            if (_proceedButtonLabel != null)
                _proceedButtonLabel.text = canProceed ? "进入下一关" : "请先领取精灵与仓库";
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

        private void RefreshShopButton()
        {
            var shop = FindObjectOfType<ShopPanelUI>();
            bool purchased = shop != null && shop.PurchasedThisVisit;
            if (_shopButton != null)
                _shopButton.interactable = !_leaving && !purchased;
            if (_shopButtonLabel != null)
                _shopButtonLabel.text = purchased ? "商店（已购买）" : "商店";
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
            int gained = levels.ScoreGainedInLevel;
            int target = levels.TargetScore;
            if (_failTitleText != null)
                _failTitleText.text = $"{levelName} 失败";
            if (_failBodyText != null)
                _failBodyText.text = $"第 {levels.MaxTurns} 回合已结束（含酸涩结算）\n得分 {gained} / 目标 {target}";
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
            var shop = ShopPanelUI.Ensure(transform);
            shop.SetToastHandler(msg => ShowToast(msg, 3f));
            shop.SetClosedHandler(Refresh);
            if (shop.PurchasedThisVisit)
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
            if (_leaving) return;
            TurnManager.Instance?.ResetRun();
            ShowToast("已重新开始本局", 2f);
            LeaveToPlay(useDissolve: true);
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

        private void Build()
        {
            var existing = transform.Find("InterLevelCanvas");
            if (existing != null)
                Destroy(existing.gameObject);

            var canvasGo = new GameObject("InterLevelCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            _root = canvasGo;

            var bg = new GameObject("Background");
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.sprite = GameOverlayUI.SharedUiSprite();
            bgImage.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            bgImage.raycastTarget = true;

            _mainRoot = new GameObject("MainHub");
            _mainRoot.transform.SetParent(canvasGo.transform, false);
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
                $"获得小精灵 ×{LevelClearRewardSession.ResolveElfRewardCount()}",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(48f, -140f), new Vector2(280f, 72f),
                OnElfClicked);
            _elfButtonLabel = _elfButton.transform.Find("Label")?.GetComponent<Text>();

            _warehouseButton = CreateAnchoredButton(_mainRoot.transform, "WarehouseBtn",
                $"仓库上限 +{LevelClearRewardSession.WarehouseBonusAmount}",
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

            _failRoot = BuildBox(canvasGo.transform, "FailBox", new Vector2(640f, 360f));
            _failTitleText = CreateLabel(_failRoot.transform, "FailTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(560f, 48f),
                34, FontStyle.Bold, TextAnchor.MiddleCenter);
            _failTitleText.text = "本关失败";
            _failBodyText = CreateLabel(_failRoot.transform, "FailBody",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(560f, 80f),
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

            _victoryRoot = BuildBox(canvasGo.transform, "VictoryBox", new Vector2(640f, 360f));
            _victoryTitleText = CreateLabel(_victoryRoot.transform, "VictoryTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -36f), new Vector2(560f, 48f),
                36, FontStyle.Bold, TextAnchor.MiddleCenter);
            _victoryTitleText.text = "游戏胜利！";
            _victoryTitleText.color = new Color(1f, 0.92f, 0.45f, 1f);
            _victoryBodyText = CreateLabel(_victoryRoot.transform, "VictoryBody",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -120f), new Vector2(560f, 100f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter);
            _victoryBodyText.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            _victoryMenuButton = CreateAnchoredButton(_victoryRoot.transform, "VictoryMenuBtn", "返回主菜单",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(280f, 56f),
                OnVictoryMenuClicked);

            _toastText = CreateLabel(canvasGo.transform, "Toast",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 100f), new Vector2(800f, 60f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter);
            _toastText.color = new Color(1f, 0.92f, 0.55f, 1f);

            _failRoot.SetActive(false);
            _victoryRoot.SetActive(false);
            _mainRoot.SetActive(true);
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
