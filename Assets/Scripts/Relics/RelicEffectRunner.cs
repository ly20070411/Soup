using Soup.Items;
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

            var manager = RelicManager.Instance;
            if (manager == null) return;

            var owned = manager.Owned;
            for (int i = 0; i < owned.Count; i++)
            {
                var relic = owned[i];
                if (relic == null || relic.Rules == null) continue;

                for (int r = 0; r < relic.Rules.Count; r++)
                {
                    var rule = relic.Rules[r];
                    if (rule == null || rule.Trigger != trigger) continue;
                    if (!EvaluateCondition(rule, context)) continue;
                    ApplyEffect(rule, context);
                }
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
            }
        }
    }
}
