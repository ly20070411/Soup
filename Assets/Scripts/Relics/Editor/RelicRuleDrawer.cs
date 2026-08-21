using Soup.Items;
using Soup.Jobs;
using UnityEditor;
using UnityEngine;

namespace Soup.Relics.Editor
{
    [CustomPropertyDrawer(typeof(RelicRule))]
    public class RelicRuleDrawer : PropertyDrawer
    {
        private const float Line = 18f;
        private const float Pad = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return Line;

            int lines = 1; // foldout
            lines += 2; // trigger + condition
            lines += ConditionExtraLines(property);
            lines += 1; // effect
            lines += EffectExtraLines(property);
            lines += 1; // summary help
            return lines * (Line + Pad) + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var foldRect = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(foldRect, property.isExpanded, BuildHeader(property), true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float y = position.y + Line + Pad;
            float w = position.width;

            var trigger = property.FindPropertyRelative("trigger");
            var condition = property.FindPropertyRelative("condition");
            var conditionCategory = property.FindPropertyRelative("conditionCategory");
            var conditionInt = property.FindPropertyRelative("conditionInt");
            var conditionIntMax = property.FindPropertyRelative("conditionIntMax");
            var effect = property.FindPropertyRelative("effect");
            var floatValue = property.FindPropertyRelative("floatValue");
            var intValue = property.FindPropertyRelative("intValue");
            var amount = property.FindPropertyRelative("amount");
            var ingredient = property.FindPropertyRelative("ingredient");
            var material = property.FindPropertyRelative("material");
            var employeeTypeId = property.FindPropertyRelative("employeeTypeId");
            var linkedRelic = property.FindPropertyRelative("linkedRelic");

            y = DrawProp(position.x, y, w, trigger, "触发时机");
            y = DrawProp(position.x, y, w, condition, "条件");

            var condType = (RelicConditionType)condition.intValue;
            if (condType == RelicConditionType.NoCategoryGathered)
                y = DrawProp(position.x, y, w, conditionCategory, "食材分类");
            else if (condType == RelicConditionType.HasFlavorCountAtLeast)
                y = DrawProp(position.x, y, w, conditionInt, "风味种类下限");
            else if (condType == RelicConditionType.HasFlavorCountAtMost)
                y = DrawProp(position.x, y, w, conditionInt, "风味种类上限");
            else if (condType == RelicConditionType.TurnIndexInRange)
            {
                y = DrawProp(position.x, y, w, conditionInt, "回合下限");
                y = DrawProp(position.x, y, w, conditionIntMax, "回合上限");
            }

            y = DrawProp(position.x, y, w, effect, "效果");

            var effectType = (RelicEffectType)effect.intValue;
            switch (effectType)
            {
                case RelicEffectType.AddFinalMultiplier:
                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                case RelicEffectType.AddGlobalLaborEfficiency:
                case RelicEffectType.MultiplyIndependentScore:
                case RelicEffectType.AddSpicyScoreMultiplier:
                    y = DrawProp(position.x, y, w, floatValue, "数值");
                    break;
                case RelicEffectType.AddEmployeeTypeLaborEfficiency:
                    y = DrawProp(position.x, y, w, floatValue, "效率增量");
                    y = DrawProp(position.x, y, w, employeeTypeId, "员工类型Id");
                    break;
                case RelicEffectType.GrantIngredientPerGather:
                    y = DrawProp(position.x, y, w, intValue, "每采集份数");
                    y = DrawProp(position.x, y, w, amount, "产出份数");
                    y = DrawProp(position.x, y, w, ingredient, "产出食材");
                    break;
                case RelicEffectType.ModifyWarehouseCapacity:
                case RelicEffectType.AddProcessed:
                case RelicEffectType.ModifyElfCount:
                    y = DrawProp(position.x, y, w, amount, "数量");
                    break;
                case RelicEffectType.AddRawMaterial:
                    y = DrawProp(position.x, y, w, material, "材质");
                    y = DrawProp(position.x, y, w, amount, "数量");
                    break;
                case RelicEffectType.GrantLinkedRelic:
                    y = DrawProp(position.x, y, w, linkedRelic, "授予遗物");
                    break;
                case RelicEffectType.GrantRawPerRawProduced:
                    y = DrawProp(position.x, y, w, material, "材质");
                    y = DrawProp(position.x, y, w, intValue, "每生产份数");
                    y = DrawProp(position.x, y, w, amount, "额外产出");
                    break;
                case RelicEffectType.GrantRawPerGather:
                    y = DrawProp(position.x, y, w, material, "材质");
                    y = DrawProp(position.x, y, w, intValue, "每采集份数");
                    y = DrawProp(position.x, y, w, amount, "产出份数");
                    break;
                case RelicEffectType.GrantSoftFromUnusedWarehousePercent:
                    y = DrawProp(position.x, y, w, floatValue, "仓库空位比例");
                    break;
                case RelicEffectType.ChanceGrantRandomRaw:
                    y = DrawProp(position.x, y, w, floatValue, "概率");
                    y = DrawProp(position.x, y, w, amount, "数量");
                    break;
                case RelicEffectType.GrantEmployeeOnElfLoss:
                    y = DrawProp(position.x, y, w, amount, "每损失授予数量");
                    y = DrawProp(position.x, y, w, employeeTypeId, "员工类型Id");
                    break;
                case RelicEffectType.AddGatherAmountPerWorker:
                    y = DrawProp(position.x, y, w, amount, "产出份数加成");
                    break;
                case RelicEffectType.ReduceWarehouseWaste:
                    y = DrawProp(position.x, y, w, floatValue, "浪费减少比例");
                    break;
            }

            var summaryRect = new Rect(position.x, y, w, Line);
            EditorGUI.HelpBox(summaryRect, BuildHeader(property), MessageType.None);

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static float DrawProp(float x, float y, float width, SerializedProperty prop, string label)
        {
            var rect = new Rect(x, y, width, Line);
            EditorGUI.PropertyField(rect, prop, new GUIContent(label));
            return y + Line + Pad;
        }

        private static int ConditionExtraLines(SerializedProperty property)
        {
            var condition = property.FindPropertyRelative("condition");
            var condType = (RelicConditionType)condition.intValue;
            switch (condType)
            {
                case RelicConditionType.NoCategoryGathered:
                case RelicConditionType.HasFlavorCountAtLeast:
                case RelicConditionType.HasFlavorCountAtMost:
                    return 1;
                case RelicConditionType.TurnIndexInRange:
                    return 2;
                default:
                    return 0;
            }
        }

        private static int EffectExtraLines(SerializedProperty property)
        {
            var effect = property.FindPropertyRelative("effect");
            var effectType = (RelicEffectType)effect.intValue;
            switch (effectType)
            {
                case RelicEffectType.AddFinalMultiplier:
                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                case RelicEffectType.AddGlobalLaborEfficiency:
                case RelicEffectType.MultiplyIndependentScore:
                case RelicEffectType.AddSpicyScoreMultiplier:
                case RelicEffectType.ModifyWarehouseCapacity:
                case RelicEffectType.AddProcessed:
                case RelicEffectType.ModifyElfCount:
                case RelicEffectType.GrantLinkedRelic:
                case RelicEffectType.GrantSoftFromUnusedWarehousePercent:
                case RelicEffectType.AddGatherAmountPerWorker:
                case RelicEffectType.ReduceWarehouseWaste:
                    return 1;
                case RelicEffectType.AddEmployeeTypeLaborEfficiency:
                case RelicEffectType.AddRawMaterial:
                case RelicEffectType.ChanceGrantRandomRaw:
                case RelicEffectType.GrantEmployeeOnElfLoss:
                    return 2;
                case RelicEffectType.GrantIngredientPerGather:
                case RelicEffectType.GrantRawPerRawProduced:
                case RelicEffectType.GrantRawPerGather:
                    return 3;
                default:
                    return 0;
            }
        }

        private static string BuildHeader(SerializedProperty property)
        {
            // Best-effort header; full summaries live on RelicRule.ToSummary at runtime.
            var trigger = (RelicTrigger)property.FindPropertyRelative("trigger").intValue;
            var effect = (RelicEffectType)property.FindPropertyRelative("effect").intValue;
            return $"{RelicRule.TriggerLabel(trigger)} → {effect}";
        }
    }
}
