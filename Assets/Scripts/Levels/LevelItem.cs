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
        [Tooltip("挑战分数（可选）。不影响通关判定，仅作额外目标展示。")]
        [SerializeField, Min(0)] private int challengeScore;
        [Tooltip("终极挑战分数（可选，如第五关）。不影响通关判定，仅作额外目标展示。")]
        [SerializeField, Min(0)] private int ultimateChallengeScore;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int OrderIndex => orderIndex;
        public int TargetScore => targetScore;
        public int MaxTurns => maxTurns;
        public int ChallengeScore => challengeScore;
        public bool HasChallengeScore => challengeScore > 0;
        public int UltimateChallengeScore => ultimateChallengeScore;
        public bool HasUltimateChallengeScore => ultimateChallengeScore > 0;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "新关卡" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetOrderIndex(int value) => orderIndex = Mathf.Max(1, value);

        public void SetVictory(int scoreTarget, int turnsLimit, int challenge = 0, int ultimateChallenge = 0)
        {
            targetScore = Mathf.Max(1, scoreTarget);
            maxTurns = Mathf.Max(1, turnsLimit);
            challengeScore = Mathf.Max(0, challenge);
            ultimateChallengeScore = Mathf.Max(0, ultimateChallenge);
        }

        public void SetChallengeScore(int value) => challengeScore = Mathf.Max(0, value);

        public void SetUltimateChallengeScore(int value) =>
            ultimateChallengeScore = Mathf.Max(0, value);

        private string FormatOptionalScoreExtras(int gained)
        {
            bool hasChallenge = challengeScore > 0;
            bool hasUltimate = ultimateChallengeScore > 0;
            if (!hasChallenge && !hasUltimate)
                return string.Empty;

            string challengePart = string.Empty;
            if (hasChallenge)
            {
                challengePart = gained >= challengeScore
                    ? "挑战达成"
                    : $"挑战 {challengeScore}";
            }

            string ultimatePart = string.Empty;
            if (hasUltimate)
            {
                ultimatePart = gained >= ultimateChallengeScore
                    ? "终极挑战达成"
                    : $"终极挑战 {ultimateChallengeScore}";
            }

            if (hasChallenge && hasUltimate)
                return $"（{challengePart}；{ultimatePart}）";
            if (hasChallenge)
                return $"（{challengePart}）";
            return $"（{ultimatePart}）";
        }

        /// <summary>HUD：通关目标与挑战进度（挑战分不影响通关判定）。</summary>
        public string FormatScoreProgress(int gained)
        {
            string line = $"得分 {Mathf.Max(0, gained)}/{targetScore}";
            line += FormatOptionalScoreExtras(gained);
            return line;
        }

        /// <summary>关卡间结算页副标题。</summary>
        public string FormatSettlementCaption(int score)
        {
            int gained = Mathf.Max(0, score);
            if (targetScore <= 0)
                return $"本关得分\n<size=64>{gained}</size>";

            string caption = $"本关得分\n<size=64>{gained}</size>\n<size=22>目标 {targetScore}</size>";
            if (challengeScore > 0)
            {
                caption += gained >= challengeScore
                    ? $"\n<size=20>挑战 {challengeScore} · 已达成</size>"
                    : $"\n<size=20>挑战 {challengeScore}</size>";
            }

            if (ultimateChallengeScore > 0)
            {
                caption += gained >= ultimateChallengeScore
                    ? $"\n<size=20>终极挑战 {ultimateChallengeScore} · 已达成</size>"
                    : $"\n<size=20>终极挑战 {ultimateChallengeScore}</size>";
            }

            return caption;
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
            challengeScore = Mathf.Max(0, challengeScore);
            ultimateChallengeScore = Mathf.Max(0, ultimateChallengeScore);
        }
#endif
    }
}
