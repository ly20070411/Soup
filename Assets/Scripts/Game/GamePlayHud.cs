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
    /// 执行顺序在 PauseMenuUI(-45) 之前 + GUI.depth=1：面板在世界地图(5)之上、
    /// 主菜单/暂停菜单(0)之下，任何情况下都不会盖住菜单按钮。
    /// </summary>
    [DefaultExecutionOrder(-46)]
    public class GamePlayHud : MonoBehaviour
    {
        [SerializeField] private bool visible;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        private Vector2 _jobScroll;
        private Vector2 _relicScroll;
        private Vector2 _progressScroll;
        private Vector2 _outerScroll;
        private string _lastResult = string.Empty;
        private readonly List<JobItem> _jobsCache = new List<JobItem>();
        private RelicAcquireStage _debugStage = RelicAcquireStage.Starting;
        private JobType _advanceType = JobType.Gather;
        private JobItem _gatherReplaceTarget;
        private GUIStyle _boldLabel;
        private bool _valueEditOpen;
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
                _outerScroll = Vector2.zero;
            }
        }

        public void TogglePanelMode()
        {
            visible = !visible;
            if (visible)
            {
                _valueFieldsSynced = false;
                _outerScroll = Vector2.zero;
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
            if (PauseMenuUI.IsOpen)
            {
                // 暂停菜单打开时收起本面板，避免两个模态叠在一起抢按钮。
                if (visible)
                    visible = false;
                return;
            }

            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsOpen)
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
                    _outerScroll = Vector2.zero;
                }
            }
            else if (visible && Input.GetKeyDown(KeyCode.Escape))
                visible = false;
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
            if (PauseMenuUI.IsOpen) return;

            // 世界地图(5)之上、各级菜单(0)之下的模态层。
            GUI.depth = 1;

            // Dim background
            var dim = new Color(0f, 0f, 0f, 0.55f);
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Large modal (~88% screen), not edge-to-edge.
            float width = Mathf.Min(Screen.width * 0.88f, Screen.width - 64f);
            float height = Mathf.Min(Screen.height * 0.88f, Screen.height - 64f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(area, SoupUITheme.PanelBox);

            // 各分区总高度可能超过模态高度（岗位分配/员工选择排在最后），
            // 外层套滚动视图保证底部分区始终可达。
            _outerScroll = GUILayout.BeginScrollView(_outerScroll, false, true);

            float relicH = Mathf.Clamp(height * 0.2f, 140f, 220f);
            float progressH = Mathf.Clamp(height * 0.24f, 180f, 280f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("操控面板（遗物 / 岗位进阶 / 分配）", SoupUITheme.BoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("关闭", GUILayout.Width(100f), GUILayout.Height(36f)))
                visible = false;
            GUILayout.EndHorizontal();
            GUILayout.Label($"[{toggleKey}] 开关 · [Esc] 关闭", SoupUITheme.Label);

            GUILayout.Space(6f);
            DrawResourceBar();
            GUILayout.Space(6f);
            DrawControls();
            GUILayout.Space(6f);
            DrawValueTweaks();
            GUILayout.Space(6f);
            DrawRelicPanel(relicH);
            GUILayout.Space(6f);
            DrawJobProgressionPanel(progressH);
            GUILayout.Space(6f);
            DrawJobPanel();
            GUILayout.Space(6f);
            DrawLastResult();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawResourceBar()
        {
            var store = ResourceStore.Instance;
            var elves = ElfManager.Instance;
            var turns = TurnManager.Instance;

            GUILayout.BeginVertical("box");
            GUILayout.Label("资源面板");

            if (store == null)
            {
                GUILayout.Label("ResourceStore 未就绪");
                GUILayout.EndVertical();
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

            GUILayout.EndVertical();
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
                Stat($"得分 {levels.ScoreGainedInLevel}/{level.TargetScore}", -1);
                Stat($"回合 {levels.LevelTurnIndex}/{level.MaxTurns}", -1);
                Stat($"[{status}]", -1);
                GUILayout.EndHorizontal();

                if (turns != null)
                {
                    GUILayout.BeginHorizontal();
                    Stat("本关烹饪", turns.StageCooked);
                    GUILayout.Label("酸涩在达标或最后一回合结束时自动结算，并计入本关目标判定");
                    GUILayout.EndHorizontal();
                }

                return;
            }

            if (turns == null) return;

            GUILayout.BeginHorizontal();
            Stat("阶段", turns.StageIndex);
            Stat("本关烹饪", turns.StageCooked);
            GUILayout.Label("酸涩仅在阶段结算时换分，不会在每回合烹饪时消失");
            GUILayout.EndHorizontal();
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
            if (GUILayout.Button("下一回合", GUILayout.Height(36f), GUILayout.Width(120f)))
            {
                if (TurnManager.Instance != null)
                    TurnManager.Instance.NextTurn();
            }
            GUI.enabled = true;

            GUI.enabled = TurnManager.Instance != null && TurnManager.Instance.CanUndo
                && (levels == null || levels.Outcome != LevelOutcome.Lost);
            if (GUILayout.Button("撤回上一回合", GUILayout.Height(36f), GUILayout.Width(130f)))
            {
                if (TurnManager.Instance != null && TurnManager.Instance.TryUndoPreviousTurn())
                    _lastResult = "已撤回上一回合";
            }
            GUI.enabled = true;

            bool canSettle = levels == null || !levels.HasLevels;
            GUI.enabled = canSettle;
            if (!levels?.HasLevels ?? true)
            {
                if (GUILayout.Button("大关结算", GUILayout.Height(36f), GUILayout.Width(100f)))
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

            if (GUILayout.Button("重置局", GUILayout.Height(36f), GUILayout.Width(100f)))
            {
                TurnManager.Instance?.ResetRun();
                _gatherReplaceTarget = null;
                _lastResult = "已重置";
                _valueFieldsSynced = false;
            }

            if (GUILayout.Button("清空分配", GUILayout.Height(36f), GUILayout.Width(100f)))
                ElfManager.Instance?.ClearAssignments();

            if (GUILayout.Button("+蘑菇人", GUILayout.Height(36f), GUILayout.Width(80f)))
            {
                EmployeeManager.Instance?.Add(EmployeeManager.MushroomPersonId, 1);
                _lastResult = "已添加 1 蘑菇人（锁定蘑菇岗）";
                FindObjectOfType<JobWorldMap>()?.RefreshLabels();
            }

            if (GUILayout.Button("+幽灵", GUILayout.Height(36f), GUILayout.Width(70f)))
            {
                EmployeeManager.Instance?.Add(EmployeeManager.GhostId, 1);
                _lastResult = "已添加 1 幽灵（不占岗，效率 0.8）";
            }

            if (GUILayout.Button("触发示例事件", GUILayout.Height(36f), GUILayout.Width(120f)))
            {
                var events = EventManager.Instance;
                if (events == null)
                    _lastResult = "EventManager 未就绪";
                else if (events.HasPendingEvent)
                    _lastResult = "已有待选事件";
                else if (events.PresentById("more_hands") || events.Present(events.All.Count > 0 ? events.All[0] : null))
                    _lastResult = $"已弹出事件：{events.PendingEvent.DisplayName}";
                else
                    _lastResult = "没有可弹出的事件（请先 Soup/Event Manager/Seed Sample Events）";
            }

            if (GUILayout.Button("关卡间", GUILayout.Height(36f), GUILayout.Width(100f)))
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

            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{toggleKey}] 显隐", SoupUITheme.Label);
            GUILayout.EndHorizontal();

            var eventMgr = EventManager.Instance;
            if (eventMgr != null)
            {
                int cool = eventMgr.GetCooldownTurnsRemaining();
                string coolText = eventMgr.EnableTurnEndEvents
                    ? (cool > 0 ? $"冷却剩余 {cool} 回合" : "可随机触发")
                    : "回合随机已关闭";
                GUILayout.Label(
                    $"族长的激励：{eventMgr.ChiefIncentive}   待选：{(eventMgr.HasPendingEvent ? eventMgr.PendingEvent.DisplayName : "无")}   {coolText}（间隔 {eventMgr.EventCooldownTurns} / 概率 {eventMgr.TurnEndEventChance:0.##}）",
                    SoupUITheme.Label);
            }
        }

        private void DrawValueTweaks()
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label("自由调整数值", BoldLabel());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_valueEditOpen ? "收起" : "展开", GUILayout.Width(70f)))
            {
                _valueEditOpen = !_valueEditOpen;
                if (_valueEditOpen)
                    SyncValueFieldsFromState();
            }
            GUILayout.EndHorizontal();

            if (!_valueEditOpen)
            {
                GUILayout.Label("展开后可直接改资源 / 回合 / 总分 / 精灵数");
                GUILayout.EndVertical();
                return;
            }

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
            if (GUILayout.Button("从当前同步", GUILayout.Height(30f), GUILayout.Width(110f)))
                SyncValueFieldsFromState();

            if (GUILayout.Button("应用数值", GUILayout.Height(30f), GUILayout.Width(110f)))
                ApplyEditedValues();

            GUILayout.FlexibleSpace();
            GUILayout.Label("应用后会清空「撤回上一回合」缓冲");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
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

        private static void ValueField(string label, ref string field)
        {
            GUILayout.BeginVertical(GUILayout.MinWidth(110f));
            GUILayout.Label(label);
            field = GUILayout.TextField(field ?? "0", GUILayout.Width(100f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(28f)))
                field = Mathf.Max(0, ParseNonNeg(field) - 1).ToString();
            if (GUILayout.Button("+", GUILayout.Width(28f)))
                field = (ParseNonNeg(field) + 1).ToString();
            if (GUILayout.Button("+10", GUILayout.Width(36f)))
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

        private void DrawRelicPanel(float panelHeight)
        {
            var relics = RelicManager.Instance;
            GUILayout.BeginVertical("box", GUILayout.Height(panelHeight));
            GUILayout.Label("遗物（局内持有）", BoldLabel());

            if (relics == null)
            {
                GUILayout.Label("RelicManager 未就绪（需 Resources/RelicDatabase）");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("调试获取阶段:", GUILayout.Width(100f));
            if (GUILayout.Button(RelicItem.StageLabel(_debugStage), GUILayout.Width(100f)))
            {
                _debugStage = _debugStage == RelicAcquireStage.Starting
                    ? RelicAcquireStage.Event
                    : RelicAcquireStage.Starting;
            }

            if (GUILayout.Button("获取该来源全部", GUILayout.Width(120f)))
            {
                var list = relics.GetRelicsForStage(_debugStage);
                int gained = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (relics.Acquire(list[i]))
                        gained++;
                }

                _lastResult = $"{RelicItem.StageLabel(_debugStage)} 新获取 {gained} 个遗物";
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"持有 {relics.Owned.Count}");
            GUILayout.EndHorizontal();

            float scrollH = Mathf.Max(80f, panelHeight - 60f);
            _relicScroll = GUILayout.BeginScrollView(_relicScroll, GUILayout.Height(scrollH));

            // Owned
            if (relics.Owned.Count == 0)
                GUILayout.Label("（空）用上方调试按钮按来源获取。开局遗物在主菜单「开始游戏」时三选一。");
            else
            {
                for (int i = 0; i < relics.Owned.Count; i++)
                {
                    var owned = relics.Owned[i];
                    if (owned == null) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"✓ {owned.DisplayName}", GUILayout.Width(160f));
                    GUILayout.Label(owned.GetRulesSummary(), GUILayout.MinWidth(280f));
                    if (GUILayout.Button("弃", GUILayout.Width(28f)))
                        relics.RemoveOwned(owned);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(4f);
            GUILayout.Label($"可选（{RelicItem.StageLabel(_debugStage)}）", BoldLabel());
            var stageRelics = relics.GetRelicsForStage(_debugStage);
            if (stageRelics.Count == 0)
                GUILayout.Label("该来源暂无遗物。请在「Soup/遗物管理器」填充正式遗物。");
            else
            {
                for (int i = 0; i < stageRelics.Count; i++)
                {
                    var item = stageRelics[i];
                    if (item == null) continue;
                    int stacks = relics.CountOwned(item);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{item.DisplayName}", GUILayout.Width(160f));
                    if (GUILayout.Button(stacks > 0 ? $"再获取({stacks})" : "获取", GUILayout.Width(80f)))
                    {
                        if (relics.Acquire(item))
                            _lastResult = $"获得遗物：{item.DisplayName}";
                    }
                    GUILayout.Label(item.Description, GUILayout.MinWidth(200f));
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawJobProgressionPanel(float panelHeight)
        {
            var progression = JobProgressionManager.Instance;
            var jobs = JobManager.Instance;
            GUILayout.BeginVertical("box", GUILayout.Height(panelHeight));
            GUILayout.Label("岗位进阶", BoldLabel());

            if (progression == null || jobs == null)
            {
                GUILayout.Label("JobProgressionManager / JobManager 未就绪");
                GUILayout.EndVertical();
                return;
            }

            progression.BootstrapDefaults();

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                $"采集 {progression.CountUnlocked(JobType.Gather)}/{JobProgressionRules.GatherMaxStations}  " +
                $"处理 {progression.CountUnlocked(JobType.Process)}/{JobProgressionRules.ProcessMaxStations}  " +
                $"开局:{(progression.IsSetupComplete ? "完成" : "待选")}",
                GUILayout.MinWidth(280f));
            if (GUILayout.Button("刷新采集二选一", GUILayout.Width(110f)))
                progression.RefreshGatherOffer();
            GUILayout.EndHorizontal();

            float scrollH = Mathf.Max(120f, panelHeight - 50f);
            _progressScroll = GUILayout.BeginScrollView(_progressScroll, GUILayout.Height(scrollH));

            if (progression.NeedsGatherStarterPick)
            {
                GUILayout.Label("开局采集：应在主菜单二选一完成；此处可补选", BoldLabel());
                DrawUnlockCandidates(progression.GetLocked(JobType.Gather), job =>
                {
                    if (progression.TryPickGatherStarter(job))
                        _lastResult = $"开局采集选择：{job.DisplayName}";
                });
            }

            if (progression.NeedsProcessStarterPick)
            {
                GUILayout.Label("开局处理：应在主菜单四选一完成；此处可补选", BoldLabel());
                DrawUnlockCandidates(progression.GetLocked(JobType.Process), job =>
                {
                    if (progression.TryPickProcessStarter(job))
                        _lastResult = $"开局处理选择：{job.DisplayName}";
                });
            }

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("进阶调试类型:", GUILayout.Width(90f));
            if (GUILayout.Button(JobItem.JobTypeLabel(_advanceType), GUILayout.Width(70f)))
            {
                int next = ((int)_advanceType + 1) % 3;
                _advanceType = (JobType)next;
            }
            GUILayout.EndHorizontal();

            DrawAdvanceActions(progression, _advanceType);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawAdvanceActions(JobProgressionManager progression, JobType type)
        {
            GUILayout.Label($"升级已有{JobItem.JobTypeLabel(type)}岗", BoldLabel());
            var unlocked = progression.GetUnlocked(type);
            if (unlocked.Count == 0)
                GUILayout.Label("（无已解锁岗位）");

            for (int i = 0; i < unlocked.Count; i++)
            {
                var job = unlocked[i];
                int level = progression.GetUpgradeLevel(job);
                int max = JobProgressionRules.MaxUpgradesPerJob(type);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{job.DisplayName} Lv{level}/{max}", GUILayout.Width(160f));
                GUILayout.Label(progression.DescribeUpgradePreview(job), GUILayout.MinWidth(180f));
                GUI.enabled = progression.CanUpgrade(job);
                if (GUILayout.Button("升级", GUILayout.Width(50f)))
                {
                    if (progression.TryUpgrade(job))
                        _lastResult = $"升级岗位：{job.DisplayName} → Lv{progression.GetUpgradeLevel(job)}";
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
                            : "先选要卸下的岗位，再从二选一中选新岗");

                    for (int i = 0; i < replaceable.Count; i++)
                    {
                        var job = replaceable[i];
                        if (job == null) continue;
                        bool selected = ReferenceEquals(_gatherReplaceTarget, job);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"{(selected ? "→ " : "  ")}{job.DisplayName} Lv{progression.GetUpgradeLevel(job)}", GUILayout.Width(180f));
                        if (GUILayout.Button(selected ? "已选" : "卸下", GUILayout.Width(50f)))
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
                    GUILayout.Label("已达采集岗位上限，且无可更换的候选岗。");
                }
            }
            else if (type == JobType.Process)
            {
                GUILayout.Space(2f);
                GUILayout.Label("新增处理岗（从剩余中选 1）", BoldLabel());
                if (!progression.CanUnlockMore(JobType.Process))
                {
                    GUILayout.Label("已达处理岗位上限或无可选岗位。");
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
                GUILayout.Label("（无可选项）");
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var job = candidates[i];
                if (job == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(job.DisplayName, GUILayout.Width(120f));
                GUILayout.Label(job.GetEffectSummary(), GUILayout.MinWidth(200f));
                if (GUILayout.Button("选择", GUILayout.Width(50f)))
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
                GUILayout.Label("ElfManager / JobManager 未就绪");
                return;
            }

            RefreshJobsCache(jobs);

            GUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
            GUILayout.Label("岗位分配（仅显示已解锁）");
            DrawAssignTypePicker(em);
            _jobScroll = GUILayout.BeginScrollView(_jobScroll);

            DrawJobGroup("采集", JobType.Gather, elves, em, progression);
            DrawJobGroup("处理", JobType.Process, elves, em, progression);

            DrawJobGroup("烹饪（火力可同时组合分配）", JobType.Cook, elves, em, progression);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawAssignTypePicker(EmployeeManager em)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("分配单位:", GUILayout.Width(70f));
            if (em == null)
            {
                GUILayout.Label("EmployeeManager 未就绪");
                GUILayout.EndHorizontal();
                return;
            }

            for (int i = 0; i < em.All.Count; i++)
            {
                var type = em.All[i];
                if (type == null || !type.CanPlayerAssign) continue;

                int free = em.GetFree(type);
                int owned = em.GetOwned(type);
                bool selected = _assignEmployeeTypeId == type.Id;
                string label = $"{type.DisplayName} 闲{free}/总{owned}";
                if (selected)
                    GUI.backgroundColor = new Color(0.55f, 0.75f, 1f);
                if (GUILayout.Button(
                        type.Icon != null
                            ? new GUIContent(label, type.Icon.texture)
                            : new GUIContent(label),
                        GUILayout.Height(26f),
                        GUILayout.MinWidth(120f)))
                    _assignEmployeeTypeId = type.Id;
                GUI.backgroundColor = Color.white;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("底部生产条与面板的 +/- 均按所选分配单位操作；蘑菇人锁定岗位不可手动分配。");
        }

        private void DrawJobGroup(
            string title,
            JobType type,
            ElfManager elves,
            EmployeeManager em,
            JobProgressionManager progression)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title, BoldLabel());

            var assignType = em != null ? em.GetById(_assignEmployeeTypeId) : null;
            if (assignType == null || !assignType.CanPlayerAssign)
                assignType = em != null ? em.ElfType : null;

            for (int i = 0; i < _jobsCache.Count; i++)
            {
                var job = _jobsCache[i];
                if (job == null || job.JobType != type) continue;
                if (progression != null && !progression.IsUnlocked(job)) continue;
                if (JobModifierManager.Instance != null && JobModifierManager.Instance.IsDisabled(job)) continue;

                int occupying = elves.GetAssigned(job);
                float labor = em != null ? em.GetLaborOnJob(job) : occupying;
                int typeAssigned = em != null && assignType != null ? em.GetAssigned(assignType, job) : 0;
                int remain = elves.GetRemainingCapacity(job);
                int capacity = elves.GetJobCapacity(job);
                string cap = capacity == int.MaxValue ? "∞" : capacity.ToString();
                int level = progression != null ? progression.GetUpgradeLevel(job) : 0;
                string levelLabel = level > 0 ? $" Lv{level}" : string.Empty;

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
                if (job.Icon != null)
                {
                    GUILayout.Box(
                        job.Icon.texture,
                        GUIStyle.none,
                        GUILayout.Width(28f),
                        GUILayout.Height(28f));
                }

                GUILayout.Label(
                    $"{mark} {job.DisplayName}{levelLabel}  占{occupying}/{cap}{laborTag}  本类{typeAssigned}",
                    GUILayout.Width(300f));
                GUILayout.Label(job.GetEffectSummary(), GUILayout.MinWidth(200f));

                GUI.enabled = typeAssigned > 0;
                if (GUILayout.Button("-", GUILayout.Width(28f)))
                    em?.TryUnassign(assignType, job, 1);
                GUI.enabled = canPlus;
                if (GUILayout.Button(otherCookActive ? "选" : "+", GUILayout.Width(28f)))
                    em?.TryAssign(assignType, job, 1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawLastResult()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("上回合结算");
            GUILayout.Label(string.IsNullOrEmpty(_lastResult) ? "—" : _lastResult);
            GUILayout.EndVertical();
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

        private static void Stat(string label, int value)
        {
            string text = value < 0 ? label : $"{label} {value}";
            GUILayout.Label(text, GUILayout.MinWidth(90f));
        }

        private static string CapLabel(int capacity) => capacity <= 0 ? "∞" : capacity.ToString();

        private GUIStyle BoldLabel()
        {
            if (_boldLabel == null)
                _boldLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            return _boldLabel;
        }
    }
}
