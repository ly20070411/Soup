using UnityEditor;
using UnityEngine;

namespace Soup.Levels.Editor
{
    /// <summary>
    /// Seeds sample campaign levels (victory targets per level).
    /// </summary>
    public static class LevelDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/LevelDatabase.asset";
        private const string LevelFolder = "Assets/Data/Levels";

        [MenuItem("Soup/Level Manager/Seed Sample Levels")]
        public static void SeedSamplesMenu()
        {
            SeedSamples(openWindow: true);
        }

        public static LevelDatabase SeedSamples(bool openWindow = false)
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(LevelFolder);

            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            CreateOrUpdate(
                db,
                "stage_1",
                "第一关",
                "10 回合内达到 5000 分即可通关。",
                orderIndex: 1,
                targetScore: 5000,
                maxTurns: 10,
                challengeScore: 6000);

            CreateOrUpdate(
                db,
                "stage_2",
                "第二关",
                "10 回合内达到 10000 分即可通关。",
                orderIndex: 2,
                targetScore: 10000,
                maxTurns: 10,
                challengeScore: 12000);

            CreateOrUpdate(
                db,
                "stage_3",
                "第三关",
                "10 回合内达到 17000 分即可通关。回合用尽时酸涩会结算并计入达标判定。",
                orderIndex: 3,
                targetScore: 17000,
                maxTurns: 10,
                challengeScore: 20000);

            CreateOrUpdate(
                db,
                "stage_4",
                "第四关",
                "10 回合内达到 30000 分即可通关。",
                orderIndex: 4,
                targetScore: 30000,
                maxTurns: 10,
                challengeScore: 35000);

            CreateOrUpdate(
                db,
                "stage_5",
                "第五关",
                "10 回合内达到 42000 分即可通关。通关后宣布游戏胜利。",
                orderIndex: 5,
                targetScore: 42000,
                maxTurns: 10,
                challengeScore: 50000,
                ultimateChallengeScore: 60000);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openWindow)
                LevelManagerWindow.Open();

            Debug.Log("[关卡管理器] 示例关卡已填充，当前数量: " + db.Count);
            return db;
        }

        private static void CreateOrUpdate(
            LevelDatabase db,
            string id,
            string displayName,
            string description,
            int orderIndex,
            int targetScore,
            int maxTurns,
            int challengeScore = 0,
            int ultimateChallengeScore = 0)
        {
            string path = $"{LevelFolder}/Level_{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<LevelItem>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<LevelItem>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.SetIdentity(id, displayName);
            item.SetDescription(description);
            item.SetOrderIndex(orderIndex);
            item.SetVictory(targetScore, maxTurns, challengeScore, ultimateChallengeScore);
            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
