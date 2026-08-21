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
                    return JobAdvanceGatherMods.SumIncomingProcessEfficiencyAura();
                case WorkEfficiencyScope.Cook:
                    return JobAdvanceGatherMods.SumIncomingCookEfficiencyAura();
                default:
                    return 0f;
            }
        }

        /// <summary>采集岗自身效率（含进阶惩罚/恢复；不含外来光环）。</summary>
        public static float ResolveGatherJobOwnEfficiency(
            JobItem job,
            JobAdvanceGatherMods mods,
            int workers)
        {
            float own = 1f;
            if (mods.EfficiencyPerWorker > 0f && workers > 0)
                own += workers * mods.EfficiencyPerWorker;

            var progression = JobProgressionManager.Instance;
            float pendingPenalty = progression != null
                ? progression.ConsumePendingGatherEfficiencyPenalty(job)
                : 0f;
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
            int levelTurnNumber = -1)
        {
            float incentiveFatigue = ResolveIncentiveFatigueEfficiency(levelTurnNumber);
            float relicTotal = ResolveRelicTotalEfficiency(levelTurnNumber);
            float aura = ResolveJobAuraBonus(job, scope);
            float own = scope == WorkEfficiencyScope.Gather
                ? ResolveGatherJobOwnEfficiency(job, gatherMods, workers)
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
            int levelTurnNumber = -1)
        {
            if (pureLabor <= 0f || workers <= 0) return 0f;

            float product = ResolveJobEfficiencyProduct(
                job, WorkEfficiencyScope.Gather, gatherMods, workers, levelTurnNumber);
            return product * pureLabor / workers;
        }

        /// <summary>处理/烹饪产能乘数（与 pureLabor × amountPerWorker 相乘）。</summary>
        public static float ResolveWorkCapacityMultiplier(
            JobItem job,
            WorkEfficiencyScope scope,
            int levelTurnNumber = -1)
        {
            return ResolveJobEfficiencyProduct(job, scope, default, 0, levelTurnNumber);
        }

        private static int ResolveLevelTurn(int levelTurnNumber)
        {
            if (levelTurnNumber >= 0) return levelTurnNumber;
            return RelicManager.GetLevelTurnNumber();
        }
    }
}
