using System;
using System.Collections.Generic;
using Soup.Employees;
using Soup.Events;
using Soup.Game;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Levels
{
    /// <summary>
    /// Per-level-clear reward bundle: elves / warehouse / relic 3-pick-1 /
    /// job advancement charges / two events. UI claims each row independently.
    /// </summary>
    public sealed class LevelClearRewardSession
    {
        public const int ElfRewardCount = 3;
        public const int WarehouseBonusAmount = 500;
        public const int RelicOfferCount = 3;

        public static int ResolveElfRewardCount() =>
            ElfRewardCount + JobAdvanceGatherMods.SumPermanentElfBonus();

        private readonly List<RelicItem> _relicOffers = new List<RelicItem>();
        private readonly List<string> _relicOfferIds = new List<string>();

        private bool _completedFired;

        public bool IsActive { get; private set; }

        public bool ElvesClaimed { get; private set; }
        public bool WarehouseClaimed { get; private set; }
        public bool RelicClaimed { get; private set; }
        public bool AdvancementClaimed { get; private set; }
        public bool EventsClaimed { get; private set; }

        public int GatherCharges { get; private set; }
        public int ProcessCharges { get; private set; }
        public int CookCharges { get; private set; }

        public int LevelsClearedAtStart { get; private set; }

        public IReadOnlyList<RelicItem> RelicOffers => _relicOffers;

        /// <summary>
        /// 关卡间主流程：小精灵与仓库领完后，点「进入下一关」即可。
        /// 遗物 / 事件 / 进阶子界面尚未接入时，由 <see cref="TryProceedToNextLevel"/> 自动收尾。
        /// </summary>
        public bool HubRewardsClaimed => ElvesClaimed && WarehouseClaimed;

        public bool AllClaimed =>
            ElvesClaimed && WarehouseClaimed && RelicClaimed && AdvancementClaimed && EventsClaimed;

        public bool NeedsRelicPick => IsActive && !RelicClaimed && _relicOffers.Count > 0;

        public bool NeedsAdvancementPick =>
            IsActive && !AdvancementClaimed && (GatherCharges + ProcessCharges + CookCharges) > 0;

        public event Action Changed;
        public event Action Completed;

        public void Clear()
        {
            IsActive = false;
            _completedFired = false;
            ElvesClaimed = false;
            WarehouseClaimed = false;
            RelicClaimed = false;
            AdvancementClaimed = false;
            EventsClaimed = false;
            GatherCharges = 0;
            ProcessCharges = 0;
            CookCharges = 0;
            LevelsClearedAtStart = 0;
            _relicOffers.Clear();
            _relicOfferIds.Clear();
        }

        /// <param name="levelsClearedIncludingThis">How many levels have been cleared after this win (1-based).</param>
        public void BeginFresh(int levelsClearedIncludingThis)
        {
            Clear();
            IsActive = true;
            LevelsClearedAtStart = Mathf.Max(1, levelsClearedIncludingThis);

            // 采集 / 处理：每关各 1 次；烹饪：每两关 1 次（第 2、4、6… 关通关后）。
            JobProgressionRules.AdvanceChargesForClear(
                LevelsClearedAtStart,
                out int gather,
                out int process,
                out int cook);
            GatherCharges = gather;
            ProcessCharges = process;
            CookCharges = cook;

            BuildRelicOffers(LevelsClearedAtStart);
            RaiseChanged();
        }

        public void Restore(
            int levelsClearedIncludingThis,
            bool elvesClaimed,
            bool warehouseClaimed,
            bool relicClaimed,
            bool advancementClaimed,
            bool eventsClaimed,
            int gatherCharges,
            int processCharges,
            int cookCharges,
            IList<string> relicOfferIds)
        {
            Clear();
            IsActive = true;
            LevelsClearedAtStart = Mathf.Max(1, levelsClearedIncludingThis);
            ElvesClaimed = elvesClaimed;
            WarehouseClaimed = warehouseClaimed;
            RelicClaimed = relicClaimed;
            AdvancementClaimed = advancementClaimed;
            EventsClaimed = eventsClaimed;
            GatherCharges = Mathf.Max(0, gatherCharges);
            ProcessCharges = Mathf.Max(0, processCharges);
            CookCharges = Mathf.Max(0, cookCharges);

            if (!RelicClaimed)
            {
                if (relicOfferIds != null && relicOfferIds.Count > 0)
                    LoadRelicOffers(relicOfferIds);
                else
                    BuildRelicOffers(LevelsClearedAtStart);
            }

            // If advancement was already marked but charges remain, keep charges for UI.
            if (AdvancementClaimed)
            {
                GatherCharges = 0;
                ProcessCharges = 0;
                CookCharges = 0;
            }

            RaiseChanged();
            if (AllClaimed)
                Complete();
        }

        public void Capture(
            out bool elvesClaimed,
            out bool warehouseClaimed,
            out bool relicClaimed,
            out bool advancementClaimed,
            out bool eventsClaimed,
            out int gatherCharges,
            out int processCharges,
            out int cookCharges,
            List<string> relicOfferIds)
        {
            elvesClaimed = ElvesClaimed;
            warehouseClaimed = WarehouseClaimed;
            relicClaimed = RelicClaimed;
            advancementClaimed = AdvancementClaimed;
            eventsClaimed = EventsClaimed;
            gatherCharges = GatherCharges;
            processCharges = ProcessCharges;
            cookCharges = CookCharges;
            relicOfferIds?.Clear();
            if (relicOfferIds == null) return;
            for (int i = 0; i < _relicOfferIds.Count; i++)
                relicOfferIds.Add(_relicOfferIds[i]);
        }

        public bool TryClaimElves(out string message)
        {
            message = string.Empty;
            if (!IsActive || ElvesClaimed)
            {
                message = "已领取";
                return false;
            }

            int elves = ResolveElfRewardCount();
            var elfMgr = ElfManager.Instance;
            if (elfMgr != null)
                elfMgr.AddElves(elves);
            else
                EmployeeManager.Instance?.Add(EmployeeManager.ElfId, elves);

            ElvesClaimed = true;
            message = $"获得小精灵 ×{elves}";
            AfterClaim();
            return true;
        }

        public bool TryClaimWarehouse(out string message)
        {
            message = string.Empty;
            if (!IsActive || WarehouseClaimed)
            {
                message = "已领取";
                return false;
            }

            var store = ResourceStore.Instance;
            if (store == null)
            {
                message = "ResourceStore 未就绪";
                return false;
            }

            store.AddWarehouseCapacityBonus(WarehouseBonusAmount);
            WarehouseClaimed = true;
            message = $"仓库上限 +{WarehouseBonusAmount}（当前 {store.WarehouseCapacity}）";
            AfterClaim();
            return true;
        }

        public bool TryPickRelic(RelicItem relic, out string message)
        {
            message = string.Empty;
            if (!IsActive || RelicClaimed)
            {
                message = "已领取";
                return false;
            }

            if (relic == null || !_relicOffers.Contains(relic))
            {
                message = "无效遗物选项";
                return false;
            }

            var relics = RelicManager.Instance;
            if (relics == null || !relics.Acquire(relic))
            {
                message = "无法获得该遗物";
                return false;
            }

            RelicClaimed = true;
            _relicOffers.Clear();
            _relicOfferIds.Clear();
            message = $"获得遗物：{relic.DisplayName}";
            AfterClaim();
            return true;
        }

        /// <summary>No unowned relics left — still mark claimed.</summary>
        public bool TrySkipRelicIfEmpty(out string message)
        {
            message = string.Empty;
            if (!IsActive || RelicClaimed)
            {
                message = "已领取";
                return false;
            }

            if (_relicOffers.Count > 0)
            {
                message = "请先选择遗物";
                return false;
            }

            RelicClaimed = true;
            message = "暂无可选遗物";
            AfterClaim();
            return true;
        }

        public bool TryUpgradeJob(JobItem job, JobAdvanceNodeId choice, out string message)
        {
            message = string.Empty;
            if (!CanSpendAdvancement(job != null ? job.JobType : JobType.Gather, out message))
                return false;

            var progression = JobProgressionManager.Instance;
            if (progression == null || job == null || !progression.CanAdvance(job, choice))
            {
                message = "该岗位无法沿所选路径进阶";
                return false;
            }

            if (!progression.TryAdvance(job, choice, out var destroyed))
            {
                message = "进阶失败";
                return false;
            }

            SpendCharge(job.JobType);
            string pathLabel = JobAdvancePath.ToLabel(progression.GetAdvancePath(job));
            message = $"{job.DisplayName} 进阶 → [{pathLabel}]（深度 {progression.GetUpgradeLevel(job)}）";
            if (destroyed != null)
                message += $"；摧毁采集岗：{destroyed.DisplayName}";
            AfterAdvancementAction();
            return true;
        }

        /// <summary>采集：解锁任意未拥有的采集岗（占用本区进阶次数，与进阶互斥）。</summary>
        public bool TryUnlockGather(JobItem job, out string message)
        {
            message = string.Empty;
            if (!CanSpendAdvancement(JobType.Gather, out message))
                return false;

            var progression = JobProgressionManager.Instance;
            if (progression == null)
            {
                message = "岗位进阶未就绪";
                return false;
            }

            if (!progression.CanUnlockMore(JobType.Gather))
            {
                message = "采集岗位已满";
                return false;
            }

            if (!progression.TryUnlockGatherJob(job))
            {
                message = "无法解锁该采集岗";
                return false;
            }

            SpendCharge(JobType.Gather);
            message = $"新增采集岗：{job.DisplayName}";
            AfterAdvancementAction();
            return true;
        }

        /// <summary>处理：解锁一个未拥有的处理岗。</summary>
        public bool TryUnlockProcess(JobItem job, out string message)
        {
            message = string.Empty;
            if (!CanSpendAdvancement(JobType.Process, out message))
                return false;

            var progression = JobProgressionManager.Instance;
            if (progression == null || !progression.TryUnlockProcessJob(job))
            {
                message = "无法解锁该处理岗";
                return false;
            }

            SpendCharge(JobType.Process);
            message = $"新增处理岗：{job.DisplayName}";
            AfterAdvancementAction();
            return true;
        }

        /// <summary>采集已满时：卸下一个非固定岗，换上 offer 中的新岗。</summary>
        public bool TryReplaceGather(JobItem outgoing, JobItem incoming, out string message)
        {
            message = string.Empty;
            if (!CanSpendAdvancement(JobType.Gather, out message))
                return false;

            var progression = JobProgressionManager.Instance;
            if (progression == null)
            {
                message = "岗位进阶未就绪";
                return false;
            }

            EnsureGatherOffer(progression);
            if (!progression.TryReplaceGatherJob(outgoing, incoming))
            {
                message = "换岗失败";
                return false;
            }

            var elves = ElfManager.Instance;
            if (elves != null && outgoing != null)
            {
                int assigned = elves.GetAssigned(outgoing);
                if (assigned > 0)
                    elves.TryUnassign(outgoing, assigned);
            }

            SpendCharge(JobType.Gather);
            message = $"更换采集岗：{outgoing.DisplayName} → {incoming.DisplayName}";
            AfterAdvancementAction();
            return true;
        }

        public bool TryFinishAdvancement(out string message)
        {
            message = string.Empty;
            if (!IsActive || AdvancementClaimed)
            {
                message = "已领取";
                return false;
            }

            // Allow finishing early when no upgradeable jobs remain for remaining charges.
            if (HasSpendableCharges())
            {
                message = "还有可用的进阶次数";
                return false;
            }

            AdvancementClaimed = true;
            GatherCharges = ProcessCharges = CookCharges = 0;
            message = "进阶已完成";
            AfterClaim();
            return true;
        }

        public bool TrySkipAdvancementIfNone(out string message)
        {
            message = string.Empty;
            if (!IsActive || AdvancementClaimed)
            {
                message = "已领取";
                return false;
            }

            if (HasSpendableCharges())
            {
                message = "仍有可进阶选项";
                return false;
            }

            AdvancementClaimed = true;
            GatherCharges = ProcessCharges = CookCharges = 0;
            message = "当前无可进阶选项";
            AfterClaim();
            return true;
        }

        public bool TryClaimEvents(out string message)
        {
            message = string.Empty;
            if (!IsActive || EventsClaimed)
            {
                message = "已领取";
                return false;
            }

            var events = EventManager.Instance;
            if (events == null)
            {
                EventsClaimed = true;
                message = "无事件可领取";
                AfterClaim();
                return true;
            }

            // Already presenting this clear's event batch — wait for player choices.
            if (events.HasPendingEvent || events.HasStageEventBatch)
            {
                message = "请先完成当前事件选择";
                return false;
            }

            int presented = events.PresentStageEvents();
            if (presented <= 0)
            {
                // PresentStageEvents already fired StageEventBatchCompleted synchronously.
                // MarkEventsResolved may have already completed; ensure flag.
                if (!EventsClaimed)
                {
                    EventsClaimed = true;
                    AfterClaim();
                }

                message = "本关没有可触发事件";
                return true;
            }

            message = $"出现 {presented} 个事件";
            RaiseChanged();
            return true;
        }

        public void MarkEventsResolved()
        {
            if (!IsActive || EventsClaimed) return;
            EventsClaimed = true;
            AfterClaim();
        }

        /// <summary>
        /// 关卡间页：领取小精灵与仓库后进入下一关。
        /// 尚未接入的遗物 / 事件 / 进阶在此收尾，避免卡在关卡间。
        /// </summary>
        public bool TryProceedToNextLevel(out string message)
        {
            message = string.Empty;
            if (!IsActive)
            {
                message = "关卡间页面未激活";
                return false;
            }

            if (!ElvesClaimed)
            {
                message = "请先领取小精灵";
                return false;
            }

            if (!WarehouseClaimed)
            {
                message = "请先提升仓库上限";
                return false;
            }

            if (!RelicClaimed)
            {
                RelicClaimed = true;
                _relicOffers.Clear();
                _relicOfferIds.Clear();
            }

            if (!AdvancementClaimed)
            {
                AdvancementClaimed = true;
                GatherCharges = ProcessCharges = CookCharges = 0;
            }

            if (!EventsClaimed)
                EventsClaimed = true;

            message = "进入下一关";
            AfterClaim();
            return true;
        }

        public List<JobItem> GetUpgradeableJobs(JobType type)
        {
            var list = new List<JobItem>();
            if (ChargeFor(type) <= 0) return list;

            var progression = JobProgressionManager.Instance;
            if (progression == null) return list;

            var jobs = progression.GetUnlocked(type);
            for (int i = 0; i < jobs.Count; i++)
            {
                if (progression.CanUpgrade(jobs[i]))
                    list.Add(jobs[i]);
            }

            return list;
        }

        public List<JobItem> GetGatherUnlockOffers()
        {
            return GetUnlockCandidates(JobType.Gather);
        }

        /// <summary>
        /// 本区可解锁岗位的随机 offer（最多 <see cref="JobProgressionRules.AdvancementUnlockOfferCount"/> 个）。
        /// 从全部未拥有岗位中抽取；未选中的仍留在锁定池，下次仍有机会出现。
        /// </summary>
        public List<JobItem> GetUnlockCandidates(JobType type)
        {
            var list = new List<JobItem>();
            if (ChargeFor(type) <= 0) return list;

            var progression = JobProgressionManager.Instance;
            if (progression == null || !progression.CanUnlockMore(type))
                return list;

            var locked = progression.GetLocked(type);
            if (locked.Count == 0) return list;

            ShuffleJobs(locked);
            int take = Mathf.Min(JobProgressionRules.AdvancementUnlockOfferCount, locked.Count);
            for (int i = 0; i < take; i++)
                list.Add(locked[i]);
            return list;
        }

        public List<JobItem> GetGatherReplaceIncomingOffers()
        {
            var list = new List<JobItem>();
            if (GatherCharges <= 0) return list;

            var progression = JobProgressionManager.Instance;
            if (progression == null || !progression.CanReplaceGather)
                return list;

            EnsureGatherOffer(progression);
            for (int i = 0; i < progression.CurrentGatherOffer.Count; i++)
            {
                var job = progression.CurrentGatherOffer[i];
                if (job != null)
                    list.Add(job);
            }

            return list;
        }

        public List<JobItem> GetGatherReplaceOutgoingJobs()
        {
            if (GatherCharges <= 0) return new List<JobItem>();
            var progression = JobProgressionManager.Instance;
            if (progression == null || !progression.CanReplaceGather)
                return new List<JobItem>();
            return progression.GetReplaceableGatherJobs();
        }

        public List<JobItem> GetProcessUnlockCandidates()
        {
            return GetUnlockCandidates(JobType.Process);
        }

        public bool HasSpendableCharges()
        {
            if (GatherCharges > 0)
            {
                if (GetUpgradeableJobs(JobType.Gather).Count > 0) return true;
                if (GetUnlockCandidates(JobType.Gather).Count > 0) return true;
                if (GetGatherReplaceOutgoingJobs().Count > 0 && GetGatherReplaceIncomingOffers().Count > 0)
                    return true;
            }

            if (ProcessCharges > 0)
            {
                if (GetUpgradeableJobs(JobType.Process).Count > 0) return true;
                if (GetUnlockCandidates(JobType.Process).Count > 0) return true;
            }

            if (CookCharges > 0 && GetUpgradeableJobs(JobType.Cook).Count > 0)
                return true;

            return false;
        }

        public string AdvancementSummary()
        {
            var parts = new List<string>(3);
            if (GatherCharges > 0) parts.Add($"采集×{GatherCharges}");
            if (ProcessCharges > 0) parts.Add($"处理×{ProcessCharges}");
            if (CookCharges > 0)
                parts.Add($"烹饪×{CookCharges}");
            else if (!AdvancementClaimed
                     && !JobProgressionRules.GrantsCookAdvanceOnClear(LevelsClearedAtStart))
                parts.Add("烹饪（每两关一次，本关无）");

            if (GatherCharges + ProcessCharges + CookCharges <= 0)
                return AdvancementClaimed ? "本关进阶次数已用完" : "当前无可进阶选项";
            return string.Join("  ", parts);
        }

        private bool CanSpendAdvancement(JobType type, out string message)
        {
            message = string.Empty;
            if (!IsActive || AdvancementClaimed)
            {
                message = "已领取";
                return false;
            }

            if (ChargeFor(type) <= 0)
            {
                message = "该环节没有剩余进阶次数";
                return false;
            }

            return true;
        }

        private void AfterAdvancementAction()
        {
            RaiseChanged();
            if (!HasSpendableCharges())
            {
                AdvancementClaimed = true;
                GatherCharges = ProcessCharges = CookCharges = 0;
                AfterClaim();
            }
        }

        private static void EnsureGatherOffer(JobProgressionManager progression)
        {
            if (progression == null) return;
            if (progression.CurrentGatherOffer.Count > 0) return;
            if (progression.CanUnlockMore(JobType.Gather) || progression.CanReplaceGather)
                progression.RefreshGatherOffer();
        }

        private void AfterClaim()
        {
            RaiseChanged();
            if (AllClaimed)
                Complete();
        }

        private void Complete()
        {
            if (_completedFired) return;
            _completedFired = true;
            IsActive = false;
            Completed?.Invoke();
        }

        private int ChargeFor(JobType type)
        {
            switch (type)
            {
                case JobType.Gather: return GatherCharges;
                case JobType.Process: return ProcessCharges;
                case JobType.Cook: return CookCharges;
                default: return 0;
            }
        }

        private void SpendCharge(JobType type)
        {
            switch (type)
            {
                case JobType.Gather:
                    GatherCharges = Mathf.Max(0, GatherCharges - 1);
                    break;
                case JobType.Process:
                    ProcessCharges = Mathf.Max(0, ProcessCharges - 1);
                    break;
                case JobType.Cook:
                    CookCharges = Mathf.Max(0, CookCharges - 1);
                    break;
            }
        }

        private void BuildRelicOffers(int levelsCleared)
        {
            _relicOffers.Clear();
            _relicOfferIds.Clear();
            var relics = RelicManager.Instance;
            if (relics == null) return;

            // levelsCleared reserved for future stage-weighted offers.
            _ = levelsCleared;
            var preferred = RelicAcquireStage.Event;
            var picks = relics.CreateOffer(RelicOfferCount, preferred);
            for (int i = 0; i < picks.Count; i++)
            {
                if (picks[i] == null) continue;
                _relicOffers.Add(picks[i]);
                _relicOfferIds.Add(picks[i].Id);
            }
        }

        private void LoadRelicOffers(IList<string> ids)
        {
            _relicOffers.Clear();
            _relicOfferIds.Clear();
            var relics = RelicManager.Instance;
            if (relics == null || ids == null) return;

            for (int i = 0; i < ids.Count; i++)
            {
                var item = relics.GetById(ids[i]);
                if (item == null || relics.Has(item)) continue;
                _relicOffers.Add(item);
                _relicOfferIds.Add(item.Id);
            }

            if (_relicOffers.Count == 0 && !RelicClaimed)
                BuildRelicOffers(LevelsClearedAtStart);
        }

        private static void ShuffleJobs(List<JobItem> list)
        {
            if (list == null || list.Count <= 1) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
