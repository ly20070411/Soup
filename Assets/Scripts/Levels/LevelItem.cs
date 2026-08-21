using UnityEngine;

namespace Soup.Levels
{
    /// <summary>
    /// One campaign level (关卡): reach <see cref="TargetScore"/> within <see cref="MaxTurns"/>.
    /// Score is measured relative to the score when the level started.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "Soup/Levels/Level", order = 0)]
    public class LevelItem : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "新关卡";
        [TextArea(2, 5)]
        [SerializeField] private string description = string.Empty;

        [Header("Order")]
        [Tooltip("关卡顺序（越小越靠前）。建议与关卡序号一致：1、2、3…")]
        [SerializeField, Min(1)] private int orderIndex = 1;

        [Header("Victory")]
        [Tooltip("本关需要达到的分数（相对本关开始时的总分增量，含回合用尽后酸涩结算分）。")]
        [SerializeField, Min(1)] private int targetScore = 50;
        [Tooltip("本关允许的最大回合数。回合用尽后先结算酸涩，再判定是否达标。")]
        [SerializeField, Min(1)] private int maxTurns = 10;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int OrderIndex => orderIndex;
        public int TargetScore => targetScore;
        public int MaxTurns => maxTurns;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "新关卡" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetOrderIndex(int value) => orderIndex = Mathf.Max(1, value);

        public void SetVictory(int scoreTarget, int turnsLimit)
        {
            targetScore = Mathf.Max(1, scoreTarget);
            maxTurns = Mathf.Max(1, turnsLimit);
        }

        public void EnsureDefaultIdFromName()
        {
            if (!string.IsNullOrWhiteSpace(id)) return;
            id = SanitizeId(displayName);
        }

        public static string SanitizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "level_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

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
                ? "level_" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                : result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;
            EnsureDefaultIdFromName();
            orderIndex = Mathf.Max(1, orderIndex);
            targetScore = Mathf.Max(1, targetScore);
            maxTurns = Mathf.Max(1, maxTurns);
        }
#endif
    }
}
