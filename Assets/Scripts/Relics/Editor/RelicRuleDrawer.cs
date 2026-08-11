using Soup.Items;
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
            var effect = property.FindPropertyRelative("effect");
            var floatValue = property.FindPropertyRelative("floatValue");
            var intValue = property.FindPropertyRelative("intValue");
            var amount = property.FindPropertyRelative("amount");
            var ingredient = property.FindPropertyRelative("ingredient");

            y = DrawProp(position.x, y, w, trigger, "触发时机");
            y = DrawProp(position.x, y, w, condition, "条件");

            var condType = (RelicConditionType)condition.intValue;
            if (condType == RelicConditionType.NoCategoryGathered)
                y = DrawProp(position.x, y, w, conditionCategory, "食材分类");
            else if (condType == RelicConditionType.HasFlavorCountAtLeast)
                y = DrawProp(position.x, y, w, conditionInt, "风味种类下限");

            y = DrawProp(position.x, y, w, effect, "效果");

            var effectType = (RelicEffectType)effect.intValue;
            switch (effectType)
            {
                case RelicEffectType.AddFinalMultiplier:
                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                    y = DrawProp(position.x, y, w, floatValue, "倍率增量");
                    break;
                case RelicEffectType.GrantIngredientPerGather:
                    y = DrawProp(position.x, y, w, intValue, "每采集份数");
                    y = DrawProp(position.x, y, w, amount, "产出份数");
                    y = DrawProp(position.x, y, w, ingredient, "产出食材");
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
                    return 1;
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
                    return 1;
                case RelicEffectType.GrantIngredientPerGather:
                    return 3;
                default:
                    return 0;
            }
        }

        private static string BuildHeader(SerializedProperty property)
        {
            var trigger = (RelicTrigger)property.FindPropertyRelative("trigger").intValue;
            var condition = (RelicConditionType)property.FindPropertyRelative("condition").intValue;
            var effect = (RelicEffectType)property.FindPropertyRelative("effect").intValue;
            var category = (IngredientCategory)property.FindPropertyRelative("conditionCategory").intValue;
            int conditionInt = property.FindPropertyRelative("conditionInt").intValue;
            float floatValue = property.FindPropertyRelative("floatValue").floatValue;
            int intValue = property.FindPropertyRelative("intValue").intValue;
            int amount = property.FindPropertyRelative("amount").intValue;
            var ingredientProp = property.FindPropertyRelative("ingredient");
            var ingredient = ingredientProp.objectReferenceValue as IngredientItem;

            string cond;
            switch (condition)
            {
                case RelicConditionType.Always:
                    cond = "始终";
                    break;
                case RelicConditionType.NoCategoryGathered:
                    cond = $"未采集{RelicRule.CategoryLabel(category)}";
                    break;
                case RelicConditionType.HasFlavorCountAtLeast:
                    cond = $"风味种类≥{conditionInt}";
                    break;
                default:
                    cond = condition.ToString();
                    break;
            }

            string eff;
            switch (effect)
            {
                case RelicEffectType.AddFinalMultiplier:
                    eff = $"最终倍率+{floatValue:0.##}";
                    break;
                case RelicEffectType.AddFinalMultiplierPerPresentFlavor:
                    eff = $"每种风味最终倍率+{floatValue:0.##}";
                    break;
                case RelicEffectType.DisableSpicyCap:
                    eff = "热辣倍率无上限";
                    break;
                case RelicEffectType.GrantIngredientPerGather:
                {
                    string name = ingredient != null ? ingredient.DisplayName : "？";
                    eff = $"每采集{intValue}→{name}×{amount}";
                    break;
                }
                default:
                    eff = effect.ToString();
                    break;
            }

            return $"{RelicRule.TriggerLabel(trigger)} | {cond} → {eff}";
        }
    }
}
