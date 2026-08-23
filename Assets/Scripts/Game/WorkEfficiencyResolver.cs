using Soup.Employees;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    public enum WorkEfficiencyScope
    {
        Gather,
        Process,
        Cook
    }

    /// <summary>
    /// 工作效率分层计算：
    /// 激励/疲惫 × 遗物总效率 × (岗位自身效率 + 灯芯草/双尾蛇加成) × 快乐坨坨 × 劳动力。
    /// 固定数量产出（平铺奖励、遗物直给等）不走本类。
    /// </summary>
    public static class WorkEfficiencyResolver
    {
        /// <summary>激励/疲惫 合计效率（1 + 放大后的激励 + 削弱后的疲惫）。</summary>
        public static float ResolveIncentiveFatigueEfficiency(int levelTurnNumber = -1)
        {
            float sum = RelicEffectRunner.SumIncentiveFatigueLaborModifier(ResolveLevelTurn(levelTurnNumber));
            return Mathf.Max(0f, 1f + sum);
        }

        /// <summary>遗物总效率（除激励/疲惫外的全局工作效率遗物，1 + 叠加值）。</summary>
        public static float ResolveRelicTotalEfficiency(int levelTurnNumber = -1)
        {
            float sum = RelicEffectRunner.SumRelicLaborModifierExcludingIncentiveFatigue(
                ResolveLevelTurn(levelTurnNumber));
            return Mathf.Max(0f, 1f + sum);
        }

        /// <summary>快乐坨坨加成效率（1 + 全局工作效率光环）。</summary>
        public static float ResolveHappyTuotuoEfficiency()
        {
            return Mathf.Max(0f, 1f + JobAdvanceGatherMods.SumGlobalLaborEfficiencyAura());
        }

        /// <summary>灯芯草/双尾蛇等岗位光环加成（加在岗位自身效率上）。</summary>
        public static float ResolveJobAuraBonus(JobItem job, WorkEfficiencyScope scope)
        {
            switch (scope)
            {
                case WorkEfficiencyScope.Gather:
                    float aura = JobAdvanceGatherMods.SumIncomingGatherEfficiencyAura(job);
                    aura += JobAdvanceGatherMods.SumIncomingDesignatedPairAllYieldBonus(job);
                    return aura;
                case WorkEfficiencyScope.Process:
                    return JobAdvanceGatherMods.SumIncomingProcessEfficiencyAura()
                           + RelicEffectRunner.SumProcessLaborEfficiency();
                case WorkEfficiencyScope.Cook:
                    return JobAdvanceGatherMods.SumIncomingCookEfficiencyAura()
                           + RelicEffectRunner.SumCookLaborEfficiency();
                default:
                    return 0f;
            }
        }

        /// <summary>采集岗自身效率（含进阶惩罚/恢复；不含外来光环）。</summary>
        public static float ResolveGatherJobOwnEfficiency(
            JobItem job,
            JobAdvanceGatherMods mods,
            int workers,
            bool consumePendingPenalty = true)
        {
            float own = 1f;
            if (mods.EfficiencyPerWorker > 0f && workers > 0)
                own += workers * mods.EfficiencyPerWorker;

            var progression = JobProgressionManager.Instance;
            float pendingPenalty = 0f;
            if (progression != null)
            {
                pendingPenalty = consumePendingPenalty
                    ? progression.ConsumePendingGatherEfficiencyPenalty(job)
                    : progression.PeekPendingGatherEfficiencyPenalty(job);
            }

            if (pendingPenalty > 0f)
                own -= pendingPenalty;

            if (mods.GatherEfficiencyFlatPenalty > 0f)
            {
                own -= mods.GatherEfficiencyFlatPenalty;
                if (mods.GatherEfficiencyRecoverPerWorker > 0f && workers > 0)
                {
                    float recover = workers * mods.GatherEfficiencyRecoverPerWorker;
                    if (mods.GatherEfficiencyRecoverCap > 0f)
                        recover = Mathf.Min(recover, mods.GatherEfficiencyRecoverCap);
                    own += recover;
                }
            }

            float outputPenalty = JobAdvanceGatherMods.SumIncomingGatherOutputPenalty(job);
            if (outputPenalty > 0f)
                own *= Mathf.Max(0f, 1f - outputPenalty);

            return Mathf.Max(0f, own);
        }

        /// <summary>
        /// 岗位侧效率乘积（不含劳动力）：
        /// 激励/疲惫 × 遗物 × (岗位自身 + 光环) × 快乐坨坨。
        /// </summary>
        public static float ResolveJobEfficiencyProduct(
            JobItem job,
            WorkEfficiencyScope scope,
            JobAdvanceGatherMods gatherMods = default,
            int workers = 0,
            int levelTurnNumber = -1,
            bool consumePendingPenalty = true)
        {
            float incentiveFatigue = ResolveIncentiveFatigueEfficiency(levelTurnNumber);
            float relicTotal = ResolveRelicTotalEfficiency(levelTurnNumber);
            float aura = ResolveJobAuraBonus(job, scope);
            float own = scope == WorkEfficiencyScope.Gather
                ? ResolveGatherJobOwnEfficiency(job, gatherMods, workers, consumePendingPenalty)
                : 1f;
            float happy = ResolveHappyTuotuoEfficiency();

            return incentiveFatigue * relicTotal * (own + aura) * happy;
        }

        /// <summary>
        /// 采集物换算食材/风味的效率。
        /// 采集物数量已随人数缩放，劳动力按 pureLabor/workers 计入，避免重复乘人数。
        /// </summary>
        public static float ResolveGatherConversionEfficiency(
            JobItem job,
            JobAdvanceGatherMods gatherMods,
            float pureLabor,
            int workers,
            int levelTurnNumber = -1,
            bool consumePendingPenalty = true)
        {
            if (pureLabor <= 0f || workers <= 0) return 0f;

            float product = ResolveJobEfficiencyProduct(
                job, WorkEfficiencyScope.Gather, gatherMods, workers, levelTurnNumber, consumePendingPenalty);
            return product * pureLabor / workers;
        }

        /// <summary>悬停预览用：不消耗待结算效率惩罚。</summary>
        public static float PreviewGatherConversionEfficiency(
            JobItem job,
            JobAdvanceGatherMods gatherMods,
            float pureLabor,
            int workers,
            int levelTurnNumber = -1)
        {
            if (workers <= 0)
            {
                return ResolveJobEfficiencyProduct(
                    job, WorkEfficiencyScope.Gather, gatherMods, 0, levelTurnNumber,
                    consumePendingPenalty: false);
            }

            float labor = pureLabor > 0f ? pureLabor : workers;
            return ResolveGatherConversionEfficiency(
                job, gatherMods, labor, workers, levelTurnNumber, consumePendingPenalty: false);
        }

        /// <summary>处理/烹饪产能乘数（与 pureLabor × amountPerWorker 相乘）。</summary>
        public static float ResolveWorkCapacityMultiplier(
            JobItem job,
            WorkEfficiencyScope scope,
            int levelTurnNumber = -1)
        {
            return ResolveJobEfficiencyProduct(job, scope, default, 0, levelTurnNumber);
        }

        /// <summary>世界地图上岗位旁效率角标：有员工且倍率≠1 时展示。</summary>
        public static float ResolveStationDisplayMultiplier(JobItem job)
        {
            if (job == null) return 1f;

            var em = EmployeeManager.Instance;
            int workers = em != null ? em.GetAssignedCountOnJob(job) : 0;
            if (workers <= 0) return 1f;

            switch (job.JobType)
            {
                case JobType.Gather:
                {
                    var progression = JobProgressionManager.Instance;
                    var path = progression != null
                        ? progression.GetAdvancePath(job)
                        : JobAdvanceNodeId.None;
                    var mods = JobAdvanceGatherMods.From(job, path);
                    float labor = em.GetLaborOnJob(job);
                    return PreviewGatherConversionEfficiency(job, mods, labor, workers);
                }
                case JobType.Process:
                    return ResolveWorkCapacityMultiplier(job, WorkEfficiencyScope.Process);
                case JobType.Cook:
                    return ResolveWorkCapacityMultiplier(job, WorkEfficiencyScope.Cook);
                default:
                    return 1f;
            }
        }

        private static int ResolveLevelTurn(int levelTurnNumber)
        {
            if (levelTurnNumber >= 0) return levelTurnNumber;
            return RelicManager.GetLevelTurnNumber();
        }
    }
}
