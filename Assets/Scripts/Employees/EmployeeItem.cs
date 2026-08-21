using Soup.Jobs;
using UnityEngine;

namespace Soup.Employees
{
    /// <summary>
    /// Employee unit definition: efficiency, slot occupation, optional locked job.
    /// </summary>
    [CreateAssetMenu(fileName = "Employee_", menuName = "Soup/Employees/Employee", order = 0)]
    public class EmployeeItem : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "New Employee";
        [TextArea(2, 5)]
        [SerializeField] private string description = string.Empty;

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color tint = Color.white;

        [Header("Work")]
        [Tooltip("工作效率。1 = 标准，0.8 = 八成产出。")]
        [SerializeField, Min(0f)] private float workEfficiency = 1f;
        [Tooltip("是否占用岗位人口上限。幽灵为 false。")]
        [SerializeField] private bool occupiesJobSlot = true;
        [Tooltip("玩家是否可手动分配/撤下岗位。蘑菇人为 false。")]
        [SerializeField] private bool canPlayerAssign = true;
        [Tooltip("锁定岗位（可选）。有值时始终在该岗位工作。")]
        [SerializeField] private JobItem lockedJob;
        [Tooltip("若开启，只能分配到指定类型岗位（如吱吱仅处理）。")]
        [SerializeField] private bool restrictToJobType;
        [SerializeField] private JobType allowedJobType = JobType.Process;
        [Tooltip("吃掉自身产出处理食材的比例。0 = 不吃。吱吱为 0.1。")]
        [SerializeField, Range(0f, 1f)] private float consumeOwnProcessedFraction;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Tint => tint;
        public float WorkEfficiency => workEfficiency;
        public bool OccupiesJobSlot => occupiesJobSlot;
        public bool CanPlayerAssign => canPlayerAssign;
        public JobItem LockedJob => lockedJob;
        public bool HasLockedJob => lockedJob != null;
        public bool RestrictToJobType => restrictToJobType;
        public JobType AllowedJobType => allowedJobType;
        public float ConsumeOwnProcessedFraction => Mathf.Clamp01(consumeOwnProcessedFraction);
        public bool IsLockedTo(JobItem job) =>
            lockedJob != null && job != null && ReferenceEquals(lockedJob, job);

        public bool CanWorkJob(JobItem job)
        {
            if (job == null) return false;
            if (HasLockedJob)
                return IsLockedTo(job);
            if (restrictToJobType && job.JobType != allowedJobType)
                return false;
            return true;
        }

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "New Employee" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetIcon(Sprite value) => icon = value;

        public void SetTint(Color value) => tint = value;

        public void SetWorkEfficiency(float value) => workEfficiency = Mathf.Max(0f, value);

        public void SetOccupiesJobSlot(bool value) => occupiesJobSlot = value;

        public void SetCanPlayerAssign(bool value) => canPlayerAssign = value;

        public void SetLockedJob(JobItem job) => lockedJob = job;

        public void SetRestrictToJobType(bool restrict, JobType type = JobType.Process)
        {
            restrictToJobType = restrict;
            allowedJobType = type;
        }

        public void SetConsumeOwnProcessedFraction(float value) =>
            consumeOwnProcessedFraction = Mathf.Clamp01(value);

        /// <summary>员工规则摘要，供事件选项悬停提示等使用。</summary>
        public string GetEffectSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"效率 ×{workEfficiency:0.##}");
            if (!occupiesJobSlot)
                sb.Append("\n不占用岗位人口");
            if (!canPlayerAssign)
                sb.Append("\n不可手动调岗");
            if (lockedJob != null)
                sb.Append("\n锁定岗位：").Append(lockedJob.DisplayName);
            else if (restrictToJobType)
                sb.Append("\n仅限").Append(JobTypeLabel(allowedJobType)).Append("岗");
            if (consumeOwnProcessedFraction > 0.001f)
                sb.Append("\n吞食自身处理产出 ").Append((consumeOwnProcessedFraction * 100f).ToString("0.#")).Append('%');
            if (!string.IsNullOrWhiteSpace(description))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(description.Trim());
            }

            return sb.Length > 0 ? sb.ToString() : DisplayName;
        }

        private static string JobTypeLabel(JobType type)
        {
            switch (type)
            {
                case JobType.Gather: return "采集";
                case JobType.Process: return "处理";
                case JobType.Cook: return "烹饪";
                default: return type.ToString();
            }
        }

        public void EnsureDefaultIdFromName()
        {
            if (!string.IsNullOrWhiteSpace(id)) return;
            id = SanitizeId(displayName);
        }

        public static string SanitizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "employee_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            var chars = source.Trim().ToLowerInvariant().ToCharArray();
            var builder = new System.Text.StringBuilder(chars.Length);
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
                ? "employee_" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                : result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
            EnsureDefaultIdFromName();
            workEfficiency = Mathf.Max(0f, workEfficiency);
            consumeOwnProcessedFraction = Mathf.Clamp01(consumeOwnProcessedFraction);
        }
#endif
    }
}
