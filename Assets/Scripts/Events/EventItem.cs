using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Soup.Events
{
    /// <summary>
    /// A random event the chief presents to the player: description plus 2-3 options
    /// whose effects are applied on pick (relics / employees / elves / job modifiers).
    /// </summary>
    [CreateAssetMenu(fileName = "Event_", menuName = "Soup/Events/Event", order = 0)]
    public class EventItem : ScriptableObject
    {
        /// <summary>Effect kinds an option can apply when picked.</summary>
        public enum EffectKind
        {
            GrantRelic = 0,
            GrantEmployee = 1,
            ModifyElves = 2,
            ModifyJobYield = 3,
            ModifyJobCapacity = 4,
            DisableJob = 5,
            ModifyWarehouse = 6,
            ModifyJobFlavor = 7
        }

        [Serializable]
        public class EventEffect
        {
            [SerializeField] private EffectKind kind = EffectKind.GrantRelic;
            [Tooltip("遗物/员工/岗位 Id。ModifyJobFlavor 时为岗位 Id。")]
            [SerializeField] private string targetId = string.Empty;
            [Tooltip("第二目标：ModifyJobFlavor 的风味名（Spicy/Sour/Cold/Magic）。")]
            [SerializeField] private string secondTargetId = string.Empty;
            [Tooltip("数量 / 小精灵增减 / 容量增减 / 每单位风味点数（百分比按整数存，如 30 = +30%）。")]
            [SerializeField] private int intAmount = 1;
            [Tooltip("产量倍率（1.3 = +30%）。")]
            [SerializeField] private float floatAmount = 1f;
            [Tooltip("触发概率（1 = 必定）。")]
            [SerializeField, Range(0f, 1f)] private float chance = 1f;

            public EffectKind Kind => kind;
            public string TargetId => targetId;
            public string SecondTargetId => secondTargetId ?? string.Empty;
            public int IntAmount => intAmount;
            public float FloatAmount => floatAmount;
            public float Chance => Mathf.Clamp01(chance);

            public void Set(
                EffectKind newKind,
                string newTarget,
                int newInt,
                float newFloat,
                float newChance = 1f,
                string newSecondTarget = null)
            {
                kind = newKind;
                targetId = newTarget ?? string.Empty;
                secondTargetId = newSecondTarget ?? string.Empty;
                intAmount = newInt;
                floatAmount = newFloat;
                chance = Mathf.Clamp01(newChance);
            }
        }

        [Serializable]
        public class EventOption
        {
            [TextArea(2, 4)]
            [SerializeField] private string text = "选项";
            [SerializeField] private List<EventEffect> effects = new List<EventEffect>();

            public string Text => text;
            public IReadOnlyList<EventEffect> Effects => effects;

            public void Set(string newText)
            {
                text = newText ?? string.Empty;
            }

            public void SetEffects(IEnumerable<EventEffect> newEffects)
            {
                effects.Clear();
                if (newEffects == null) return;
                foreach (var effect in newEffects)
                {
                    if (effect != null)
                        effects.Add(effect);
                }
            }
        }

        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "New Event";
        [TextArea(3, 6)]
        [SerializeField] private string description = string.Empty;

        [Header("Trigger")]
        [Tooltip("只在第 N 关结束时触发（0 = 任意时机随机/阶段结算）。")]
        [SerializeField] private int triggerLevelIndex = 0;
        [Tooltip("仅当该岗位已解锁时才会出现（进阶专属事件；空 = 任意）。")]
        [SerializeField] private string requiredJobId = string.Empty;

        [Header("Options")]
        [SerializeField] private List<EventOption> options = new List<EventOption>();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int TriggerLevelIndex => Mathf.Max(0, triggerLevelIndex);
        public string RequiredJobId => requiredJobId ?? string.Empty;
        public bool HasRequiredJob => !string.IsNullOrEmpty(RequiredJobId);
        public IReadOnlyList<EventOption> Options => options;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "New Event" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetTrigger(int levelIndex, string requiredJob)
        {
            triggerLevelIndex = Mathf.Max(0, levelIndex);
            requiredJobId = requiredJob ?? string.Empty;
        }

        public void SetOptions(IEnumerable<EventOption> newOptions)
        {
            options.Clear();
            if (newOptions == null) return;
            foreach (var option in newOptions)
            {
                if (option != null)
                    options.Add(option);
            }
        }

        public void EnsureDefaultIdFromName()
        {
            if (!string.IsNullOrWhiteSpace(id)) return;
            id = SanitizeId(name);
        }

        public string GetSummary()
        {
            var sb = new StringBuilder();
            sb.Append(displayName);
            if (!string.IsNullOrEmpty(description))
            {
                sb.Append(" — ");
                sb.Append(description);
            }

            return sb.ToString();
        }

        public static string SanitizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            var sb = new StringBuilder(source.Length);
            bool lastWasUnderscore = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasUnderscore = false;
                }
                else if (!lastWasUnderscore && sb.Length > 0)
                {
                    sb.Append('_');
                    lastWasUnderscore = true;
                }
            }

            string result = sb.ToString();
            return result.EndsWith("_") ? result.Substring(0, result.Length - 1) : result;
        }
    }
}
