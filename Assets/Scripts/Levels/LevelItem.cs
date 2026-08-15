using UnityEngine;

namespace Soup.Levels
{
    /// <summary>
    /// One campaign level: reach TargetScore within MaxTurns.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "Soup/Levels/Level", order = 0)]
    public class LevelItem : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "New Level";
        [TextArea(2, 5)]
        [SerializeField] private string description = string.Empty;

        [Header("Story & Presentation")]
        [SerializeField] private string chapterId = "chapter_1";
        [TextArea(3, 8)]
        [SerializeField] private string storyIntro = string.Empty;
        [TextArea(3, 8)]
        [SerializeField] private string storyOutro = string.Empty;
        [TextArea(2, 5)]
        [SerializeField] private string secretGoal = string.Empty;
        [SerializeField] private Sprite background;

        [Header("Goals")]
        [Tooltip("本关需要达到的目标分数。")]
        [SerializeField] private int targetScore = 100;
        [Tooltip("本关最大回合数。")]
        [SerializeField] private int maxTurns = 10;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public string ChapterId => chapterId;
        public string StoryIntro => storyIntro;
        public string StoryOutro => storyOutro;
        public string SecretGoal => secretGoal;
        public Sprite Background => background;
        public int TargetScore => Mathf.Max(0, targetScore);
        public int MaxTurns => Mathf.Max(1, maxTurns);

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "New Level" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetPresentation(
            string newChapterId,
            string newStoryIntro,
            string newStoryOutro,
            string newSecretGoal,
            Sprite newBackground = null)
        {
            chapterId = string.IsNullOrWhiteSpace(newChapterId) ? "chapter_1" : newChapterId.Trim();
            storyIntro = newStoryIntro ?? string.Empty;
            storyOutro = newStoryOutro ?? string.Empty;
            secretGoal = newSecretGoal ?? string.Empty;
            background = newBackground;
        }

        public void SetGoals(int newTargetScore, int newMaxTurns)
        {
            targetScore = Mathf.Max(0, newTargetScore);
            maxTurns = Mathf.Max(1, newMaxTurns);
        }

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
