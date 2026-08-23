using System.Collections.Generic;
using Soup.Employees;
using Soup.Events;
using Soup.Jobs;
using Soup.Levels;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Control panel (relics / job unlock & advancement). Opened from overlay HUD.
    /// </summary>
    public class GamePlayHud : MonoBehaviour
    {
        [SerializeField] private bool visible;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        private Vector2 _panelScroll;
        private Vector2 _jobScroll;
        private Vector2 _progressScroll;
        private string _lastResult = string.Empty;
        private readonly List<JobItem> _jobsCache = new List<JobItem>();
        /// <summary>null = 全部来源（调试默认，避免新遗物被商店/事件筛选漏掉）。</summary>
        private RelicAcquireStage? _debugStageFilter;
        private string _relicSearch = string.Empty;
        private Vector2 _ownedRelicScroll;
        private JobType _advanceType = JobType.Gather;
        private JobItem _gatherReplaceTarget;
        private GUIStyle _titleLabel;
        private GUIStyle _bodyLabel;
        private GUIStyle _boldLabel;
        private GUIStyle _buttonStyle;
        private GUIStyle _textFieldStyle;
        private bool _sectionResourcesOpen = true;
        private bool _sectionControlsOpen = true;
        private bool _sectionValueEditOpen;
        private bool _sectionRelicsOpen;
        private bool _sectionProgressionOpen;
        private bool _sectionJobsOpen = true;
        private bool _sectionResultOpen = true;
        private int _debugEventIndex;
        private readonly List<EventItem> _debugEventCache = new List<EventItem>();
        private string _editSoft = "0";
        private string _editTough = "0";
        private string _editSolid = "0";
        private string _editSpicy = "0";
        private string _editSour = "0";
        private string _editCold = "0";
        private string _editMagic = "0";
        private string _editProcessed = "0";
        private string _editCooked = "0";
        private string _editTurn = "0";
        private string _editScore = "0";
        private string _editLastCooked = "0";
        private string _editLastScore = "0";
        private string _editElves = "0";
        private bool _valueFieldsSynced;
        private string _assignEmployeeTypeId = EmployeeManager.ElfId;

        public bool IsPanelOpen => visible;

        public void SetPanelMode(bool open)
        {
            visible = open;
            if (open)
            {
                _valueFieldsSynced = false;
                RelicManager.Instance?.ReloadDatabaseFromResources();
            }
        }

        public void TogglePanelMode()
        {
            visible = !visible;
            if (visible)
            {
                _valueFieldsSynced = false;
                RelicManager.Instance?.ReloadDatabaseFromResources();
            }
        }

        private void OnEnable()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.TurnResolved += OnTurnResolved;
                TurnManager.Instance.UndoApplied += OnUndoApplied;
            }
        }

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.TurnResolved -= OnTurnResolved;
                TurnManager.Instance.UndoApplied -= OnUndoApplied;
            }
        }

        private void Update()
        {
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsOpen)
            {
                if (visible)
                    visible = false;
                return;
            }

            if (StarterJobSelectUI.Instance != null && StarterJobSelectUI.Instance.IsOpen)
            {
                if (visible)
                    visible = false;
                return;
            }

            if (Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
                if (visible)
                {
                    _valueFieldsSynced = false;
                    RelicManager.Instance?.ReloadDatabaseFromResources();
                }
            }
        }

        private void OnTurnResolved(TurnResult result)
        {
            _lastResult = result != null ? result.ToString() : string.Empty;
            _valueFieldsSynced = false;
        }

        private void OnUndoApplied()
        {
            _lastResult = "已撤回上一回合";
            _valueFieldsSynced = false;
        }

        private void OnGUI()
        {
            if (!visible) return;

            EnsureStyles();

            // Dim background
            var dim = new Color(0f, 0f, 0f, 0.55f);
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Large modal (~88% screen), not edge-to-edge.
            float width = Mathf.Min(Screen.width * 0.88f, Screen.width - 64f);
            float height = Mathf.Min(Screen.height * 0.88f, Screen.height - 64f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(area, "box");

            GUILayout.BeginHorizontal();
            GUILayout.Label("操控面板（遗物 / 岗位进阶 / 分配）", TitleLabel());
            GUILayout.FlexibleSpace();
            if (ActionButton("关闭", 110f, 40f))
                visible = false;
            GUILayout.EndHorizontal();
            GUILayout.Label($"[{toggleKey}] 开关 · [Esc] 关闭 · 各大类点击「展开」查看", BodyLabel());

            GUILayout.Space(8f);
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            if (BeginFoldSection("资源面板", ref _sectionResourcesOpen, "展开查看当前资源 / 关卡 / 员工摘要"))
            {
                DrawResourceBar();
                EndFoldSection();
            }

            GUILayout.Space(6f);
            if (BeginFoldSection("快捷操作", ref _sectionControlsOpen, "下一回合、撤回、重置、测试事件等"))
            {
                DrawControls();
                EndFoldSection();
            }

            GUILayout.Space(6f);
            if (BeginFoldSection("自由调整数值", ref _sectionValueEditOpen, "展开后可直接改资源 / 回合 / 总分 / 精灵数"))
            {
                DrawValueTweaksBody();
                EndFoldSection();
            }

            GUILayout.Space(6f);
            if (BeginFoldSection("遗物（局内持有）", ref _sectionRelicsOpen, "展开查看持有遗物与调试获取"))
            {
                DrawRelicPanel();
                EndFoldSection();
            }

            GUILayout.Space(6f);
            if (BeginFoldSection("岗位进阶", ref _sectionProgressionOpen, "展开进行开局补选 / 升级 / 新增岗位"))
            {
                DrawJobProgressionPanel();
                EndFoldSection();
            }

            GUILayout.Space(6f);
            if (BeginFoldSection("岗位分配", ref _sectionJobsOpen, "展开分配采集 / 处理 / 烹饪岗位"))
            {
                DrawJobPanel();
                EndFoldSection();
            }

            GUILayout.Space(6f);
            if (BeginFoldSection("上回合结算", ref _sectionResultOpen, string.IsNullOrEmpty(_lastResult) ? "暂无结算信息" : "展开查看最近一次结算"))
            {
                DrawLastResult();
                EndFoldSection();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private bool BeginFoldSection(string title, ref bool open, string collapsedHint)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, TitleLabel());
            GUILayout.FlexibleSpace();
            if (ActionButton(open ? "收起" : "展开", 96f, 36f))
                open = !open;

            GUILayout.EndHorizontal();

            if (!open)
            {
                if (!string.IsNullOrEmpty(collapsedHint))
                    GUILayout.Label(collapsedHint, BodyLabel());
                GUILayout.EndVertical();
                return false;
            }

            return true;
        }

        private static void EndFoldSection()
        {
            GUILayout.EndVertical();
        }

        private void DrawResourceBar()
        {
            var store = ResourceStore.Instance;
            var elves = ElfManager.Instance;
            var turns = TurnManager.Instance;

            if (store == null)
            {
                GUILayout.Label("ResourceStore 未就绪", BodyLabel());
                return;
            }

            GUILayout.BeginHorizontal();
            Stat("柔软", store.Soft);
            Stat("强韧", store.Tough);
            Stat("坚固", store.Solid);
            Stat($"仓库 {store.TotalRaw}/{CapLabel(store.WarehouseCapacity)}", -1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            Stat("热辣", store.Spicy);
            Stat("酸涩", store.Sour);
            Stat("寒冷", store.Cold);
            Stat("鲜美", store.Magic);
            GUILayout.EndHorizontal();

            DrawFlavorScoreBreakdown(store, elves, turns);

            GUILayout.BeginHorizontal();
            Stat("已处理", store.Processed);
            Stat("已烹饪", store.Cooked);
            if (turns != null)
            {
                Stat("回合", turns.TurnIndex);
                Stat("总分", turns.Score);
            }
            if (elves != null)
                Stat($"精灵 闲{elves.FreeCount}/总{elves.TotalCount}", -1);
            GUILayout.EndHorizontal();

            DrawEmployeeSummaryRow();
            DrawLevelSummaryRow(turns);
        }

        private void DrawFlavorScoreBreakdown(ResourceStore store, ElfManager elves, TurnManager turns)
        {
            GUILayout.Space(4f);
            GUILayout.Label("分数组成（本关已计入总分）", BoldLabel());

            if (turns == null)
            {
                GUILayout.Label("TurnManager 未就绪", BodyLabel());
                return;
            }

            GUILayout.BeginHorizontal();
            Stat($"烹饪 +{turns.ScoreFromCook}", -1);
            Stat($"热辣 +{turns.ScoreFromSpicy}", -1);
            Stat($"酸涩 +{turns.ScoreFromSour}", -1);
            Stat($"寒冷 +{turns.ScoreFromCold}", -1);
            Stat($"鲜美 +{turns.ScoreFromMagic}", -1);
            GUILayout.EndHorizontal();

            int partsTotal = turns.ScoreFromCook + turns.ScoreFromSpicy + turns.ScoreFromSour
                + turns.ScoreFromCold + turns.ScoreFromMagic;
            GUILayout.Label(
                $"组成合计 {partsTotal} = 总分 {turns.Score}（烹饪=岗位烹饪分，热辣=关底倍率额外分）",
                BodyLabel());
        }

        private void DrawLevelSummaryRow(TurnManager turns)
        {
            var levels = LevelManager.Instance;
            if (levels != null && levels.HasLevels && levels.Current != null)
            {
                var level = levels.Current;
                string status = levels.Outcome switch
                {
                    LevelOutcome.Won when levels.IsCampaignComplete => "全通关",
                    LevelOutcome.Won => "已通关·结算中",
                    LevelOutcome.Lost => "失败",
                    _ => "进行中"
                };

                GUILayout.BeginHorizontal();
                Stat($"关卡 {level.DisplayName}", -1);
                Stat(level.FormatScoreProgress(levels.ScoreGainedInLevel), -1);
                Stat($"回合 {levels.LevelTurnIndex}/{level.MaxTurns}", -1);
                Stat($"[{status}]", -1);
                GUILayout.EndHorizontal();

                if (turns != null)
                {
                    GUILayout.BeginHorizontal();
                    Stat("本关烹饪", turns.StageCooked);
                    GUILayout.Label("酸涩在每一大关（关底）结束时换分，计入本关目标判定", BodyLabel());
                    GUILayout.EndHorizontal();
                }

                return;
            }

            if (turns == null) return;

            GUILayout.BeginHorizontal();
            Stat("阶段", turns.StageIndex);
            Stat("本关烹饪", turns.StageCooked);
            GUILayout.Label("酸涩在每一大关结束时按已烹饪食材占比换分", BodyLabel());
            GUILayout.EndHorizontal();
        }

        private void DrawDebugEventPicker()
        {
            var events = EventManager.Instance;
            if (events == null)
            {
                GUILayout.Label("EventManager 未就绪", BodyLabel());
                return;
            }

            RefreshDebugEventCache(events);

            GUILayout.Space(4f);
            GUILayout.Label("测试事件（忽略已触发限制）", BoldLabel());

            if (_debugEventCache.Count == 0)
            {
                GUILayout.Label("无事件数据（请先 Soup/Event Manager/Seed Sample Events）", BodyLabel());
                return;
            }

            _debugEventIndex = Mathf.Clamp(_debugEventIndex, 0, _debugEventCache.Count - 1);
            var pick = _debugEventCache[_debugEventIndex];

            GUILayout.BeginHorizontal();
            if (ActionButton("◀", 40f, 36f))
                _debugEventIndex = (_debugEventIndex - 1 + _debugEventCache.Count) % _debugEventCache.Count;

            GUILayout.Label($"{pick.DisplayName}  ({pick.Id})", BodyLabel(), GUILayout.ExpandWidth(true));

            if (ActionButton("▶", 40f, 36f))
                _debugEventIndex = (_debugEventIndex + 1) % _debugEventCache.Count;

            GUI.enabled = !events.HasPendingEvent;
            if (ActionButton("弹出事件", 110f, 36f))
            {
                if (events.PresentForDebug(pick))
                {
                    _lastResult = $"已弹出事件：{pick.DisplayName}";
                    SetPanelMode(false);
                }
                else
                    _lastResult = "弹出失败（可能已有待选事件）";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (events.HasPendingEvent)
                GUILayout.Label($"当前待选：{events.PendingEvent.DisplayName}", BodyLabel());
        }

        private void RefreshDebugEventCache(EventManager events)
        {
            _debugEventCache.Clear();
            var all = events.All;
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i];
                if (item != null)
                    _debugEventCache.Add(item);
            }

            _debugEventCache.Sort((a, b) =>
            {
                int cmp = string.CompareOrdinal(a.DisplayName, b.DisplayName);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.Id, b.Id);
            });

            _debugEventIndex = Mathf.Clamp(_debugEventIndex, 0, Mathf.Max(0, _debugEventCache.Count - 1));
        }

        private void DrawEmployeeSummaryRow()
        {
            var em = EmployeeManager.Instance;
            if (em == null) return;

            GUILayout.BeginHorizontal();
            var mushroom = em.MushroomPersonType;
            if (mushroom != null)
            {
                int n = em.GetOwned(mushroom);
                if (n > 0 || mushroom.HasLockedJob)
                    Stat($"蘑菇人 {n}（锁蘑菇岗）", -1);
            }

            var ghost = em.GhostType;
            if (ghost != null)
            {
                int owned = em.GetOwned(ghost);
                int free = em.GetFree(ghost);
                if (owned > 0 || free > 0)
                    Stat($"幽灵 闲{free}/总{owned}", -1);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            var levels = LevelManager.Instance;
            bool canTurn = levels == null || levels.CanAdvanceTurn;
            GUI.enabled = canTurn;
            if (ActionButton("下一回合", 140f, 40f))
            {
                if (TurnManager.Instance != null)
                    TurnManager.Instance.NextTurn();
            }
            GUI.enabled = true;

            GUI.enabled = TurnManager.Instance != null && TurnManager.Instance.CanUndo
                && (levels == null || levels.Outcome != LevelOutcome.Lost);
            if (ActionButton("撤回上一回合", 150f, 40f))
            {
                if (TurnManager.Instance != null && TurnManager.Instance.TryUndoPreviousTurn())
                    _lastResult = "已撤回上一回合";
            }
            GUI.enabled = true;

            bool canSettle = levels == null || !levels.HasLevels;
            GUI.enabled = canSettle;
            if (!levels?.HasLevels ?? true)
            {
                if (ActionButton("大关结算", 120f, 40f))
                {
                    if (TurnManager.Instance != null)
                    {
                        var settle = TurnManager.Instance.SettleStage();
                        _lastResult = settle != null ? settle.ToString() : "大关已结算";
                        _valueFieldsSynced = false;
                    }
                }
            }
            GUI.enabled = true;

            if (ActionButton("重置局", 120f, 40f))
            {
                TurnManager.Instance?.ResetRun();
                _gatherReplaceTarget = null;
                _lastResult = "已重置";
                _valueFieldsSynced = false;
            }

            if (ActionButton("清空分配", 120f, 40f))
                ElfManager.Instance?.ClearAssignments();

            if (ActionButton("+蘑菇人", 100f, 40f))
            {
                EmployeeManager.Instance?.Add(EmployeeManager.MushroomPersonId, 1);
                _lastResult = "已添加 1 蘑菇人（锁定蘑菇岗）";
                FindObjectOfType<JobWorldMap>()?.RefreshLabels();
            }

            if (ActionButton("+幽灵", 90f, 40f))
            {
                EmployeeManager.Instance?.Add(EmployeeManager.GhostId, 1);
                _lastResult = "已添加 1 幽灵（不占岗，效率 0.8）";
            }

            if (ActionButton("关卡间", 110f, 40f))
            {
                if (levels == null || !levels.HasLevels)
                    _lastResult = "无关卡数据";
                else if (levels.HasActiveClearRewards)
                    _lastResult = "关卡间页面已打开";
                else
                {
                    levels.DebugForceOpenClearRewards();
                    _lastResult = "已打开关卡间页面（调试）";
                    SetPanelMode(false);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (levels != null && levels.HasLevels)
            {
                if (ActionButton("普通胜利", 100f, 36f))
                {
                    levels.DebugForceCampaignVictory(DebugCampaignVictoryKind.Normal);
                    _lastResult = "已跳转：普通胜利结算";
                    SetPanelMode(false);
                }

                if (ActionButton("挑战胜利", 100f, 36f))
                {
                    levels.DebugForceCampaignVictory(DebugCampaignVictoryKind.Challenge);
                    _lastResult = "已跳转：挑战胜利结算";
                    SetPanelMode(false);
                }

                if (ActionButton("终极挑战胜利", 130f, 36f))
                {
                    levels.DebugForceCampaignVictory(DebugCampaignVictoryKind.UltimateChallenge);
                    _lastResult = "已跳转：终极挑战胜利结算";
                    SetPanelMode(false);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{toggleKey}] 显隐", BodyLabel());
            GUILayout.EndHorizontal();

            DrawDebugEventPicker();

            var eventMgr = EventManager.Instance;
            if (eventMgr != null)
            {
                int cool = eventMgr.GetCooldownTurnsRemaining();
                string coolText = eventMgr.EnableTurnEndEvents
                    ? (cool > 0 ? $"冷却剩余 {cool} 回合" : "可随机触发")
                    : "回合随机已关闭";
                int incentive = RelicManager.Instance != null
                    ? RelicManager.Instance.CountOwnedId(RelicManager.IncentiveId)
                    : 0;
                GUILayout.Label(
                    $"激励 ×{incentive}   待选：{(eventMgr.HasPendingEvent ? eventMgr.PendingEvent.DisplayName : "无")}   {coolText}（间隔 {eventMgr.EventCooldownTurns} / 概率 {eventMgr.TurnEndEventChance:0.##}）",
                    BodyLabel());
            }
        }

        private void DrawValueTweaksBody()
        {
            if (!_valueFieldsSynced)
                SyncValueFieldsFromState();

            GUILayout.BeginHorizontal();
            ValueField("柔软", ref _editSoft);
            ValueField("强韧", ref _editTough);
            ValueField("坚固", ref _editSolid);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ValueField("热辣", ref _editSpicy);
            ValueField("酸涩", ref _editSour);
            ValueField("寒冷", ref _editCold);
            ValueField("鲜美", ref _editMagic);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ValueField("已处理", ref _editProcessed);
            ValueField("已烹饪", ref _editCooked);
            ValueField("精灵总数", ref _editElves);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            ValueField("回合", ref _editTurn);
            ValueField("总分", ref _editScore);
            ValueField("上回合烹饪", ref _editLastCooked);
            ValueField("上回合得分", ref _editLastScore);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (ActionButton("从当前同步", 130f, 34f))
                SyncValueFieldsFromState();

            if (ActionButton("应用数值", 130f, 34f))
                ApplyEditedValues();

            GUILayout.FlexibleSpace();
            GUILayout.Label("应用后会清空「撤回上一回合」缓冲", BodyLabel());
            GUILayout.EndHorizontal();
        }

        private void SyncValueFieldsFromState()
        {
            var store = ResourceStore.Instance;
            var turns = TurnManager.Instance;
            var elves = ElfManager.Instance;

            if (store != null)
            {
                _editSoft = store.Soft.ToString();
                _editTough = store.Tough.ToString();
                _editSolid = store.Solid.ToString();
                _editSpicy = store.Spicy.ToString();
                _editSour = store.Sour.ToString();
                _editCold = store.Cold.ToString();
                _editMagic = store.Magic.ToString();
                _editProcessed = store.Processed.ToString();
                _editCooked = store.Cooked.ToString();
            }

            if (turns != null)
            {
                _editTurn = turns.TurnIndex.ToString();
                _editScore = turns.Score.ToString();
                _editLastCooked = turns.LastTurnCooked.ToString();
                _editLastScore = turns.LastTurnScore.ToString();
            }

            if (elves != null)
                _editElves = elves.TotalCount.ToString();

            _valueFieldsSynced = true;
        }

        private void ApplyEditedValues()
        {
            int soft = ParseNonNeg(_editSoft);
            int tough = ParseNonNeg(_editTough);
            int solid = ParseNonNeg(_editSolid);
            int spicy = ParseNonNeg(_editSpicy);
            int sour = ParseNonNeg(_editSour);
            int cold = ParseNonNeg(_editCold);
            int magic = ParseNonNeg(_editMagic);
            int processed = ParseNonNeg(_editProcessed);
            int cooked = ParseNonNeg(_editCooked);
            int turn = ParseNonNeg(_editTurn);
            int score = ParseNonNeg(_editScore);
            int lastCooked = ParseNonNeg(_editLastCooked);
            int lastScore = ParseNonNeg(_editLastScore);
            int elves = ParseNonNeg(_editElves);

            ResourceStore.Instance?.ApplyState(
                soft, tough, solid,
                spicy, sour, cold, magic,
                processed, cooked);
            TurnManager.Instance?.ApplyState(turn, score, lastCooked, lastScore);
            ElfManager.Instance?.SetTotalCount(elves);
            TurnManager.Instance?.ClearUndoSnapshot();

            var map = FindObjectOfType<JobWorldMap>();
            map?.RefreshLabels();

            _lastResult =
                $"已应用数值：回合 {turn} / 总分 {score} / 精灵 {elves} / " +
                $"原料 {soft}+{tough}+{solid} / 处理 {processed} / 烹饪 {cooked}";
            SyncValueFieldsFromState();
        }

        private void ValueField(string label, ref string field)
        {
            GUILayout.BeginVertical(GUILayout.MinWidth(120f));
            GUILayout.Label(label, BodyLabel());
            field = GUILayout.TextField(field ?? "0", TextFieldStyle(), GUILayout.Width(110f), GUILayout.Height(28f));
            GUILayout.BeginHorizontal();
            if (ActionButton("-", 32f, 28f))
                field = Mathf.Max(0, ParseNonNeg(field) - 1).ToString();
            if (ActionButton("+", 32f, 28f))
                field = (ParseNonNeg(field) + 1).ToString();
            if (ActionButton("+10", 42f, 28f))
                field = (ParseNonNeg(field) + 10).ToString();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static int ParseNonNeg(string text)
        {
            if (!int.TryParse(text, out int value))
                return 0;
            return Mathf.Max(0, value);
        }

        private void DrawRelicPanel()
        {
            var relics = RelicManager.Instance;
            if (relics == null)
            {
                GUILayout.Label("RelicManager 未就绪（需 Resources/RelicDatabase）", BodyLabel());
                return;
            }

            // RelicManager 在打开面板时已 ReloadDatabaseFromResources。

            GUILayout.BeginHorizontal();
            GUILayout.Label("筛选:", BodyLabel(), GUILayout.Width(48f));
            string stageLabel = _debugStageFilter.HasValue
                ? RelicItem.StageLabel(_debugStageFilter.Value)
                : "全部来源";
            if (ActionButton(stageLabel, 130f, 32f))
                CycleDebugRelicStageFilter();

            if (ActionButton("刷新库", 80f, 32f))
            {
                relics.ReloadDatabaseFromResources();
                _lastResult = $"遗物库已刷新，共 {relics.All.Count} 个";
            }

            if (ActionButton("获取筛选全部", 140f, 32f))
            {
                var list = relics.GetRelicsForDebug(_debugStageFilter);
                int gained = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (MatchesRelicSearch(list[i]) && relics.Acquire(list[i]))
                        gained++;
                }

                _lastResult = $"{stageLabel} 新获取 {gained} 个遗物";
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"库 {relics.All.Count} · 持有 {relics.Owned.Count}", BodyLabel());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索:", BodyLabel(), GUILayout.Width(48f));
            _relicSearch = GUILayout.TextField(_relicSearch ?? string.Empty, TextFieldStyle(), GUILayout.MinWidth(200f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("局内持有", BoldLabel());
            if (relics.Owned.Count == 0)
            {
                GUILayout.Label("（空）下方可选列表直接获取；商店遗物也可在关卡间购买。", BodyLabel());
            }
            else
            {
                _ownedRelicScroll = GUILayout.BeginScrollView(
                    _ownedRelicScroll, GUILayout.MinHeight(72f), GUILayout.MaxHeight(140f));
                for (int i = 0; i < relics.Owned.Count; i++)
                {
                    var owned = relics.Owned[i];
                    if (owned == null) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"✓ {owned.DisplayName}", BodyLabel(), GUILayout.Width(180f));
                    GUILayout.Label(owned.GetRulesSummary(), BodyLabel(), GUILayout.MinWidth(280f));
                    if (ActionButton("弃", 36f, 28f))
                    {
                        relics.RemoveOwned(owned);
                        _lastResult = $"已弃置：{owned.DisplayName}";
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(6f);
            var stageRelics = relics.GetRelicsForDebug(_debugStageFilter);
            int shown = 0;
            for (int i = 0; i < stageRelics.Count; i++)
            {
                if (MatchesRelicSearch(stageRelics[i]))
                    shown++;
            }

            GUILayout.Label($"可选获取（{stageLabel} · {shown}/{stageRelics.Count}）", BoldLabel());
            if (stageRelics.Count == 0)
            {
                GUILayout.Label("该筛选暂无遗物。请执行「Soup/Relic Manager/Seed All Relics」。", BodyLabel());
                return;
            }

            // 不用大块内层 ScrollView：嵌套滚动会导致列表底部遗物滚不到、像「没同步」。
            for (int i = 0; i < stageRelics.Count; i++)
            {
                var item = stageRelics[i];
                if (item == null || !MatchesRelicSearch(item)) continue;
                int stacks = relics.CountOwned(item);
                GUILayout.BeginHorizontal();
                GUILayout.Label(item.DisplayName, BodyLabel(), GUILayout.Width(150f));
                GUILayout.Label(RelicItem.StageLabel(item.AcquireStage), BodyLabel(), GUILayout.Width(100f));
                if (ActionButton(stacks > 0 ? $"再获取({stacks})" : "获取", 100f, 28f))
                {
                    if (relics.Acquire(item))
                        _lastResult = $"获得遗物：{item.DisplayName}";
                }
                GUILayout.Label(item.Description, BodyLabel(), GUILayout.MinWidth(200f));
                GUILayout.EndHorizontal();
            }
        }

        private void CycleDebugRelicStageFilter()
        {
            if (!_debugStageFilter.HasValue)
            {
                _debugStageFilter = RelicAcquireStage.Shop;
                return;
            }

            if (_debugStageFilter.Value == RelicAcquireStage.Shop)
            {
                _debugStageFilter = RelicAcquireStage.Event;
                return;
            }

            if (_debugStageFilter.Value == RelicAcquireStage.Event)
            {
                _debugStageFilter = RelicAcquireStage.Starting;
                return;
            }

            _debugStageFilter = null;
        }

        private bool MatchesRelicSearch(RelicItem item)
        {
            if (item == null) return false;
            string q = (_relicSearch ?? string.Empty).Trim();
            if (q.Length == 0) return true;
            if (!string.IsNullOrEmpty(item.DisplayName)
                && item.DisplayName.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrEmpty(item.Id)
                && item.Id.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private void DrawJobProgressionPanel()
        {
            var progression = JobProgressionManager.Instance;
            var jobs = JobManager.Instance;
            if (progression == null || jobs == null)
            {
                GUILayout.Label("JobProgressionManager / JobManager 未就绪", BodyLabel());
                return;
            }

            progression.BootstrapDefaults();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"采集 {progression.CountUnlocked(JobType.Gather)}/{JobProgressionRules.GatherMaxStations}  " +
                $"处理 {progression.CountUnlocked(JobType.Process)}/{JobProgressionRules.ProcessMaxStations}  " +
                $"开局:{(progression.IsSetupComplete ? "完成" : "待选")}",
                BodyLabel(),
                GUILayout.MinWidth(320f));
            if (ActionButton("刷新采集二选一", 140f, 32f))
                progression.RefreshGatherOffer();
            GUILayout.EndHorizontal();

            _progressScroll = GUILayout.BeginScrollView(_progressScroll, GUILayout.MinHeight(200f), GUILayout.MaxHeight(360f));

            if (progression.NeedsGatherStarterPick)
            {
                GUILayout.Label("开局采集：进关后二选一；此处可补选", BoldLabel());
                DrawUnlockCandidates(progression.GetLocked(JobType.Gather), job =>
                {
                    if (progression.TryPickGatherStarter(job))
                        _lastResult = $"开局采集选择：{job.DisplayName}";
                });
            }

            if (progression.NeedsProcessStarterPick)
            {
                GUILayout.Label("开局处理：进关后四选一；此处可补选", BoldLabel());
                DrawUnlockCandidates(progression.GetLocked(JobType.Process), job =>
                {
                    if (progression.TryPickProcessStarter(job))
                        _lastResult = $"开局处理选择：{job.DisplayName}";
                });
            }

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("进阶调试类型:", BodyLabel(), GUILayout.Width(110f));
            if (ActionButton(JobItem.JobTypeLabel(_advanceType), 80f, 32f))
            {
                int next = ((int)_advanceType + 1) % 3;
                _advanceType = (JobType)next;
            }
            GUILayout.EndHorizontal();

            DrawAdvanceActions(progression, _advanceType);

            GUILayout.EndScrollView();
        }

        private void DrawAdvanceActions(JobProgressionManager progression, JobType type)
        {
            GUILayout.Label($"升级已有{JobItem.JobTypeLabel(type)}岗", BoldLabel());
            var unlocked = progression.GetUnlocked(type);
            if (unlocked.Count == 0)
                GUILayout.Label("（无已解锁岗位）", BodyLabel());

            for (int i = 0; i < unlocked.Count; i++)
            {
                var job = unlocked[i];
                int level = progression.GetUpgradeLevel(job);
                int max = JobProgressionRules.MaxUpgradesPerJob(type);
                var path = progression.GetAdvancePath(job);
                string pathTag = path != JobAdvanceNodeId.None
                    ? JobAdvancePath.ToLabel(path)
                    : "-";
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{job.DisplayName} [{pathTag}] {level}/{max}", BodyLabel(), GUILayout.Width(200f));
                GUILayout.Label(progression.DescribeUpgradePreview(job), BodyLabel(), GUILayout.MinWidth(180f));
                GUI.enabled = progression.CanUpgrade(job);
                if (ActionButton("升A", 52f, 30f))
                {
                    var choices = new System.Collections.Generic.List<JobAdvanceNodeId>(2);
                    progression.GetAvailableAdvanceChoices(job, choices);
                    if (choices.Count > 0 && progression.TryAdvance(job, choices[0]))
                        _lastResult = $"进阶：{job.DisplayName} → [{JobAdvancePath.ToLabel(progression.GetAdvancePath(job))}]";
                }
                if (ActionButton("升B", 52f, 30f))
                {
                    var choices = new System.Collections.Generic.List<JobAdvanceNodeId>(2);
                    progression.GetAvailableAdvanceChoices(job, choices);
                    if (choices.Count > 1 && progression.TryAdvance(job, choices[1]))
                        _lastResult = $"进阶：{job.DisplayName} → [{JobAdvancePath.ToLabel(progression.GetAdvancePath(job))}]";
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            if (type == JobType.Gather)
            {
                GUILayout.Space(2f);
                if (progression.CanUnlockMore(JobType.Gather))
                {
                    GUILayout.Label("新增采集岗（二选一）", BoldLabel());
                    if (progression.CurrentGatherOffer.Count == 0)
                        progression.RefreshGatherOffer();

                    DrawUnlockCandidates(progression.CurrentGatherOffer, job =>
                    {
                        if (progression.TryUnlockFromGatherOffer(job))
                        {
                            _gatherReplaceTarget = null;
                            _lastResult = $"新增采集岗：{job.DisplayName}";
                        }
                    });
                }
                else if (progression.CanReplaceGather)
                {
                    GUILayout.Label("采集已满：更换岗位（蘑菇不可换）", BoldLabel());
                    var replaceable = progression.GetReplaceableGatherJobs();
                    GUILayout.Label(
                        _gatherReplaceTarget != null
                            ? $"将卸下：{_gatherReplaceTarget.DisplayName}，再选新岗"
                            : "先选要卸下的岗位，再从二选一中选新岗",
                        BodyLabel());

                    for (int i = 0; i < replaceable.Count; i++)
                    {
                        var job = replaceable[i];
                        if (job == null) continue;
                        bool selected = ReferenceEquals(_gatherReplaceTarget, job);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(
                            $"{(selected ? "→ " : "  ")}{job.DisplayName} Lv{progression.GetUpgradeLevel(job)}",
                            BodyLabel(),
                            GUILayout.Width(200f));
                        if (ActionButton(selected ? "已选" : "卸下", 60f, 30f))
                            _gatherReplaceTarget = job;
                        GUILayout.EndHorizontal();
                    }

                    if (progression.CurrentGatherOffer.Count == 0)
                        progression.RefreshGatherOffer();

                    GUI.enabled = _gatherReplaceTarget != null;
                    DrawUnlockCandidates(progression.CurrentGatherOffer, job =>
                    {
                        if (_gatherReplaceTarget == null) return;
                        string outgoingName = _gatherReplaceTarget.DisplayName;
                        var outgoing = _gatherReplaceTarget;
                        if (progression.TryReplaceGatherJob(outgoing, job))
                        {
                            var elves = ElfManager.Instance;
                            if (elves != null)
                            {
                                int assigned = elves.GetAssigned(outgoing);
                                if (assigned > 0)
                                    elves.TryUnassign(outgoing, assigned);
                            }

                            _lastResult = $"更换采集岗：{outgoingName} → {job.DisplayName}";
                            _gatherReplaceTarget = null;
                        }
                    });
                    GUI.enabled = true;
                }
                else
                {
                    GUILayout.Label("新增采集岗（二选一）", BoldLabel());
                    GUILayout.Label("已达采集岗位上限，且无可更换的候选岗。", BodyLabel());
                }
            }
            else if (type == JobType.Process)
            {
                GUILayout.Space(2f);
                GUILayout.Label("新增处理岗（从剩余中选 1）", BoldLabel());
                if (!progression.CanUnlockMore(JobType.Process))
                {
                    GUILayout.Label("已达处理岗位上限或无可选岗位。", BodyLabel());
                }
                else
                {
                    DrawUnlockCandidates(progression.GetLocked(JobType.Process), job =>
                    {
                        if (progression.TryUnlockProcessJob(job))
                            _lastResult = $"新增处理岗：{job.DisplayName}";
                    });
                }
            }
            else
            {
                GUILayout.Space(2f);
                GUILayout.Label("烹饪：选择一种火力升级一次（进阶效果暂空）", BoldLabel());
            }
        }

        private void DrawUnlockCandidates(IReadOnlyList<JobItem> candidates, System.Action<JobItem> onPick)
        {
            if (candidates == null || candidates.Count == 0)
            {
                GUILayout.Label("（无可选项）", BodyLabel());
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var job = candidates[i];
                if (job == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(job.DisplayName, BodyLabel(), GUILayout.Width(140f));
                GUILayout.Label(job.GetEffectSummary(), BodyLabel(), GUILayout.MinWidth(200f));
                if (ActionButton("选择", 60f, 30f))
                    onPick?.Invoke(job);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawJobPanel()
        {
            var elves = ElfManager.Instance;
            var em = EmployeeManager.Instance;
            var jobs = JobManager.Instance;
            var progression = JobProgressionManager.Instance;
            if (elves == null || jobs == null)
            {
                GUILayout.Label("ElfManager / JobManager 未就绪", BodyLabel());
                return;
            }

            RefreshJobsCache(jobs);

            GUILayout.Label("仅显示已解锁岗位", BodyLabel());
            DrawAssignTypePicker(em);
            _jobScroll = GUILayout.BeginScrollView(_jobScroll, GUILayout.MinHeight(220f), GUILayout.MaxHeight(420f));

            DrawJobGroup("采集", JobType.Gather, elves, em, progression);
            DrawJobGroup("处理", JobType.Process, elves, em, progression);

            var activeCook = elves.GetActiveCookJob();
            string cookTitle = activeCook != null
                ? $"烹饪（当前：{activeCook.DisplayName}，三选一）"
                : "烹饪（小火/中火/大火 三选一）";
            DrawJobGroup(cookTitle, JobType.Cook, elves, em, progression);

            GUILayout.EndScrollView();
        }

        private void DrawAssignTypePicker(EmployeeManager em)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("分配单位:", BodyLabel(), GUILayout.Width(90f));
            if (em == null)
            {
                GUILayout.Label("EmployeeManager 未就绪", BodyLabel());
                GUILayout.EndHorizontal();
                return;
            }

            for (int i = 0; i < em.All.Count; i++)
            {
                var type = em.All[i];
                if (type == null || !type.CanPlayerAssign) continue;

                int free = em.GetFree(type);
                int owned = em.GetOwned(type);
                bool selected = EmployeeAssignSelection.SelectedTypeId == type.Id;
                string label = $"{type.DisplayName} 闲{free}/总{owned}";
                if (selected)
                    GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
                if (ActionButton(label, 0f, 32f, 140f))
                {
                    _assignEmployeeTypeId = type.Id;
                    EmployeeAssignSelection.Select(type);
                }
                GUI.backgroundColor = Color.white;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("玩法界面头像与岗位 +/- 共用当前分配单位。", BodyLabel());
        }

        private void DrawJobGroup(
            string title,
            JobType type,
            ElfManager elves,
            EmployeeManager em,
            JobProgressionManager progression)
        {
            GUILayout.Space(6f);
            GUILayout.Label(title, BoldLabel());

            _assignEmployeeTypeId = EmployeeAssignSelection.SelectedTypeId;
            var assignType = EmployeeAssignSelection.Current;
            if (assignType == null || !assignType.CanPlayerAssign)
                assignType = em != null ? em.ElfType : null;

            for (int i = 0; i < _jobsCache.Count; i++)
            {
                var job = _jobsCache[i];
                if (job == null || job.JobType != type) continue;
                if (progression != null && !progression.IsUnlocked(job)) continue;

                int occupying = elves.GetAssigned(job);
                float labor = em != null ? em.GetLaborOnJob(job) : occupying;
                int typeAssigned = em != null && assignType != null ? em.GetAssigned(assignType, job) : 0;
                int remain = elves.GetRemainingCapacity(job);
                int capacity = elves.GetJobCapacity(job);
                string cap = capacity == int.MaxValue ? "∞" : capacity.ToString();
                int level = progression != null ? progression.GetUpgradeLevel(job) : 0;
                var path = progression != null ? progression.GetAdvancePath(job) : JobAdvanceNodeId.None;
                string levelLabel = path != JobAdvanceNodeId.None
                    ? $" [{JobAdvancePath.ToLabel(path)}]"
                    : (level > 0 ? $" Lv{level}" : string.Empty);

                bool isCook = job.JobType == JobType.Cook;
                var activeCook = isCook ? elves.GetActiveCookJob() : null;
                bool otherCookActive = isCook && activeCook != null && !ReferenceEquals(activeCook, job);

                int free = assignType != null && em != null ? em.GetFree(assignType) : 0;
                int fromOtherCook = 0;
                if (otherCookActive && em != null && assignType != null)
                    fromOtherCook = em.GetAssigned(assignType, activeCook);

                bool canPlus = assignType != null && (
                    (otherCookActive && fromOtherCook > 0) ||
                    (free > 0 && (assignType.OccupiesJobSlot ? remain > 0 || remain == int.MaxValue : true)));

                GUILayout.BeginHorizontal();
                string mark = otherCookActive ? "·" : (isCook && occupying > 0 ? "✓" : " ");
                string laborTag = Mathf.Abs(labor - occupying) > 0.01f ? $" 劳{labor:0.##}" : string.Empty;
                GUILayout.Label(
                    $"{mark} {job.DisplayName}{levelLabel}  占{occupying}/{cap}{laborTag}  本类{typeAssigned}",
                    BodyLabel(),
                    GUILayout.Width(360f));
                GUILayout.Label(job.GetEffectSummary(), BodyLabel(), GUILayout.MinWidth(200f));

                GUI.enabled = typeAssigned > 0;
                if (ActionButton("-", 32f, 30f))
                    em?.TryUnassign(assignType, job, 1);
                GUI.enabled = canPlus;
                if (ActionButton(otherCookActive ? "选" : "+", 32f, 30f))
                    em?.TryAssign(assignType, job, 1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawLastResult()
        {
            GUILayout.Label(string.IsNullOrEmpty(_lastResult) ? "—" : _lastResult, BodyLabel());
        }

        private void RefreshJobsCache(JobManager jobs)
        {
            _jobsCache.Clear();
            var all = jobs.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                    _jobsCache.Add(all[i]);
            }

            _jobsCache.Sort((a, b) =>
            {
                int typeCmp = a.JobType.CompareTo(b.JobType);
                if (typeCmp != 0) return typeCmp;
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
        }

        private void Stat(string label, int value)
        {
            string text = value < 0 ? label : $"{label} {value}";
            GUILayout.Label(text, BodyLabel(), GUILayout.MinWidth(110f));
        }

        private static string CapLabel(int capacity) => capacity <= 0 ? "∞" : capacity.ToString();

        private void EnsureStyles()
        {
            if (_titleLabel == null)
            {
                _titleLabel = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 22,
                    richText = true
                };
            }

            if (_bodyLabel == null)
            {
                _bodyLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    wordWrap = true
                };
            }

            if (_boldLabel == null)
            {
                _boldLabel = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 18
                };
            }

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (_textFieldStyle == null)
            {
                _textFieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 16
                };
            }
        }

        private GUIStyle TitleLabel()
        {
            EnsureStyles();
            return _titleLabel;
        }

        private GUIStyle BodyLabel()
        {
            EnsureStyles();
            return _bodyLabel;
        }

        private GUIStyle BoldLabel()
        {
            EnsureStyles();
            return _boldLabel;
        }

        private GUIStyle ButtonStyle()
        {
            EnsureStyles();
            return _buttonStyle;
        }

        private GUIStyle TextFieldStyle()
        {
            EnsureStyles();
            return _textFieldStyle;
        }

        private bool ActionButton(string text, float width, float height, float minWidth = 0f)
        {
            if (width > 0f)
                return GUILayout.Button(text, ButtonStyle(), GUILayout.Width(width), GUILayout.Height(height));
            if (minWidth > 0f)
                return GUILayout.Button(text, ButtonStyle(), GUILayout.MinWidth(minWidth), GUILayout.Height(height));
            return GUILayout.Button(text, ButtonStyle(), GUILayout.Height(height));
        }
    }
}
