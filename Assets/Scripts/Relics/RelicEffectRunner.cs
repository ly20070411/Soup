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

                case RelicEffectType.GrantIngredientPerGather:
                {
                    int every = rule.IntValue;
                    int grantAmount = rule.Amount;
                    var ingredient = rule.Ingredient;
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

                // Passive-only effects — handled by RelicManager queries.
                case RelicEffectType.AddGlobalLaborEfficiency:
                case RelicEffectType.AddEmployeeTypeLaborEfficiency:
                case RelicEffectType.GrantEmployeeOnElfLoss:
                    break;
            }
        }

        /// <summary>
        /// Sum Passive labor modifiers that pass their conditions for the given level turn.
        /// </summary>
        public static float SumGlobalLaborEfficiency(int levelTurnNumber)
        {
            float sum = 0f;
            ForEachPassiveRule(levelTurnNumber, rule =>
            {
                if (rule.Effect == RelicEffectType.AddGlobalLaborEfficiency)
                    sum += rule.FloatValue;
            });
            return sum;
        }

        public static float SumEmployeeLaborEfficiency(string employeeTypeId, int levelTurnNumber)
        {
            if (string.IsNullOrEmpty(employeeTypeId)) return 0f;
            float sum = 0f;
            ForEachPassiveRule(levelTurnNumber, rule =>
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
            ForEachPassiveRule(1, rule =>
            {
                if (rule.Effect == RelicEffectType.GrantEmployeeOnElfLoss)
                    sum += Mathf.Max(0, rule.Amount);
            });
            return sum;
        }

        private static void ForEachPassiveRule(int levelTurnNumber, System.Action<RelicRule> action)
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
                    action(rule);
                }
            }
        }
    }
}
