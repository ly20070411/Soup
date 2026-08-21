using System.Collections.Generic;
using System.Text;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Events
{
    /// <summary>
    /// Event definition: identity, category, trigger, and player options.
    /// </summary>
    [CreateAssetMenu(fileName = "Event_", menuName = "Soup/Events/Event", order = 0)]
    public class EventItem : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "New Event";
        [TextArea(3, 8)]
        [SerializeField] private string description = string.Empty;

        [Header("Classification")]
        [SerializeField] private EventCategory category = EventCategory.General;
        [SerializeField] private EventTriggerMoment triggerMoment = EventTriggerMoment.AfterStage;
        [SerializeField] [Min(0f)] private float weight = 1f;
        [SerializeField] private bool canRepeat = true;
        [Tooltip("0 = 任意关；>0 时仅在该关通关后进入抽取池（对应 LevelManager.LevelsClearedCount）。")]
        [SerializeField] [Min(0)] private int requiredStageIndex;
        [Tooltip("互斥组：触发过同组任一事件后，组内其余事件不再出现（如祝福女神）。")]
        [SerializeField] private string exclusionGroup = string.Empty;

        [Header("进阶专属")]
        [Tooltip("进阶专属事件对应的岗位；该岗位进阶至少一次后才有概率出现。")]
        [SerializeField] private JobItem relatedJob;

        [Header("Options")]
        [SerializeField] private List<EventOption> options = new List<EventOption>();

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public EventCategory Category => category;
        public EventTriggerMoment TriggerMoment => triggerMoment;
        public float Weight => weight;
        public bool CanRepeat => canRepeat;
        /// <summary>0 = any stage; otherwise only eligible after clearing that stage index.</summary>
        public int RequiredStageIndex => requiredStageIndex;
        public string ExclusionGroup => exclusionGroup;
        public JobItem RelatedJob => relatedJob;
        public bool IsAdvancedExclusive => category == EventCategory.AdvancedExclusive;
        public IReadOnlyList<EventOption> Options => options;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "New Event" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetCategory(EventCategory value) => category = value;

        public void SetTriggerMoment(EventTriggerMoment value) => triggerMoment = value;

        public void SetWeight(float value) => weight = Mathf.Max(0f, value);

        public void SetCanRepeat(bool value) => canRepeat = value;

        public void SetRequiredStageIndex(int value) => requiredStageIndex = Mathf.Max(0, value);

        public void SetExclusionGroup(string value) =>
            exclusionGroup = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        public void SetRelatedJob(JobItem job) => relatedJob = job;

        public void SetOptions(List<EventOption> value) =>
            options = value ?? new List<EventOption>();

        public void ClearOptions() => options = new List<EventOption>();

        public void AddOption(EventOption option)
        {
            if (option == null) return;
            if (options == null)
                options = new List<EventOption>();
            options.Add(option);
        }

        public static string CategoryLabel(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.General: return "一般事件";
                case EventCategory.AdvancedExclusive: return "进阶专属事件";
                default: return category.ToString();
            }
        }

        public string GetOptionsSummary()
        {
            if (options == null || options.Count == 0)
                return "无选项";

            var sb = new StringBuilder();
            for (int i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                if (opt == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(i + 1).Append(". ").Append(opt.Label);
            }

            return sb.Length > 0 ? sb.ToString() : "无选项";
        }

        public void EnsureDefaultIdFromName()
        {
            if (!string.IsNullOrWhiteSpace(id)) return;
            id = SanitizeId(displayName);
        }

        public static string SanitizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "event_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

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
                ? "event_" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                : result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
            EnsureDefaultIdFromName();
            if (options == null)
                options = new List<EventOption>();
            weight = Mathf.Max(0f, weight);
        }
#endif
    }
}
