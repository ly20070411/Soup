using System.Collections.Generic;
using UnityEngine;

namespace Soup.Levels
{
    /// <summary>
    /// 主菜单「关卡调节」覆盖值，经 PlayerPrefs 持久化并写回 <see cref="LevelItem"/>。
    /// </summary>
    public static class LevelTuningStore
    {
        private const string Prefix = "soup_level_tune_";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // PlayerPrefs 跨域保留；无需清静态。
        }

        public static void ApplySavedToDatabase(LevelDatabase database)
        {
            if (database == null) return;
            database.RebuildIndex();
            var ordered = database.GetOrdered();
            for (int i = 0; i < ordered.Count; i++)
            {
                var level = ordered[i];
                if (level == null || string.IsNullOrEmpty(level.Id)) continue;
                if (TryLoad(level.Id, out int score, out int turns))
                    level.SetVictory(score, turns);
            }
        }

        public static bool TryLoad(string levelId, out int targetScore, out int maxTurns)
        {
            targetScore = 0;
            maxTurns = 0;
            if (string.IsNullOrEmpty(levelId)) return false;
            string scoreKey = ScoreKey(levelId);
            string turnsKey = TurnsKey(levelId);
            if (!PlayerPrefs.HasKey(scoreKey) || !PlayerPrefs.HasKey(turnsKey))
                return false;

            targetScore = Mathf.Max(1, PlayerPrefs.GetInt(scoreKey, 1));
            maxTurns = Mathf.Max(1, PlayerPrefs.GetInt(turnsKey, 1));
            return true;
        }

        public static void SaveLevel(LevelItem level)
        {
            if (level == null || string.IsNullOrEmpty(level.Id)) return;
            PlayerPrefs.SetInt(ScoreKey(level.Id), Mathf.Max(1, level.TargetScore));
            PlayerPrefs.SetInt(TurnsKey(level.Id), Mathf.Max(1, level.MaxTurns));
            PlayerPrefs.Save();
        }

        public static void SaveAll(IReadOnlyList<LevelItem> levels)
        {
            if (levels == null) return;
            for (int i = 0; i < levels.Count; i++)
                SaveLevel(levels[i]);
        }

        private static string ScoreKey(string id) => Prefix + id + "_score";
        private static string TurnsKey(string id) => Prefix + id + "_turns";
    }
}
