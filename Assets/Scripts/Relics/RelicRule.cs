using System;
using Soup.Employees;
using Soup.Game;
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

        public string ToSummary(int stacks = 1)
        {
            string cond = ConditionSummary();
            string eff = EffectSummary(stacks);
            return $"{TriggerLabel(trigger)} | {cond} → {eff}";
        }

        public string ConditionSummary()
        {
            switch (condition)
            {
                case RelicConditionType.Always:
                    return "始终";
                case RelicConditionType.NoCategoryGathered:
                    return $"无{CategoryLabel(conditionCategory)}采集岗";
                case RelicConditionType.HasFlavorCountAtLeast:
                    return $"风味种类≥{conditionInt}";
                case RelicConditionType.HasFlavorCountAtMost:
                    return $"风味种类≤{conditionInt}";
                case RelicConditionType.TurnIndexInRange:
                    return $"本关回合{conditionInt}-{conditionIntMax}";
                case RelicConditionType.WarehouseSpaceBelowHalf:
                    return "空闲仓库<总量一半";
                case RelicConditionType.LastNLevelTurns:
                    return $"本关末{conditionInt}回合";
                case RelicConditionType.AllEmployeesOnCook:
                    return "全部员工在烹饪岗";
                default:
                    return condition.ToString();
            }
        }

        /// <summary>
        /// Player-facing effect text. <paramref name="stacks"/> scales additive/multiplicative totals
        /// to match how multiple owned copies are applied in <c>RelicEffectRunner</c>.
        /// </summary>
        public string EffectSummary(int stacks = 1)
        {
            stacks = Mathf.Max(1, stacks);
            float fAdd = floatValue * stacks;
            int iAdd = intValue; // thresholds usually don't stack
            int amt = amount * stacks;

            switch (effect)
            {
                case RelicEffectType.AddFinalMultiplier:
                    return $"最终倍率{(fAdd >= 0f ? "+" : "")}{fAdd:0.##}";
                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                    return $"每种风味最终倍率+{fAdd:0.##}";
                case RelicEffectType.DisableSpicyCap:
                    return "热辣倍率无上限";
                case RelicEffectType.AddSpicyScoreMultiplier:
                    return $"热辣提供的倍数变为{1f + fAdd:0.##}倍";
                case RelicEffectType.GrantIngredientPerGather:
                {
                    string name = ingredient != null ? ingredient.DisplayName : "？";
                    return $"每采集{intValue}→{name}×{amt}";
                }
                case RelicEffectType.ModifyWarehouseCapacity:
                    return $"仓库容量{(amt >= 0 ? "+" : "")}{amt}";
                case RelicEffectType.AddRawMaterial:
                    return $"{MaterialLabel(material)}+{amt}";
                case RelicEffectType.AddProcessed:
                    return $"已处理+{amt}";
                case RelicEffectType.AddGlobalLaborEfficiency:
                    return $"全局效率{(fAdd >= 0f ? "+" : "")}{fAdd:0.##}";
                case RelicEffectType.AddProcessLaborEfficiency:
                    return $"处理岗效率{(fAdd >= 0f ? "+" : "")}{(fAdd * 100f):0.##}%";
                case RelicEffectType.AddCookLaborEfficiency:
                    return $"烹饪岗效率{(fAdd >= 0f ? "+" : "")}{(fAdd * 100f):0.##}%";
                case RelicEffectType.AddCookOutputWasteFraction:
                    return $"烹饪产出浪费{(Mathf.Clamp01(fAdd) * 100f):0.##}%";
                case RelicEffectType.AddEmployeeTypeLaborEfficiency:
                {
                    string id = string.IsNullOrEmpty(employeeTypeId) ? "？" : employeeTypeId;
                    return $"{id}效率{(fAdd >= 0f ? "+" : "")}{fAdd:0.##}";
                }
                case RelicEffectType.MultiplyIndependentScore:
                {
                    float mult = stacks == 1 ? floatValue : Mathf.Pow(floatValue, stacks);
                    return $"独立乘区×{mult:0.##}";
                }
                case RelicEffectType.ModifyElfCount:
                    return $"小精灵{(amt >= 0 ? "+" : "")}{amt}";
                case RelicEffectType.GrantEmployee:
                {
                    string id = string.IsNullOrEmpty(employeeTypeId) ? "幽灵" : employeeTypeId;
                    if (id == EmployeeManager.GhostId) id = "幽灵";
                    return $"获得{id}×{amt}";
                }
                case RelicEffectType.ConvertAllGatherToHappyTuotuo:
                    return "所有采集岗变为快乐坨坨（进阶保留）";
                case RelicEffectType.AddAdvanceChargesAllZones:
                {
                    int n = amount != 0 ? Mathf.Abs(amount) : 1;
                    n *= stacks;
                    return $"采集/处理/烹饪进阶各+{n}";
                }
                case RelicEffectType.ConvertToughSolidFractionToSoft:
                    return $"回合结束强韧/坚固×{(fAdd > 0f ? fAdd : 0.75f):0.##}→柔软";
                case RelicEffectType.ConvertElvesToGhosts:
                {
                    int cost = iAdd > 0 ? iAdd : 3;
                    int gain = amt > 0 ? amt : 4;
                    return $"关卡结束{cost}小精灵→{gain}幽灵";
                }
                case RelicEffectType.AddAllJobMaxWorkers:
                    return $"所有岗位人口上限+{amt}";
                case RelicEffectType.PresentBonusStageEvents:
                {
                    int n = amt > 0 ? amt : 3;
                    return $"立刻获得{n}个事件";
                }
                case RelicEffectType.GrantLinkedRelic:
                {
                    string name = linkedRelic != null ? linkedRelic.DisplayName : "？";
                    return stacks > 1 ? $"获得{name}×{stacks}" : $"获得{name}";
                }
                case RelicEffectType.GrantRawPerRawProduced:
                    return $"每生产{intValue}{MaterialLabel(material)}→+{amt}";
                case RelicEffectType.GrantRawPerGather:
                    return $"每采集{intValue}→{MaterialLabel(material)}+{amt}";
                case RelicEffectType.AddGatherAmountPerWorker:
                    return $"每种采集物产出份数+{amt}";
                case RelicEffectType.ReduceWarehouseWaste:
                    return $"浪费减少{(Mathf.Min(1f, fAdd) * 100f):0.##}%";
                case RelicEffectType.ConvertWasteToEqualGain:
                {
                    int mult = amount > 0 ? amount : 1;
                    mult *= stacks;
                    return mult > 1 ? $"浪费变为{mult}倍增加" : "浪费变为等量增加";
                }
                case RelicEffectType.AddColdScorePerUnit:
                    return $"寒冷每份已烹饪+{amt}分";
                case RelicEffectType.ReduceMagicConsumePercent:
                    return $"鲜美消耗减少{(Mathf.Min(1f, fAdd) * 100f):0.##}%（等效每回合消耗{Mathf.Max(0f, (FlavorResolver.MagicConsumeBaseRate - fAdd) * 100f):0.##}%）";
                case RelicEffectType.OverrideSourTopTierPercent:
                    return $"酸涩最高档阈值{iAdd}%";
                case RelicEffectType.OverrideSourSecondTierPercent:
                    return $"酸涩第二档阈值{iAdd}%";
                case RelicEffectType.GrantSoftFromUnusedWarehousePercent:
                    return $"未用仓库×{fAdd:0.##}→柔软";
                case RelicEffectType.ChanceGrantRandomRaw:
                    return $"{floatValue:0.##}概率随机未处理×{amt}";
                case RelicEffectType.GrantEmployeeOnElfLoss:
                {
                    string id = string.IsNullOrEmpty(employeeTypeId) ? "ghost" : employeeTypeId;
                    return $"每损失小精灵→{id}×{amt}";
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
                case RelicTrigger.TurnEnd: return "回合结束";
                case RelicTrigger.LevelEnd: return "关卡结束";
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
