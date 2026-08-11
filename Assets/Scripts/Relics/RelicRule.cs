using System;
using Soup.Items;
using UnityEngine;

namespace Soup.Relics
{
    /// <summary>
    /// One cause→effect row on a relic: trigger + condition + effect with shared params.
    /// </summary>
    [Serializable]
    public class RelicRule
    {
        [SerializeField] private RelicTrigger trigger = RelicTrigger.AfterScore;
        [SerializeField] private RelicConditionType condition = RelicConditionType.Always;
        [SerializeField] private IngredientCategory conditionCategory = IngredientCategory.Meat;
        [SerializeField, Min(0)] private int conditionInt = 1;

        [SerializeField] private RelicEffectType effect = RelicEffectType.AddFinalMultiplier;
        [SerializeField] private float floatValue = 0.6f;
        [SerializeField, Min(0)] private int intValue = 5;
        [SerializeField, Min(0)] private int amount = 1;
        [SerializeField] private IngredientItem ingredient;

        public RelicTrigger Trigger => trigger;
        public RelicConditionType Condition => condition;
        public IngredientCategory ConditionCategory => conditionCategory;
        public int ConditionInt => conditionInt;
        public RelicEffectType Effect => effect;
        public float FloatValue => floatValue;
        public int IntValue => intValue;
        public int Amount => amount;
        public IngredientItem Ingredient => ingredient;

        public void SetTrigger(RelicTrigger value) => trigger = value;

        public void SetCondition(RelicConditionType type, IngredientCategory category, int conditionValue)
        {
            condition = type;
            conditionCategory = category;
            conditionInt = Mathf.Max(0, conditionValue);
        }

        public void SetEffect(
            RelicEffectType type,
            float fValue,
            int iValue,
            int grantAmount,
            IngredientItem item)
        {
            effect = type;
            floatValue = fValue;
            intValue = Mathf.Max(0, iValue);
            amount = Mathf.Max(0, grantAmount);
            ingredient = item;
        }

        public string ToSummary()
        {
            string cond = ConditionSummary();
            string eff = EffectSummary();
            return $"{TriggerLabel(trigger)} | {cond} → {eff}";
        }

        public string ConditionSummary()
        {
            switch (condition)
            {
                case RelicConditionType.Always:
                    return "始终";
                case RelicConditionType.NoCategoryGathered:
                    return $"未采集{CategoryLabel(conditionCategory)}";
                case RelicConditionType.HasFlavorCountAtLeast:
                    return $"风味种类≥{conditionInt}";
                default:
                    return condition.ToString();
            }
        }

        public string EffectSummary()
        {
            switch (effect)
            {
                case RelicEffectType.AddFinalMultiplier:
                    return $"最终倍率+{floatValue:0.##}";
                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                    return $"每种风味最终倍率+{floatValue:0.##}";
                case RelicEffectType.DisableSpicyCap:
                    return "热辣倍率无上限";
                case RelicEffectType.GrantIngredientPerGather:
                {
                    string name = ingredient != null ? ingredient.DisplayName : "？";
                    return $"每采集{intValue}→{name}×{amount}";
                }
                default:
                    return effect.ToString();
            }
        }

        public static string TriggerLabel(RelicTrigger value)
        {
            switch (value)
            {
                case RelicTrigger.AfterGather: return "采集后";
                case RelicTrigger.BeforeSpicy: return "热辣前";
                case RelicTrigger.AfterScore: return "结算后";
                default: return value.ToString();
            }
        }

        public static string CategoryLabel(IngredientCategory category)
        {
            switch (category)
            {
                case IngredientCategory.Vegetable: return "蔬菜";
                case IngredientCategory.Meat: return "肉类";
                case IngredientCategory.Seafood: return "海鲜";
                case IngredientCategory.Spice: return "香料";
                case IngredientCategory.Grain: return "谷物";
                case IngredientCategory.Dairy: return "乳品";
                case IngredientCategory.Fruit: return "水果";
                default: return "其他";
            }
        }
    }
}
