using UnityEditor;
using UnityEngine;

namespace Soup.Levels.Editor
{
    /// <summary>
    /// Seeds the campaign levels (档期：一关基础 DEMO → 3 关递进示例).
    /// 每关通关条件：在回合上限内累计达到目标分。
    /// </summary>
    public static class LevelDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/LevelDatabase.asset";
        private const string LevelFolder = "Assets/Data/Levels";

        [MenuItem("Soup/Level Manager/Seed Design Levels")]
        public static void SeedAllMenu()
        {
            SeedAll();
            Debug.Log("[LevelDataSeeder] 三关 DEMO 数据已生成。");
        }

        [MenuItem("Soup/Level Manager/Seed Sample Levels")]
        private static void SeedLegacyMenu() => SeedAllMenu();

        public static LevelDatabase SeedAll()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(LevelFolder);

            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            CreateOrUpdate(
                db,
                "level_1",
                "第一关 · 巨人的早餐",
                "基础三段流水线：先采集，再处理，最后烹饪。",
                300,
                8,
                "chapter_1",
                "饥肠王敲响了早餐餐铃。族长把锅铲塞进你手里：在第三次催单前，先让这口旧锅重新冒起香气。",
                "巨人喝光了早餐汤，却仍然喊饿。采集队在回程路上发现了一座不该出现在厨房地下的神秘墓穴。",
                "秘味：本关没有发生仓库溢出。",
                "Assets/Art/Generated/Environments/environment_kitchen_main.png");
            CreateOrUpdate(
                db,
                "level_2",
                "第二关 · 午后的宴席",
                "三类原料与处理专精：别让仓库和处理台成为瓶颈。",
                800,
                10,
                "chapter_1",
                "巨人城的客人已经入席。无味的空盘子越堆越高，这一次只靠蘑菇和小火已经不够了。",
                "一只小精灵意外落锅后，客人短暂感到了饱腹。族长第一次怀疑：鲜美也许来自生命之间的联系。",
                "秘味：同一关内有效使用至少两种处理岗位。",
                "Assets/Art/Generated/Environments/environment_royal_palace.png");
            CreateOrUpdate(
                db,
                "level_3",
                "第三关 · 巨人的晚宴",
                "四种风味与火力选择：即时爆发和关底酸涩需要同时规划。",
                1500,
                12,
                "chapter_1",
                "正式晚宴开始。请用风味证明这锅汤仍能对抗吞掉饱腹感的灰雾。",
                "汤雾短暂驱散了饥雾。饥肠王揭开城堡地板，世界之釜的灰色裂缝贯穿城下——三关 DEMO 至此完成。",
                "秘味：至少两种风味在本关产生过有效得分。",
                "Assets/Art/Generated/Environments/environment_giant_cave.png");

            db.RebuildIndex();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        private static void CreateOrUpdate(
            LevelDatabase db,
            string id,
            string displayName,
            string description,
            int targetScore,
            int maxTurns,
            string chapterId,
            string storyIntro,
            string storyOutro,
            string secretGoal,
            string backgroundPath)
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
            item.SetGoals(targetScore, maxTurns);
            item.SetPresentation(
                chapterId,
                storyIntro,
                storyOutro,
                secretGoal,
                LoadSprite(backgroundPath));

            EditorUtility.SetDirty(item);
            db.EditorAdd(item);
        }

        private static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
