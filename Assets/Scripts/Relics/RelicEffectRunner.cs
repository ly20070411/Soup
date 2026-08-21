using Soup.Employees;
using Soup.Game;
using Soup.Items;
using Soup.Jobs;
using System.Collections.Generic;
using UnityEngine;

namespace Soup.Relics
{
    /// <summary>
    /// Evaluates owned relic rules for a given trigger against RelicContext.
    /// </summary>
    public static class RelicEffectRunner
    {
        public static void Run(RelicTrigger trigger, RelicContext context)
        {
            if (context == null) return;
            if (trigger == RelicTrigger.Passive) return;

            var manager = RelicManager.Instance;
            if (manager == null) return;

            // Snapshot: effects may Acquire more relics mid-iteration.
            var owned = manager.Owned;
            var snapshot = new List<RelicItem>(owned.Count);
            for (int i = 0; i < owned.Count; i++)
                snapshot.Add(owned[i]);

            for (int i = 0; i < snapshot.Count; i++)
                RunRelic(snapshot[i], trigger, context);
        }

        public static void RunRelic(RelicItem relic, RelicTrigger trigger, RelicContext context)
        {
            if (relic == null || context == null || relic.Rules == null) return;
            if (trigger == RelicTrigger.Passive) return;

            for (int r = 0; r < relic.Rules.Count; r++)
            {
                var rule = relic.Rules[r];
                if (rule == null || rule.Trigger != trigger) continue;
                if (!EvaluateCondition(rule, context)) continue;
                ApplyEffect(rule, context);
            }
        }

        public static bool EvaluateCondition(RelicRule rule, RelicContext context)
        {
            if (rule == null || context == null) return false;

            switch (rule.Condition)
            {
                case RelicConditionType.Always:
                    return true;
                case RelicConditionType.NoCategoryGathered:
                    return context.GetGathered(rule.ConditionCategory) <= 0;
                case RelicConditionType.HasFlavorCountAtLeast:
                    return context.CountPresentFlavors() >= rule.ConditionInt;
                case RelicConditionType.HasFlavorCountAtMost:
                    return context.CountPresentFlavors() <= rule.ConditionInt;
                case RelicConditionType.TurnIndexInRange:
                {
                    int turn = context.LevelTurnNumber;
                    int min = rule.ConditionInt;
                    int max = rule.ConditionIntMax;
                    if (max < min)
                        (min, max) = (max, min);
                    return turn >= min && turn <= max;
                }
                default:
                    return false;
            }
        }

        public static void ApplyEffect(RelicRule rule, RelicContext context)
        {
            if (rule == null || context == null) return;

            switch (rule.Effect)
            {
                case RelicEffectType.AddFinalMultiplier:
                    context.FinalMultiplier += rule.FloatValue;
                    break;

                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                    context.FinalMultiplier += rule.FloatValue * context.CountPresentFlavors();
                    break;

                case RelicEffectType.DisableSpicyCap:
                    context.SpicyUncapped = true;
                    break;

                case RelicEffectType.AddSpicyScoreMultiplier:
                    context.SpicyScoreMultiplierBonus += rule.FloatValue;
                    break;

                case RelicEffectType.GrantIngredientPerGather:
                {
                    int every = rule.IntValue;
                    int grantAmount = rule.Amount;
                    var ingredient = ResolveSporeInvasionMushroom(rule.Ingredient);
                    if (every <= 0 || grantAmount <= 0 || ingredient == null) break;
                    if (context.ApplyYield == null) break;

                    int grants = context.GatheredUnits / every;
                    if (grants <= 0) break;

                    int units = grants * grantAmount;
                    var yield = IngredientYieldResolver.FromIngredient(ingredient, units);
                    context.ApplyYield(yield);
                    break;
                }

                case RelicEffectType.ModifyWarehouseCapacity:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null || rule.Amount == 0) break;
                    store.AddWarehouseCapacityBonus(rule.Amount);
                    break;
                }

                case RelicEffectType.AddRawMaterial:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null || rule.Amount == 0) break;
                    int added = store.AddRaw(rule.Material, rule.Amount);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                case RelicEffectType.AddProcessed:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null || rule.Amount == 0) break;
                    store.AddProcessed(rule.Amount);
                    if (context.Result != null && rule.Amount > 0)
                        context.Result.ProcessedGained += rule.Amount;
                    break;
                }

                case RelicEffectType.MultiplyIndependentScore:
                    if (rule.FloatValue > 0f)
                        context.IndependentMultiplier *= rule.FloatValue;
                    break;

                case RelicEffectType.ModifyElfCount:
                {
                    if (rule.Amount == 0) break;
                    ElfManager.Instance?.AddElves(rule.Amount);
                    break;
                }

                case RelicEffectType.GrantLinkedRelic:
                {
                    if (rule.LinkedRelic == null) break;
                    RelicManager.Instance?.Acquire(rule.LinkedRelic);
                    break;
                }

                case RelicEffectType.GrantRawPerRawProduced:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null) break;
                    int every = rule.IntValue;
                    int grant = rule.Amount;
                    if (every <= 0 || grant <= 0) break;

                    int produced = rule.Material == IngredientMaterial.Solid
                        ? context.SolidProducedThisBatch
                        : 0;
                    if (produced <= 0) break;

                    int extras = (produced / every) * grant;
                    if (extras <= 0) break;
                    int added = store.AddRaw(rule.Material, extras);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                case RelicEffectType.GrantRawPerGather:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null) break;
                    int every = rule.IntValue;
                    int grant = rule.Amount;
                    if (every <= 0 || grant <= 0) break;

                    int extras = (context.GatheredUnits / every) * grant;
                    if (extras <= 0) break;
                    int added = store.AddRaw(rule.Material, extras);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                case RelicEffectType.GrantSoftFromUnusedWarehousePercent:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null) break;
                    float pct = rule.FloatValue;
                    if (pct <= 0f) break;
                    int soft = Mathf.FloorToInt(context.PreviousUnusedWarehouse * pct);
                    if (soft <= 0) break;
                    int added = store.AddRaw(IngredientMaterial.Soft, soft);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                case RelicEffectType.ChanceGrantRandomRaw:
                {
                    var store = context.Store ?? ResourceStore.Instance;
                    if (store == null || rule.Amount <= 0) break;
                    if (Random.value > Mathf.Clamp01(rule.FloatValue)) break;
                    var mat = (IngredientMaterial)Random.Range(0, 3);
                    int added = store.AddRaw(mat, rule.Amount);
                    if (context.Result != null && added > 0)
                        context.Result.RawGained += added;
                    break;
                }

                // Passive-only effects — handled by query helpers below.
                case RelicEffectType.AddGlobalLaborEfficiency:
                case RelicEffectType.AddEmployeeTypeLaborEfficiency:
                case RelicEffectType.GrantEmployeeOnElfLoss:
                case RelicEffectType.AddGatherAmountPerWorker:
                case RelicEffectType.ReduceWarehouseWaste:
                case RelicEffectType.ConvertWasteToEqualGain:
                case RelicEffectType.AddColdScorePerUnit:
                case RelicEffectType.ReduceMagicConsumePercent:
                case RelicEffectType.OverrideSourTopTierPercent:
                    break;
            }
        }

        /// <summary>激励/疲惫遗物叠加值（不含 +1 底数）。</summary>
        public static float SumIncentiveFatigueLaborModifier(int levelTurnNumber)
        {
            float sum = 0f;
            float incentiveAmp = JobAdvanceGatherMods.SumIncentiveEffectAmplify();
            float fatigueReduce = JobAdvanceGatherMods.SumFatigueEffectReduce();

            ForEachPassiveRelicRule(levelTurnNumber, (relic, rule) =>
            {
                if (rule.Effect != RelicEffectType.AddGlobalLaborEfficiency) return;
                if (relic == null) return;
                if (relic.Id != RelicManager.IncentiveId && relic.Id != RelicManager.FatigueId) return;

                float value = rule.FloatValue;
                if (relic.Id == RelicManager.IncentiveId)
                    value *= 1f + incentiveAmp;
                else
                    value *= 1f - fatigueReduce;
                sum += value;
            });

            return sum;
        }

        /// <summary>除激励/疲惫外，全局工作效率遗物叠加值（不含 +1 底数）。</summary>
        public static float SumRelicLaborModifierExcludingIncentiveFatigue(int levelTurnNumber)
        {
            float sum = 0f;
            ForEachPassiveRelicRule(levelTurnNumber, (relic, rule) =>
            {
                if (rule.Effect != RelicEffectType.AddGlobalLaborEfficiency) return;
                if (relic != null
                    && (relic.Id == RelicManager.IncentiveId || relic.Id == RelicManager.FatigueId))
                    return;
                sum += rule.FloatValue;
            });
            return sum;
        }

        /// <summary>
        /// 全部工作效率加成叠加值（兼容旧接口；不含 +1 底数）。
        /// 不含快乐坨坨光环——该层由 <see cref="WorkEfficiencyResolver"/> 单独乘算。
        /// </summary>
        public static float SumGlobalLaborEfficiency(int levelTurnNumber)
        {
            return SumIncentiveFatigueLaborModifier(levelTurnNumber)
                   + SumRelicLaborModifierExcludingIncentiveFatigue(levelTurnNumber);
        }

        public static float SumEmployeeLaborEfficiency(string employeeTypeId, int levelTurnNumber)
        {
            if (string.IsNullOrEmpty(employeeTypeId)) return 0f;
            float sum = 0f;
            ForEachPassiveRelicRule(levelTurnNumber, (_, rule) =>
            {
                if (rule.Effect != RelicEffectType.AddEmployeeTypeLaborEfficiency) return;
                string id = string.IsNullOrEmpty(rule.EmployeeTypeId)
                    ? EmployeeManager.GhostId
                    : rule.EmployeeTypeId;
                if (id == employeeTypeId)
                    sum += rule.FloatValue;
            });
            return sum;
        }

        public static int SumGhostsPerElfLoss()
        {
            int sum = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.GrantEmployeeOnElfLoss)
                    sum += Mathf.Max(0, rule.Amount);
            });
            return sum;
        }

        /// <summary>Passive: extra gather amount-per-worker from relics (e.g. 丰饶祝福).</summary>
        public static int SumGatherAmountPerWorkerBonus()
        {
            int sum = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.AddGatherAmountPerWorker)
                    sum += rule.Amount;
            });
            return sum;
        }

        /// <summary>
        /// Passive: fraction of overflow waste prevented (0.75 = keep 75% that would be discarded).
        /// Clamped to [0, 1].
        /// </summary>
        public static float SumWarehouseWasteReduction()
        {
            float sum = 0f;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.ReduceWarehouseWaste)
                    sum += Mathf.Max(0f, rule.FloatValue);
            });
            return Mathf.Clamp01(sum);
        }

        /// <summary>
        /// Passive: wasted ingredients convert into processed food × multiplier.
        /// 0 = off; 回收器 amount=2 → 双倍.
        /// </summary>
        public static float WasteConvertMultiplier()
        {
            float mult = 0f;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect != RelicEffectType.ConvertWasteToEqualGain) return;
                float m = rule.Amount > 0 ? rule.Amount : 1f;
                if (m > mult) mult = m;
            });
            return mult;
        }

        /// <summary>Backward-compatible: any waste→gain relic present.</summary>
        public static bool ConvertsWasteToEqualGain() => WasteConvertMultiplier() > 0f;

        /// <summary>
        /// Passive: extra score per cold-cooked unit (冰点 amount=2 → 每份 2→4 分).
        /// </summary>
        public static int SumColdScorePerCookedUnit()
        {
            int sum = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.AddColdScorePerUnit)
                    sum += Mathf.Max(0, rule.Amount);
            });
            return sum;
        }

        /// <summary>Obsolete name; use <see cref="SumColdScorePerCookedUnit"/>.</summary>
        public static int SumColdScorePerUnit() => SumColdScorePerCookedUnit();

        /// <summary>Passive: fraction of magic consume rate removed (0.1 = −10%).</summary>
        public static float SumMagicConsumeReduction()
        {
            float sum = 0f;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect == RelicEffectType.ReduceMagicConsumePercent)
                    sum += Mathf.Max(0f, rule.FloatValue);
            });
            return Mathf.Clamp01(sum);
        }

        /// <summary>
        /// Passive: sour best-tier cooked percent override (e.g. 20). 0 = use default 10.
        /// </summary>
        public static int ResolveSourTopTierPercent(int defaultPercent = 10)
        {
            int best = 0;
            ForEachPassiveRelicRule(1, (_, rule) =>
            {
                if (rule.Effect != RelicEffectType.OverrideSourTopTierPercent) return;
                if (rule.IntValue > best) best = rule.IntValue;
            });
            return best > 0 ? best : Mathf.Max(1, defaultPercent);
        }

        private static void ForEachPassiveRule(int levelTurnNumber, System.Action<RelicRule> action)
        {
            ForEachPassiveRelicRule(levelTurnNumber, (_, rule) => action(rule));
        }

        private static void ForEachPassiveRelicRule(int levelTurnNumber, System.Action<RelicItem, RelicRule> action)
        {
            if (action == null) return;
            var manager = RelicManager.Instance;
            if (manager == null) return;

            var ctx = new RelicContext(ResourceStore.Instance, null)
            {
                LevelTurnNumber = Mathf.Max(1, levelTurnNumber)
            };

            var owned = manager.Owned;
            for (int i = 0; i < owned.Count; i++)
            {
                var relic = owned[i];
                if (relic == null || relic.Rules == null) continue;
                for (int r = 0; r < relic.Rules.Count; r++)
                {
                    var rule = relic.Rules[r];
                    if (rule == null || rule.Trigger != RelicTrigger.Passive) continue;
                    if (!EvaluateCondition(rule, ctx)) continue;
                    action(relic, rule);
                }
            }
        }

        /// <summary>孢子入侵：蘑菇种类跟随蘑菇岗当前进阶（变异/肥大/奇异）。</summary>
        private static IngredientItem ResolveSporeInvasionMushroom(IngredientItem fallback)
        {
            if (fallback == null || !IsBaseMushroomIngredient(fallback))
                return fallback;

            var jobs = JobManager.Instance;
            var progression = JobProgressionManager.Instance;
            if (jobs == null || progression == null)
                return fallback;

            var mushroomJob = jobs.GetById("mushroom") ?? jobs.FindByName("蘑菇");
            if (mushroomJob == null)
                return fallback;

            var advancePath = progression.GetAdvancePath(mushroomJob);
            var mods = JobAdvanceGatherMods.From(mushroomJob, advancePath);
            var baseIngredient = mushroomJob.OutputIngredient ?? fallback;

            if (!mods.HasVariant || mods.VariantIngredient == null)
                return baseIngredient;

            return UnityEngine.Random.value < mods.VariantChance
                ? mods.VariantIngredient
                : baseIngredient;
        }

        private static bool IsBaseMushroomIngredient(IngredientItem ingredient)
        {
            if (ingredient == null) return false;
            if (string.Equals(ingredient.Id, "mushroom", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(ingredient.Id, "mushroom_relic", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(ingredient.DisplayName, "蘑菇", System.StringComparison.Ordinal);
        }
    }
}
