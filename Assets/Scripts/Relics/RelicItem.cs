using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Soup.Relics
{
    /// <summary>
    /// Relic definition: identity, acquire stage, and cause→effect rules.
    /// </summary>
    [CreateAssetMenu(fileName = "Relic_", menuName = "Soup/Relics/Relic", order = 0)]
    public class RelicItem : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "New Relic";
        [TextArea(2, 5)]
        [SerializeField] private string description = string.Empty;

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color tint = Color.white;

        [Header("Progression")]
        [SerializeField] private RelicAcquireStage acquireStage = RelicAcquireStage.Stage1;

        [Header("Rules")]
        [SerializeField] private List<RelicRule> rules = new List<RelicRule>();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Tint => tint;
        public RelicAcquireStage AcquireStage => acquireStage;
        public IReadOnlyList<RelicRule> Rules => rules;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "New Relic" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetIcon(Sprite value) => icon = value;

        public void SetTint(Color value) => tint = value;

        public void SetAcquireStage(RelicAcquireStage value) => acquireStage = value;

        public void SetRules(List<RelicRule> value)
        {
            rules = value ?? new List<RelicRule>();
        }

        public void ClearRules() => rules = new List<RelicRule>();

        public void AddRule(RelicRule rule)
        {
            if (rule == null) return;
            if (rules == null)
                rules = new List<RelicRule>();
            rules.Add(rule);
        }

        public string GetRulesSummary()
        {
            if (rules == null || rules.Count == 0)
                return "无规则";

            var sb = new StringBuilder();
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(rules[i].ToSummary());
            }

            return sb.Length > 0 ? sb.ToString() : "无规则";
        }

        public void EnsureDefaultIdFromName()
        {
            if (!string.IsNullOrWhiteSpace(id)) return;
            id = SanitizeId(displayName);
        }

        public static string SanitizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "relic_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            var chars = source.Trim().ToLowerInvariant().ToCharArray();
            var builder = new StringBuilder(chars.Length);
            bool lastWasSeparator = false;
            foreach (char c in chars)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            var result = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(result)
                ? "relic_" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                : result;
        }

        public static string StageLabel(RelicAcquireStage stage)
        {
            switch (stage)
            {
                case RelicAcquireStage.Stage1: return "阶段1";
                case RelicAcquireStage.Stage2: return "阶段2";
                case RelicAcquireStage.Stage3: return "阶段3";
                case RelicAcquireStage.Stage4: return "阶段4";
                default: return stage.ToString();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
            EnsureDefaultIdFromName();
            if (rules == null)
                rules = new List<RelicRule>();
        }
#endif
    }
}
