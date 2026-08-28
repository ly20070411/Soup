using System.Collections.Generic;
using Soup.Employees;
using Soup.Items;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// 采集岗沿进阶路径汇总后的数值修正。
    /// </summary>
    public struct JobAdvanceGatherMods
    {
        /// <summary>0 = 使用岗位基础每精灵产出。</summary>
        public int GatherAmountOverride;
        public int SoftPerUnitBonus;
        public int SolidPerUnitBonus;
        public int ToughPerUnitBonus;
        public int ColdPerUnitBonus;
        public int SpicyPerUnitBonus;
        public int SourPerUnitBonus;
        public int MagicPerUnitBonus;
        public int RandomFlavorPerUnitBonus;
        /// <summary>每个在岗员工叠加的效率（如 0.08）。</summary>
        public float EfficiencyPerWorker;
        public int SoftPerUnitWhenFull;
        public int FlatSoftBonus;
        public int FlatToughBonus;
        public int FlatSolidBonus;
        public int PermanentElfBonus;
        public float AllGatherEfficiencyPerWorker;
        public float AllGatherEfficiencyCap;
        public float DesignatedGatherEfficiencyPerWorker;
        public float DesignatedGatherEfficiencyCap;
        public float AllProcessEfficiencyPerWorker;
        public float AllProcessEfficiencyCap;
        public float AllCookEfficiencyPerWorker;
        public float AllCookEfficiencyCap;
        public float OtherGatherOutputPenalty;
        public float EndTurnRawWasteFraction;
        public int IncentivePerEmployeeGained;
        public int TopFlavorBonus;
        public int TopFlavorTieBonus;
        public int TopMaterialBonus;
        public float GatherAmountMultiplier;
        public int DestroyedJobsOutputPerTurn;
        public float ColdScoreMultiplierBonus;
        public float SpicyScoreMultiplierBonus;
        public float SourScoreMultiplierBonus;
        public float MagicScoreMultiplierBonus;
        public float FlavorScoreMultiplierBonus;
        public int OtherGatherColdPerUnit;
        public int OtherGatherSpicyPerUnit;
        public int OtherGatherSourPerUnit;
        public int OtherGatherMagicPerUnit;
        public int OtherGatherRandomFlavorPerUnit;
        public IngredientItem VariantIngredient;
        public float VariantChance;
        public IngredientItem BonusIngredient;
        public int BonusIngredientAmount;
        public int ConvertConsumeSoftOrSolidPerUnit;
        public int ConvertGainToughPerUnit;
        public int ConvertGainTopFlavorPerUnit;
        public bool ForceOutputMaterial;
        public IngredientMaterial ForcedOutputMaterial;
        public int FlatRandomMaterialBonus;
        public float GatherEfficiencyFlatPenalty;
        public float GatherEfficiencyRecoverPerWorker;
        public float GatherEfficiencyRecoverCap;
        public float NextTurnGatherEfficiencyPenalty;
        public float IncentiveEffectAmplifyPerWorker;
        public float FatigueEffectReducePerWorker;
        public float AllGlobalLaborEfficiencyPerWorker;
        public float AllGlobalLaborEfficiencyCap;
        public float EndTurnIncentiveChancePerWorker;
        public int EndTurnIncentiveMaxPerLevel;
        public bool SuppressRawMaterialOutput;
        public int FlatColdBonus;
        public int FlatSpicyBonus;
        public float DesignatedPairFlavorYieldBonus;
        public float DesignatedPairAllYieldBonus;
        public int SolidPerUnusedWarehouseThreshold;
        public int SolidPerUnusedWarehouseAmount;
        public int SolidPerWarehouseCapacityThreshold;
        public int SolidPerWarehouseCapacityAmount;
        public int SolidPerWarehouseSolidThreshold;
        public int SolidPerWarehouseSolidAmount;
        public int GatherUnitsPerProcessedThreshold;
        public int GatherUnitsPerProcessedAmount;

        public bool HasVariant =>
            VariantIngredient != null && VariantChance > 0f;

        public bool HasBonusIngredient =>
            BonusIngredient != null && BonusIngredientAmount > 0;

        /// <summary>进阶改为只产新采集物（如大团球/巨团山），不再产原岗食材。</summary>
        public bool IsReplacementGatherOutput =>
            SuppressRawMaterialOutput && HasBonusIngredient;

        /// <summary>
        /// suppress 仅作用于本岗基础采集物；替代产出食材（大团球/巨团山等）仍应保留其材质。
        /// </summary>
        public bool ShouldSuppressYieldFor(IngredientItem ingredient)
        {
            if (!SuppressRawMaterialOutput) return false;
            if (HasBonusIngredient
                && ingredient != null
                && BonusIngredient != null
                && ingredient.Id == BonusIngredient.Id)
                return false;
            return true;
        }

        public bool HasSoftOrSolidConvert =>
            ConvertConsumeSoftOrSolidPerUnit > 0 && ConvertGainToughPerUnit > 0;

        public bool HasForcedOutputMaterial => ForceOutputMaterial;

        /// <summary>
        /// 棍棍虫等：按回合开始时的仓库快照换算额外坚固（空仓/总容/囤固）。
        /// </summary>
        public int ComputeWarehouseScaledSolidBonus(
            int unusedWarehouseSpace,
            int warehouseCapacity,
            int solidStock)
        {
            int solid = 0;
            if (SolidPerUnusedWarehouseThreshold > 0 && SolidPerUnusedWarehouseAmount > 0)
            {
                if (unusedWarehouseSpace > 0 && unusedWarehouseSpace < int.MaxValue)
                {
                    solid += (unusedWarehouseSpace / SolidPerUnusedWarehouseThreshold)
                             * SolidPerUnusedWarehouseAmount;
                }
            }

            if (SolidPerWarehouseCapacityThreshold > 0 && SolidPerWarehouseCapacityAmount > 0
                && warehouseCapacity > 0)
            {
                solid += (warehouseCapacity / SolidPerWarehouseCapacityThreshold)
                         * SolidPerWarehouseCapacityAmount;
            }

            if (SolidPerWarehouseSolidThreshold > 0 && SolidPerWarehouseSolidAmount > 0)
            {
                int stock = Mathf.Max(0, solidStock);
                solid += (stock / SolidPerWarehouseSolidThreshold)
                         * SolidPerWarehouseSolidAmount;
            }

            return solid;
        }

        public static JobAdvanceGatherMods From(JobItem job, JobAdvanceNodeId path)
        {
            var mods = new JobAdvanceGatherMods();
            if (job == null || path == JobAdvanceNodeId.None)
                return mods;

            var progression = JobProgressionManager.Instance;
            if (progression != null)
                job = progression.ResolveGatherDefinition(job);

            var chain = new List<JobAdvanceNodeId>(JobAdvancePath.MaxDepth);
            JobAdvancePath.GetChain(path, chain);
            for (int i = 0; i < chain.Count; i++)
            {
                var node = job.GetAdvanceNode(chain[i]);
                if (node == null) continue;

                if (node.GatherAmountOverride > 0)
                    mods.GatherAmountOverride = node.GatherAmountOverride;

                mods.SoftPerUnitBonus += node.SoftPerUnitBonus;
                mods.SolidPerUnitBonus += node.SolidPerUnitBonus;
                mods.ToughPerUnitBonus += node.ToughPerUnitBonus;
                mods.ColdPerUnitBonus += node.ColdPerUnitBonus;
                mods.SpicyPerUnitBonus += node.SpicyPerUnitBonus;
                mods.SourPerUnitBonus += node.SourPerUnitBonus;
                mods.MagicPerUnitBonus += node.MagicPerUnitBonus;
                mods.RandomFlavorPerUnitBonus += node.RandomFlavorPerUnitBonus;
                mods.ColdScoreMultiplierBonus += node.ColdScoreMultiplierBonus;
                mods.SpicyScoreMultiplierBonus += node.SpicyScoreMultiplierBonus;
                mods.SourScoreMultiplierBonus += node.SourScoreMultiplierBonus;
                mods.MagicScoreMultiplierBonus += node.MagicScoreMultiplierBonus;
                mods.FlavorScoreMultiplierBonus += node.FlavorScoreMultiplierBonus;
                mods.OtherGatherColdPerUnit += node.OtherGatherColdPerUnit;
                mods.OtherGatherSpicyPerUnit += node.OtherGatherSpicyPerUnit;
                mods.OtherGatherSourPerUnit += node.OtherGatherSourPerUnit;
                mods.OtherGatherMagicPerUnit += node.OtherGatherMagicPerUnit;
                mods.OtherGatherRandomFlavorPerUnit += node.OtherGatherRandomFlavorPerUnit;
                mods.FlatSoftBonus += node.FlatSoftBonus;
                mods.FlatToughBonus += node.FlatToughBonus;
                mods.FlatSolidBonus += node.FlatSolidBonus;
                mods.PermanentElfBonus += node.PermanentElfBonus;
                mods.IncentivePerEmployeeGained += node.IncentivePerEmployeeGained;

                if (node.TopFlavorBonus > 0)
                    mods.TopFlavorBonus = node.TopFlavorBonus;
                if (node.TopFlavorTieBonus > 0)
                    mods.TopFlavorTieBonus = node.TopFlavorTieBonus;
                if (node.TopMaterialBonus > 0)
                    mods.TopMaterialBonus = node.TopMaterialBonus;

                if (node.GatherAmountMultiplier > 0f)
                    mods.GatherAmountMultiplier = node.GatherAmountMultiplier;

                if (node.DestroyedJobsOutputPerTurn > 0)
                    mods.DestroyedJobsOutputPerTurn = node.DestroyedJobsOutputPerTurn;

                if (node.EfficiencyPerWorker > 0f)
                    mods.EfficiencyPerWorker = node.EfficiencyPerWorker;

                mods.SoftPerUnitWhenFull += node.SoftPerUnitWhenFull;

                if (node.AllGatherEfficiencyPerWorker > 0f)
                {
                    mods.AllGatherEfficiencyPerWorker = node.AllGatherEfficiencyPerWorker;
                    mods.AllGatherEfficiencyCap = node.AllGatherEfficiencyCap;
                    mods.DesignatedGatherEfficiencyPerWorker = 0f;
                    mods.DesignatedGatherEfficiencyCap = 0f;
                }

                if (node.DesignatedGatherEfficiencyPerWorker > 0f)
                {
                    mods.DesignatedGatherEfficiencyPerWorker = node.DesignatedGatherEfficiencyPerWorker;
                    mods.DesignatedGatherEfficiencyCap = node.DesignatedGatherEfficiencyCap;
                    mods.AllGatherEfficiencyPerWorker = 0f;
                    mods.AllGatherEfficiencyCap = 0f;
                }

                if (node.AllProcessEfficiencyPerWorker > 0f)
                {
                    mods.AllProcessEfficiencyPerWorker = node.AllProcessEfficiencyPerWorker;
                    mods.AllProcessEfficiencyCap = node.AllProcessEfficiencyCap;
                }

                if (node.AllCookEfficiencyPerWorker > 0f)
                {
                    mods.AllCookEfficiencyPerWorker = node.AllCookEfficiencyPerWorker;
                    mods.AllCookEfficiencyCap = node.AllCookEfficiencyCap;
                }

                if (node.OtherGatherOutputPenalty > 0f)
                    mods.OtherGatherOutputPenalty = node.OtherGatherOutputPenalty;

                if (node.EndTurnRawWasteFraction > 0f)
                    mods.EndTurnRawWasteFraction = node.EndTurnRawWasteFraction;

                if (node.VariantIngredient != null && node.VariantChance > 0f)
                {
                    mods.VariantIngredient = node.VariantIngredient;
                    mods.VariantChance = node.VariantChance;
                }

                if (node.BonusIngredient != null && node.BonusIngredientAmount > 0)
                {
                    mods.BonusIngredient = node.BonusIngredient;
                    mods.BonusIngredientAmount = node.BonusIngredientAmount;
                }

                if (node.ConvertConsumeSoftOrSolidPerUnit > 0)
                {
                    mods.ConvertConsumeSoftOrSolidPerUnit = node.ConvertConsumeSoftOrSolidPerUnit;
                    mods.ConvertGainToughPerUnit = node.ConvertGainToughPerUnit;
                    mods.ConvertGainTopFlavorPerUnit = node.ConvertGainTopFlavorPerUnit;
                }

                if (node.ForceOutputMaterial)
                {
                    mods.ForceOutputMaterial = true;
                    mods.ForcedOutputMaterial = node.ForcedOutputMaterial;
                }

                if (node.FlatRandomMaterialBonus > 0)
                    mods.FlatRandomMaterialBonus += node.FlatRandomMaterialBonus;

                if (node.GatherEfficiencyFlatPenalty > 0f)
                {
                    mods.GatherEfficiencyFlatPenalty = node.GatherEfficiencyFlatPenalty;
                    mods.GatherEfficiencyRecoverPerWorker = node.GatherEfficiencyRecoverPerWorker;
                    mods.GatherEfficiencyRecoverCap = node.GatherEfficiencyRecoverCap;
                }

                if (node.NextTurnGatherEfficiencyPenalty > 0f)
                    mods.NextTurnGatherEfficiencyPenalty = node.NextTurnGatherEfficiencyPenalty;

                if (node.IncentiveEffectAmplifyPerWorker > 0f || node.FatigueEffectReducePerWorker > 0f)
                {
                    mods.IncentiveEffectAmplifyPerWorker = node.IncentiveEffectAmplifyPerWorker;
                    mods.FatigueEffectReducePerWorker = node.FatigueEffectReducePerWorker;
                }

                if (node.AllGlobalLaborEfficiencyPerWorker > 0f)
                {
                    mods.AllGlobalLaborEfficiencyPerWorker = node.AllGlobalLaborEfficiencyPerWorker;
                    mods.AllGlobalLaborEfficiencyCap = node.AllGlobalLaborEfficiencyCap;
                }

                if (node.EndTurnIncentiveChancePerWorker > 0f)
                {
                    mods.EndTurnIncentiveChancePerWorker = node.EndTurnIncentiveChancePerWorker;
                    mods.EndTurnIncentiveMaxPerLevel = node.EndTurnIncentiveMaxPerLevel;
                }

                if (node.SuppressRawMaterialOutput)
                    mods.SuppressRawMaterialOutput = true;

                mods.FlatColdBonus += node.FlatColdBonus;
                mods.FlatSpicyBonus += node.FlatSpicyBonus;

                if (node.DesignatedPairAllYieldBonus > 0f)
                {
                    mods.DesignatedPairAllYieldBonus = node.DesignatedPairAllYieldBonus;
                    mods.DesignatedPairFlavorYieldBonus = 0f;
                }

                if (node.DesignatedPairFlavorYieldBonus > 0f)
                {
                    mods.DesignatedPairFlavorYieldBonus = node.DesignatedPairFlavorYieldBonus;
                    mods.DesignatedPairAllYieldBonus = 0f;
                }

                if (node.SolidPerUnusedWarehouseThreshold > 0 && node.SolidPerUnusedWarehouseAmount > 0)
                {
                    mods.SolidPerUnusedWarehouseThreshold = node.SolidPerUnusedWarehouseThreshold;
                    mods.SolidPerUnusedWarehouseAmount = node.SolidPerUnusedWarehouseAmount;
                    mods.SolidPerWarehouseCapacityThreshold = 0;
                    mods.SolidPerWarehouseCapacityAmount = 0;
                }

                if (node.SolidPerWarehouseCapacityThreshold > 0 && node.SolidPerWarehouseCapacityAmount > 0)
                {
                    mods.SolidPerWarehouseCapacityThreshold = node.SolidPerWarehouseCapacityThreshold;
                    mods.SolidPerWarehouseCapacityAmount = node.SolidPerWarehouseCapacityAmount;
                    mods.SolidPerUnusedWarehouseThreshold = 0;
                    mods.SolidPerUnusedWarehouseAmount = 0;
                }

                if (node.SolidPerWarehouseSolidThreshold > 0 && node.SolidPerWarehouseSolidAmount > 0)
                {
                    mods.SolidPerWarehouseSolidThreshold = node.SolidPerWarehouseSolidThreshold;
                    mods.SolidPerWarehouseSolidAmount = node.SolidPerWarehouseSolidAmount;
                }

                if (node.GatherUnitsPerProcessedThreshold > 0 && node.GatherUnitsPerProcessedAmount > 0)
                {
                    mods.GatherUnitsPerProcessedThreshold = node.GatherUnitsPerProcessedThreshold;
                    mods.GatherUnitsPerProcessedAmount = node.GatherUnitsPerProcessedAmount;
                }
            }

            return mods;
        }

        public int ResolveAmountPerWorker(JobItem job)
        {
            int amount = GatherAmountOverride > 0
                ? GatherAmountOverride
                : (job != null ? job.GatherAmountPerWorker : 0);
            if (GatherAmountMultiplier > 0f && !Mathf.Approximately(GatherAmountMultiplier, 1f))
                amount = Mathf.CeilToInt(amount * GatherAmountMultiplier);
            return amount;
        }

        /// <summary>
        /// 替代产出模式下的采集份数：每员工 BonusIngredientAmount 份 + 遗物每精灵加成（如丰饶祝福）。
        /// </summary>
        public int ResolveReplacementOutputUnits(int workers, int scaledUnits, int relicBonusPerWorker)
        {
            if (!IsReplacementGatherOutput)
                return scaledUnits;

            int extra = relicBonusPerWorker > 0 && workers > 0
                ? Mathf.CeilToInt(workers * relicBonusPerWorker)
                : 0;

            if (BonusIngredientAmount > 0 && workers > 0)
                return BonusIngredientAmount * workers + extra;

            if (BonusIngredientAmount > 0)
                return BonusIngredientAmount;

            return scaledUnits + extra;
        }

        /// <summary>已摧毁岗位每回合额外产出的份数（取各岗进阶路径最大值之和；通常仅小白花 2-2）。</summary>
        public static int SumDestroyedJobsOutputPerTurn()
        {
            int best = 0;
            var progression = JobProgressionManager.Instance;
            if (progression == null) return 0;

            foreach (var job in progression.GetUnlocked(JobType.Gather))
            {
                if (job == null) continue;
                var mods = From(job, progression.GetAdvancePath(job));
                if (mods.DestroyedJobsOutputPerTurn > best)
                    best = mods.DestroyedJobsOutputPerTurn;
            }

            return best;
        }

        /// <summary>提供摧毁产出效果的岗位（用于取其 OutputIngredient）。</summary>
        public static JobItem FindDestroyedOutputSourceJob()
        {
            var progression = JobProgressionManager.Instance;
            if (progression == null) return null;

            JobItem best = null;
            int bestAmount = 0;
            foreach (var job in progression.GetUnlocked(JobType.Gather))
            {
                if (job == null) continue;
                var mods = From(job, progression.GetAdvancePath(job));
                if (mods.DestroyedJobsOutputPerTurn > bestAmount)
                {
                    bestAmount = mods.DestroyedJobsOutputPerTurn;
                    best = job;
                }
            }

            return best;
        }

        public static float SumColdScoreMultiplierBonus() =>
            SumUnlockedGather(m => m.ColdScoreMultiplierBonus + m.FlavorScoreMultiplierBonus);

        public static float SumSpicyScoreMultiplierBonus() =>
            SumUnlockedGather(m => m.SpicyScoreMultiplierBonus + m.FlavorScoreMultiplierBonus);

        public static float SumSourScoreMultiplierBonus() =>
            SumUnlockedGather(m => m.SourScoreMultiplierBonus + m.FlavorScoreMultiplierBonus);

        public static float SumMagicScoreMultiplierBonus() =>
            SumUnlockedGather(m => m.MagicScoreMultiplierBonus + m.FlavorScoreMultiplierBonus);

        public static int SumOtherGatherColdAura(JobItem self) =>
            SumUnlockedGatherExcept(self, m => m.OtherGatherColdPerUnit);

        public static int SumOtherGatherSpicyAura(JobItem self) =>
            SumUnlockedGatherExcept(self, m => m.OtherGatherSpicyPerUnit);

        public static int SumOtherGatherSourAura(JobItem self) =>
            SumUnlockedGatherExcept(self, m => m.OtherGatherSourPerUnit);

        public static int SumOtherGatherMagicAura(JobItem self) =>
            SumUnlockedGatherExcept(self, m => m.OtherGatherMagicPerUnit);

        public static int SumOtherGatherRandomFlavorAura(JobItem self) =>
            SumUnlockedGatherExcept(self, m => m.OtherGatherRandomFlavorPerUnit);

        public static int SumPermanentElfBonus() =>
            SumUnlockedGather(m => m.PermanentElfBonus);

        public static int SumIncentivePerEmployeeGained() =>
            SumUnlockedGather(m => m.IncentivePerEmployeeGained);

        /// <summary>
        /// 其他岗位对本岗施加的采集效率光环（全采集 / 指定采集）。
        /// </summary>
        public static float SumIncomingGatherEfficiencyAura(JobItem target)
        {
            if (target == null || target.JobType != JobType.Gather) return 0f;

            float sum = 0f;
            var progression = JobProgressionManager.Instance;
            var employees = EmployeeManager.Instance;
            if (progression == null || employees == null) return 0f;

            foreach (var source in progression.GetUnlocked(JobType.Gather))
            {
                if (source == null) continue;
                var mods = From(source, progression.GetAdvancePath(source));
                int workers = employees.GetAssignedCountOnJob(source);
                if (workers <= 0) continue;

                if (mods.AllGatherEfficiencyPerWorker > 0f)
                {
                    float bonus = workers * mods.AllGatherEfficiencyPerWorker;
                    if (mods.AllGatherEfficiencyCap > 0f)
                        bonus = Mathf.Min(bonus, mods.AllGatherEfficiencyCap);
                    sum += bonus;
                }

                if (mods.DesignatedGatherEfficiencyPerWorker > 0f)
                {
                    var designated = progression.GetDesignatedGatherAuraTarget(source);
                    if (designated != null && ReferenceEquals(designated, target))
                    {
                        float bonus = workers * mods.DesignatedGatherEfficiencyPerWorker;
                        if (mods.DesignatedGatherEfficiencyCap > 0f)
                            bonus = Mathf.Min(bonus, mods.DesignatedGatherEfficiencyCap);
                        sum += bonus;
                    }
                }
            }

            return Mathf.Max(0f, sum);
        }

        /// <summary>其他采集岗对本岗造成的产量惩罚（0~1，如 0.25 = 减产 25%）。取各来源最大值。</summary>
        public static float SumIncomingGatherOutputPenalty(JobItem target)
        {
            float best = 0f;
            var progression = JobProgressionManager.Instance;
            if (progression == null || target == null) return 0f;

            foreach (var source in progression.GetUnlocked(JobType.Gather))
            {
                if (source == null || source == target) continue;
                var mods = From(source, progression.GetAdvancePath(source));
                if (mods.OtherGatherOutputPenalty > best)
                    best = mods.OtherGatherOutputPenalty;
            }

            return Mathf.Clamp01(best);
        }

        /// <summary>采集岗为所有处理岗提供的效率光环总和。</summary>
        public static float SumIncomingProcessEfficiencyAura()
        {
            return SumJobTypeEfficiencyAura(
                m => m.AllProcessEfficiencyPerWorker,
                m => m.AllProcessEfficiencyCap);
        }

        /// <summary>采集岗为所有烹饪岗提供的效率光环总和。</summary>
        public static float SumIncomingCookEfficiencyAura()
        {
            return SumJobTypeEfficiencyAura(
                m => m.AllCookEfficiencyPerWorker,
                m => m.AllCookEfficiencyCap);
        }

        /// <summary>已解锁采集岗中，烹饪结束后浪费未处理食材的最大比例。</summary>
        public static float MaxEndTurnRawWasteFraction()
        {
            float best = 0f;
            var progression = JobProgressionManager.Instance;
            if (progression == null) return 0f;

            foreach (var job in progression.GetUnlocked(JobType.Gather))
            {
                if (job == null) continue;
                var mods = From(job, progression.GetAdvancePath(job));
                if (mods.EndTurnRawWasteFraction > best)
                    best = mods.EndTurnRawWasteFraction;
            }

            return Mathf.Clamp01(best);
        }

        /// <summary>快乐坨坨等：在岗员工使激励效果放大的总和。</summary>
        public static float SumIncentiveEffectAmplify()
        {
            return SumWorkerScaledGatherAura(m => m.IncentiveEffectAmplifyPerWorker, _ => 0f);
        }

        /// <summary>快乐坨坨等：在岗员工使疲惫效果减弱的总和（再 clamp 到 0~1）。</summary>
        public static float SumFatigueEffectReduce()
        {
            return Mathf.Clamp01(SumWorkerScaledGatherAura(m => m.FatigueEffectReducePerWorker, _ => 0f));
        }

        /// <summary>快乐坨坨等：在岗员工提供的全局工作效率光环。</summary>
        public static float SumGlobalLaborEfficiencyAura()
        {
            return SumWorkerScaledGatherAura(
                m => m.AllGlobalLaborEfficiencyPerWorker,
                m => m.AllGlobalLaborEfficiencyCap);
        }

        /// <summary>指定岗配对：目标岗或来源岗获得的风味产量加成。</summary>
        public static float SumIncomingDesignatedPairFlavorYieldBonus(JobItem target)
        {
            return SumIncomingDesignatedPairBonus(target, m => m.DesignatedPairFlavorYieldBonus);
        }

        /// <summary>指定岗配对：目标岗或来源岗获得的全部产量加成。</summary>
        public static float SumIncomingDesignatedPairAllYieldBonus(JobItem target)
        {
            return SumIncomingDesignatedPairBonus(target, m => m.DesignatedPairAllYieldBonus);
        }

        private static float SumIncomingDesignatedPairBonus(
            JobItem target,
            System.Func<JobAdvanceGatherMods, float> selector)
        {
            if (target == null || target.JobType != JobType.Gather) return 0f;
            float best = 0f;
            var progression = JobProgressionManager.Instance;
            if (progression == null) return 0f;

            foreach (var source in progression.GetUnlocked(JobType.Gather))
            {
                if (source == null) continue;
                var mods = From(source, progression.GetAdvancePath(source));
                float bonus = selector(mods);
                if (bonus <= 0f) continue;

                bool affectsSelf = ReferenceEquals(source, target);
                var designated = progression.GetDesignatedGatherAuraTarget(source);
                bool affectsDesignated = designated != null && ReferenceEquals(designated, target);
                if (!affectsSelf && !affectsDesignated) continue;
                if (bonus > best)
                    best = bonus;
            }

            return Mathf.Max(0f, best);
        }

        /// <summary>回合结束按在岗人数掷骰产激励的来源岗位（取概率最大者）。</summary>
        public static bool TryGetEndTurnIncentiveRoll(out JobItem sourceJob, out float chancePerWorker, out int maxPerLevel)
        {
            sourceJob = null;
            chancePerWorker = 0f;
            maxPerLevel = 0;
            var progression = JobProgressionManager.Instance;
            if (progression == null) return false;

            var employees = EmployeeManager.Instance;
            foreach (var job in progression.GetUnlocked(JobType.Gather))
            {
                if (job == null) continue;
                // 只考虑当前有在岗员工的岗位，避免「排序第一的最大概率岗无人」
                // 导致整回合激励产出为 0（我爱坨坨多岗共享路径时尤其明显）。
                int workers = employees != null ? employees.GetAssignedCountOnJob(job) : 0;
                if (workers <= 0) continue;
                var mods = From(job, progression.GetAdvancePath(job));
                if (mods.EndTurnIncentiveChancePerWorker <= chancePerWorker) continue;
                chancePerWorker = mods.EndTurnIncentiveChancePerWorker;
                maxPerLevel = mods.EndTurnIncentiveMaxPerLevel;
                sourceJob = job;
            }

            return sourceJob != null && chancePerWorker > 0f && maxPerLevel > 0;
        }

        private static float SumWorkerScaledGatherAura(
            System.Func<JobAdvanceGatherMods, float> perWorkerSelector,
            System.Func<JobAdvanceGatherMods, float> capSelector)
        {
            float sum = 0f;
            var progression = JobProgressionManager.Instance;
            var employees = EmployeeManager.Instance;
            if (progression == null || employees == null) return 0f;

            foreach (var source in progression.GetUnlocked(JobType.Gather))
            {
                if (source == null) continue;
                var mods = From(source, progression.GetAdvancePath(source));
                float perWorker = perWorkerSelector(mods);
                if (perWorker <= 0f) continue;

                int workers = employees.GetAssignedCountOnJob(source);
                if (workers <= 0) continue;

                float bonus = workers * perWorker;
                float cap = capSelector(mods);
                if (cap > 0f)
                    bonus = Mathf.Min(bonus, cap);
                sum += bonus;
            }

            return Mathf.Max(0f, sum);
        }

        private static float SumJobTypeEfficiencyAura(
            System.Func<JobAdvanceGatherMods, float> perWorkerSelector,
            System.Func<JobAdvanceGatherMods, float> capSelector)
        {
            float sum = 0f;
            var progression = JobProgressionManager.Instance;
            var employees = EmployeeManager.Instance;
            if (progression == null || employees == null) return 0f;

            foreach (var source in progression.GetUnlocked(JobType.Gather))
            {
                if (source == null) continue;
                var mods = From(source, progression.GetAdvancePath(source));
                float perWorker = perWorkerSelector(mods);
                if (perWorker <= 0f) continue;

                int workers = employees.GetAssignedCountOnJob(source);
                if (workers <= 0) continue;

                float bonus = workers * perWorker;
                float cap = capSelector(mods);
                if (cap > 0f)
                    bonus = Mathf.Min(bonus, cap);
                sum += bonus;
            }

            return Mathf.Max(0f, sum);
        }

        private static float SumUnlockedGather(System.Func<JobAdvanceGatherMods, float> selector)
        {
            float sum = 0f;
            var progression = JobProgressionManager.Instance;
            if (progression == null) return 0f;

            foreach (var job in progression.GetUnlocked(JobType.Gather))
            {
                if (job == null) continue;
                sum += selector(From(job, progression.GetAdvancePath(job)));
            }

            return Mathf.Max(0f, sum);
        }

        private static int SumUnlockedGather(System.Func<JobAdvanceGatherMods, int> selector)
        {
            int sum = 0;
            var progression = JobProgressionManager.Instance;
            if (progression == null) return 0;

            foreach (var job in progression.GetUnlocked(JobType.Gather))
            {
                if (job == null) continue;
                sum += selector(From(job, progression.GetAdvancePath(job)));
            }

            return Mathf.Max(0, sum);
        }

        private static int SumUnlockedGatherExcept(JobItem self, System.Func<JobAdvanceGatherMods, int> selector)
        {
            int sum = 0;
            var progression = JobProgressionManager.Instance;
            if (progression == null) return 0;

            foreach (var job in progression.GetUnlocked(JobType.Gather))
            {
                if (job == null || job == self) continue;
                sum += selector(From(job, progression.GetAdvancePath(job)));
            }

            return Mathf.Max(0, sum);
        }
    }
}
