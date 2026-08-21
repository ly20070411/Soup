using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Soup.Events
{
    [Serializable]
    public class EventOption
    {
        [TextArea(1, 3)]
        [SerializeField] private string label = string.Empty;
        [SerializeField] private List<EventEffect> effects = new List<EventEffect>();

        public string Label => label;
        public IReadOnlyList<EventEffect> Effects => effects;

        public void SetLabel(string value) => label = value ?? string.Empty;

        public void SetEffects(List<EventEffect> value) =>
            effects = value ?? new List<EventEffect>();

        public void ClearEffects() => effects = new List<EventEffect>();

        public void AddEffect(EventEffect effect)
        {
            if (effect == null) return;
            if (effects == null)
                effects = new List<EventEffect>();
            effects.Add(effect);
        }

        public string GetEffectsSummary()
        {
            if (effects == null || effects.Count == 0)
                return "无效果";

            var sb = new StringBuilder();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null) continue;
                if (sb.Length > 0) sb.Append("，");
                sb.Append(effects[i].ToSummary());
            }

            return sb.Length > 0 ? sb.ToString() : "无效果";
        }
    }
}
