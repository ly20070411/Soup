using System;
using Soup.Jobs;
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
        [SerializeField, Min(0)] private int conditionIntMax = 1;

        [SerializeField] private RelicEffectType effect = RelicEffectType.AddFinalMultiplier;
        [SerializeField] private float floatValue = 0.6f;
        [SerializeField, Min(0)] private int intValue = 5;
        [SerializeField] private int amount = 1;
        [SerializeField] private IngredientItem ingredient;
        [SerializeField] private IngredientMaterial material = IngredientMaterial.Soft;
        [SerializeField] private string employeeTypeId = string.Empty;
        [SerializeField] private RelicItem linkedRelic;

        public RelicTrigger Trigger => trigger;
        public RelicConditionType Condition => condition;
        public IngredientCategory ConditionCategory => conditionCategory;
        public int ConditionInt => conditionInt;
        public int ConditionIntMax => conditionIntMax;
        public RelicEffectType Effect => effect;
        public float FloatValue => floatValue;
        public int IntValue => intValue;
        public int Amount => amount;
        public IngredientItem Ingredient => ingredient;
        public IngredientMaterial Material => material;
        public string EmployeeTypeId => employeeTypeId;
        public RelicItem LinkedRelic => linkedRelic;

        public void SetTrigger(RelicTrigger value) => trigger = value;

        public void SetCondition(
            RelicConditionType type,
            IngredientCategory category,
            int conditionValue,
            int conditionMax = 0)
        {
            condition = type;
            conditionCategory = category;
            conditionInt = Mathf.Max(0, conditionValue);
            conditionIntMax = Mathf.Max(0, conditionMax);
        }

        public void SetEffect(
            RelicEffectType type,
            float fValue,
            int iValue,
            int grantAmount,
            IngredientItem item,
            IngredientMaterial mat = IngredientMaterial.Soft,
            string employeeId = null,
            RelicItem linked = null)
        {
            effect = type;
            floatValue = fValue;
            intValue = Mathf.Max(0, iValue);
            amount = grantAmount;
            ingredient = item;
            material = mat;
            employeeTypeId = employeeId ?? string.Empty;
            linkedRelic = linked;
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
                case RelicConditionType.HasFlavorCountAtMost:
                    return $"风味种类≤{conditionInt}";
                case RelicConditionType.TurnIndexInRange:
                    return $"本关回合{conditionInt}-{conditionIntMax}";
                default:
                    return condition.ToString();
            }
        }

        public string EffectSummary()
        {
            switch (effect)
            {
                case RelicEffectType.AddFinalMultiplier:
                    return $"最终倍率{(floatValue >= 0f ? "+" : "")}{floatValue:0.##}";
                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                    return $"每种风味最终倍率+{floatValue:0.##}";
                case RelicEffectType.DisableSpicyCap:
                    return "热辣倍率无上限";
                case RelicEffectType.GrantIngredientPerGather:
                {
                    string name = ingredient != null ? ingredient.DisplayName : "？";
                    return $"每采集{intValue}→{name}×{amount}";
                }
                case RelicEffectType.ModifyWarehouseCapacity:
                    return $"仓库容量{(amount >= 0 ? "+" : "")}{amount}";
                case RelicEffectType.AddRawMaterial:
                    return $"{MaterialLabel(material)}+{amount}";
                case RelicEffectType.AddProcessed:
                    return $"已处理+{amount}";
                case RelicEffectType.AddGlobalLaborEfficiency:
                    return $"全局效率{(floatValue >= 0f ? "+" : "")}{floatValue:0.##}";
                case RelicEffectType.AddEmployeeTypeLaborEfficiency:
                {
                    string id = string.IsNullOrEmpty(employeeTypeId) ? "？" : employeeTypeId;
                    return $"{id}效率{(floatValue >= 0f ? "+" : "")}{floatValue:0.##}";
                }
                case RelicEffectType.MultiplyIndependentScore:
                    return $"独立乘区×{floatValue:0.##}";
                case RelicEffectType.ModifyElfCount:
                    return $"小精灵{(amount >= 0 ? "+" : "")}{amount}";
                case RelicEffectType.GrantLinkedRelic:
                {
                    string name = linkedRelic != null ? linkedRelic.DisplayName : "？";
                    return $"获得{name}";
                }
                case RelicEffectType.GrantRawPerRawProduced:
                    return $"每生产{intValue}{MaterialLabel(material)}→+{amount}";
                case RelicEffectType.GrantSoftFromUnusedWarehousePercent:
                    return $"未用仓库×{floatValue:0.##}→柔软";
                case RelicEffectType.ChanceGrantRandomRaw:
                    return $"{floatValue:0.##}概率随机未处理×{amount}";
                case RelicEffectType.GrantEmployeeOnElfLoss:
                {
                    string id = string.IsNullOrEmpty(employeeTypeId) ? "ghost" : employeeTypeId;
                    return $"每损失小精灵→{id}×{amount}";
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
                case RelicTrigger.OnAcquire: return "获得时";
                case RelicTrigger.TurnStart: return "回合开始";
                case RelicTrigger.LevelStart: return "关卡开始";
                case RelicTrigger.AfterProcess: return "处理后";
                case RelicTrigger.Passive: return "被动";
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

        public static string MaterialLabel(IngredientMaterial mat)
        {
            switch (mat)
            {
                case IngredientMaterial.Soft: return "柔软";
                case IngredientMaterial.Tough: return "强韧";
                case IngredientMaterial.Solid: return "坚固";
                default: return mat.ToString();
            }
        }
    }
}
