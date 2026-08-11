using System.Collections.Generic;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Prototype play HUD: resource panel, job assignment, next-turn button.
    /// </summary>
    public class GamePlayHud : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;

        private Vector2 _jobScroll;
        private Vector2 _relicScroll;
        private Vector2 _progressScroll;
        private string _lastResult = string.Empty;
        private readonly List<JobItem> _jobsCache = new List<JobItem>();
        private RelicAcquireStage _debugStage = RelicAcquireStage.Stage1;
        private JobType _advanceType = JobType.Gather;
        private JobItem _gatherReplaceTarget;
        private GUIStyle _boldLabel;

        private void OnEnable()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.TurnResolved += OnTurnResolved;
        }

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.TurnResolved -= OnTurnResolved;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        private void OnTurnResolved(TurnResult result)
        {
            _lastResult = result != null ? result.ToString() : string.Empty;
        }

        private void OnGUI()
        {
            if (!visible) return;

            const float pad = 10f;
            float width = Mathf.Min(920f, Screen.width - pad * 2f);
            GUILayout.BeginArea(new Rect(pad, pad, width, Screen.height - pad * 2f));

            DrawResourceBar();
            GUILayout.Space(8f);
            DrawControls();
            GUILayout.Space(8f);
            DrawRelicPanel();
            GUILayout.Space(8f);
            DrawJobProgressionPanel();
            GUILayout.Space(8f);
            DrawJobPanel();
            GUILayout.Space(8f);
            DrawLastResult();

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

            GUILayout.EndVertical();
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("下一回合", GUILayout.Height(36f), GUILayout.Width(120f)))
            {
                if (TurnManager.Instance != null)
                    TurnManager.Instance.NextTurn();
            }

            if (GUILayout.Button("重置局", GUILayout.Height(36f), GUILayout.Width(100f)))
            {
                TurnManager.Instance?.ResetRun();
                _gatherReplaceTarget = null;
                _lastResult = "已重置";
            }

            if (GUILayout.Button("清空分配", GUILayout.Height(36f), GUILayout.Width(100f)))
                ElfManager.Instance?.ClearAssignments();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{toggleKey}] 显隐");
            GUILayout.EndHorizontal();
        }

        private void DrawRelicPanel()
        {
            var relics = RelicManager.Instance;
            GUILayout.BeginVertical("box", GUILayout.Height(150f));
            GUILayout.Label("遗物（局内持有）", BoldLabel());

            if (relics == null)
            {
                GUILayout.Label("RelicManager 未就绪（需 Resources/RelicDatabase）");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("调试获取阶段:", GUILayout.Width(100f));
            if (GUILayout.Button(RelicItem.StageLabel(_debugStage), GUILayout.Width(80f)))
            {
                int next = ((int)_debugStage % 4) + 1;
                _debugStage = (RelicAcquireStage)next;
            }

            if (GUILayout.Button("获取该阶段全部", GUILayout.Width(120f)))
            {
                var list = relics.GetRelicsForStage(_debugStage);
                int gained = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (relics.Acquire(list[i]))
                        gained++;
                }

                _lastResult = $"阶段 {RelicItem.StageLabel(_debugStage)} 新获取 {gained} 个遗物";
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"持有 {relics.Owned.Count}");
            GUILayout.EndHorizontal();

            _relicScroll = GUILayout.BeginScrollView(_relicScroll, GUILayout.Height(90f));

            // Owned
            if (relics.Owned.Count == 0)
                GUILayout.Label("（空）开局无遗物，用上方调试按钮按阶段获取。");
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
                GUILayout.Label("该阶段暂无遗物。请在「Soup/遗物管理器」填充示例。");
            else
            {
                for (int i = 0; i < stageRelics.Count; i++)
                {
                    var item = stageRelics[i];
                    if (item == null) continue;
                    bool has = relics.Has(item);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{item.DisplayName}", GUILayout.Width(160f));
                    GUI.enabled = !has;
                    if (GUILayout.Button(has ? "已持有" : "获取", GUILayout.Width(60f)))
                    {
                        if (relics.Acquire(item))
                            _lastResult = $"获得遗物：{item.DisplayName}";
                    }
                    GUI.enabled = true;
                    GUILayout.Label(item.Description, GUILayout.MinWidth(200f));
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawJobProgressionPanel()
        {
            var progression = JobProgressionManager.Instance;
            var jobs = JobManager.Instance;
            GUILayout.BeginVertical("box", GUILayout.Height(200f));
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

            _progressScroll = GUILayout.BeginScrollView(_progressScroll, GUILayout.Height(150f));

            if (progression.NeedsGatherStarterPick)
            {
                GUILayout.Label("开局采集：蘑菇已解锁，请再选 1 个采集岗", BoldLabel());
                DrawUnlockCandidates(progression.GetLocked(JobType.Gather), job =>
                {
                    if (progression.TryPickGatherStarter(job))
                        _lastResult = $"开局采集选择：{job.DisplayName}";
                });
            }

            if (progression.NeedsProcessStarterPick)
            {
                GUILayout.Label("开局处理：请选择 1 个处理岗", BoldLabel());
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
            _jobScroll = GUILayout.BeginScrollView(_jobScroll);

            DrawJobGroup("采集", JobType.Gather, elves, progression);
            DrawJobGroup("处理", JobType.Process, elves, progression);

            var activeCook = elves.GetActiveCookJob();
            string cookTitle = activeCook != null
                ? $"烹饪（当前：{activeCook.DisplayName}，三选一）"
                : "烹饪（小火/中火/大火 三选一）";
            DrawJobGroup(cookTitle, JobType.Cook, elves, progression);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawJobGroup(string title, JobType type, ElfManager elves, JobProgressionManager progression)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title, BoldLabel());

            for (int i = 0; i < _jobsCache.Count; i++)
            {
                var job = _jobsCache[i];
                if (job == null || job.JobType != type) continue;
                if (progression != null && !progression.IsUnlocked(job)) continue;

                int assigned = elves.GetAssigned(job);
                int remain = elves.GetRemainingCapacity(job);
                int capacity = elves.GetJobCapacity(job);
                string cap = capacity == int.MaxValue ? "∞" : capacity.ToString();
                int level = progression != null ? progression.GetUpgradeLevel(job) : 0;
                string levelLabel = level > 0 ? $" Lv{level}" : string.Empty;

                bool isCook = job.JobType == JobType.Cook;
                var activeCook = isCook ? elves.GetActiveCookJob() : null;
                bool otherCookActive = isCook && activeCook != null && !ReferenceEquals(activeCook, job);

                GUILayout.BeginHorizontal();
                string mark = otherCookActive ? "·" : (isCook && assigned > 0 ? "✓" : " ");
                GUILayout.Label($"{mark} {job.DisplayName}{levelLabel}  {assigned}/{cap}", GUILayout.Width(250f));
                GUILayout.Label(job.GetEffectSummary(), GUILayout.MinWidth(240f));

                GUI.enabled = assigned > 0;
                if (GUILayout.Button("-", GUILayout.Width(28f)))
                    elves.TryUnassign(job, 1);
                GUI.enabled = remain > 0 && (elves.FreeCount > 0 || otherCookActive);
                if (GUILayout.Button(otherCookActive ? "选" : "+", GUILayout.Width(28f)))
                    elves.TryAssign(job, 1);
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
