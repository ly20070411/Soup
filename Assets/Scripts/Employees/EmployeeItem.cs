using Soup.Jobs;
using UnityEngine;

namespace Soup.Employees
{
    /// <summary>
    /// Employee type definition (小精灵 / 蘑菇人 / 幽灵 / 异世界勇者 / 吱吱).
    /// </summary>
    [CreateAssetMenu(fileName = "Employee_", menuName = "Soup/Employees/Employee", order = 0)]
    public class EmployeeItem : ScriptableObject
    {
        /// <summary>Any-job sentinel for allowedJobType.</summary>
        public const JobType AnyJob = (JobType)(-1);

        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "New Employee";
        [TextArea(2, 5)]
        [SerializeField] private string description = string.Empty;
        [SerializeField] private Sprite icon;

        [Header("Labor")]
        [Tooltip("每个员工贡献的劳力倍率（幽灵 0.8，异世界勇者 3.0）。")]
        [SerializeField] private float laborEfficiency = 1f;

        [Header("Assignment Rules")]
        [Tooltip("是否占用岗位容量（幽灵不占岗）。")]
        [SerializeField] private bool occupiesJobSlot = true;
        [Tooltip("玩家是否可以手动分配（蘑菇人锁定岗位，不可手动分配）。")]
        [SerializeField] private bool canPlayerAssign = true;
        [Tooltip("锁定的岗位 Id（如蘑菇人锁 mushroom 采集岗；空表示不锁定）。")]
        [SerializeField] private string lockedJobId = string.Empty;
        [Tooltip("只能分配到该类型岗位（吱吱只能处理）；-1 = 任意。")]
        [SerializeField] private JobType allowedJobType = AnyJob;

        [Header("Upkeep")]
        [Tooltip("在处理岗上会吃掉该岗位产出处理食材的比例（吱吱 0.1；0 = 不吃）。")]
        [SerializeField, Range(0f, 1f)] private float eatProcessedShare = 0f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public float LaborEfficiency => Mathf.Max(0f, laborEfficiency);
        public bool OccupiesJobSlot => occupiesJobSlot;
        public bool CanPlayerAssign => canPlayerAssign;
        public string LockedJobId => lockedJobId ?? string.Empty;
        public bool HasLockedJob => !string.IsNullOrEmpty(LockedJobId);
        public JobType? AllowedJobType => allowedJobType == AnyJob ? (JobType?)null : allowedJobType;
        public float EatProcessedShare => Mathf.Clamp01(eatProcessedShare);

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "New Employee" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetLaborEfficiency(float value) => laborEfficiency = Mathf.Max(0f, value);

        public void SetAssignmentRules(
            bool occupiesSlot,
            bool playerAssignable,
            string lockedJob,
            JobType? allowedType = null)
        {
            occupiesJobSlot = occupiesSlot;
            canPlayerAssign = playerAssignable;
            lockedJobId = lockedJob ?? string.Empty;
            allowedJobType = allowedType ?? AnyJob;
        }

        public void SetEatProcessedShare(float value) =>
            eatProcessedShare = Mathf.Clamp01(value);

        public void SetIcon(Sprite value) => icon = value;

        public void EnsureDefaultIdFromName()
        {
            if (!string.IsNullOrWhiteSpace(id)) return;
            id = SanitizeId(name);
        }

        public static string SanitizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            var sb = new System.Text.StringBuilder(source.Length);
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
