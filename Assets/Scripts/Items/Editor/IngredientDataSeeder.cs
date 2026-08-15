using UnityEditor;
using UnityEngine;

namespace Soup.Items.Editor
{
    /// <summary>
    /// Seeds the design-doc ingredient catalog (采集物对应数据表) into the manager.
    /// </summary>
    public static class IngredientDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/IngredientDatabase.asset";
        private const string IngredientFolder = "Assets/Data/Ingredients";

        [MenuItem("Soup/Ingredient Manager/Seed Sample Ingredients")]
        public static void SeedSamplesMenu()
        {
            SeedSamples(openWindow: true);
        }

        public static IngredientDatabase SeedSamples(bool openWindow = false)
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(IngredientFolder);

            var db = AssetDatabase.LoadAssetAtPath<IngredientDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<IngredientDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            SeedGatherIngredients(db);

            // 美术素材已放入 Docs 时立即绑定图标（幂等）。
            Soup.Game.Editor.ArtIconLinker.LinkCompletedIcons(quiet: true);

            if (openWindow)
                IngredientManagerWindow.Open();

            Debug.Log("[物品管理器] 设计文档食材已填充，当前数量: " + db.Count);
            return db;
        }

        private static void SeedGatherIngredients(IngredientDatabase db)
        {
            // —— 素食 ——
            CreateOrUpdate(db, "mushroom", "蘑菇", "基础蘑菇食材。",
                IngredientCategory.Vegetable, new[] { "素食", "蘑菇" }, 5, 0,
                item => item.SetStat("soft", 20));

            CreateOrUpdate(db, "berry", "小甜果", "通红带高光的甜果子。",
                IngredientCategory.Vegetable, new[] { "素食", "果子" }, 8, 0,
                item => item.SetStat("soft", 30));

            CreateOrUpdate(db, "ice_fruit", "冰晶果", "透亮冰蓝的果实，带着寒气。",
                IngredientCategory.Vegetable, new[] { "素食", "果子", "寒冷" }, 12, 1,
                item =>
                {
                    item.SetStat("solid", 50);
                    item.SetStat("cold", 10);
                });

            CreateOrUpdate(db, "hot_fruit", "爆辣果", "表皮带火焰纹路的辣果子。",
                IngredientCategory.Vegetable, new[] { "素食", "果子", "热辣" }, 12, 1,
                item =>
                {
                    item.SetStat("tough", 50);
                    item.SetStat("spicy", 10);
                });

            CreateOrUpdate(db, "sour_fruit", "青酸果", "青绿色表皮的酸果子。",
                IngredientCategory.Vegetable, new[] { "素食", "果子", "酸涩" }, 12, 1,
                item =>
                {
                    item.SetStat("soft", 50);
                    item.SetStat("sour", 10);
                });

            CreateOrUpdate(db, "magic_leaf", "魔法叶", "七彩的叶子，散发奇异风味。",
                IngredientCategory.Vegetable, new[] { "素食", "叶子", "随机" }, 15, 2,
                item =>
                {
                    item.SetStat("soft", 50);
                    item.SetStat("random_flavor", 8);
                });

            CreateOrUpdate(db, "rush", "灯芯草", "半透明的淡蓝色草花。",
                IngredientCategory.Vegetable, new[] { "素食", "草" }, 10, 1,
                item =>
                {
                    item.SetStat("soft", 15);
                    item.SetStat("tough", 10);
                });

            CreateOrUpdate(db, "daisy", "小白花", "黄色花蕊的白色雏菊。",
                IngredientCategory.Vegetable, new[] { "素食", "花" }, 8, 0,
                item => item.SetStat("soft", 20));

            CreateOrUpdate(db, "mutant_mushroom", "变异蘑菇", "蘑菇的变种，风味更活跃。",
                IngredientCategory.Vegetable, new[] { "素食", "蘑菇", "随机" }, 10, 1,
                item =>
                {
                    item.SetStat("soft", 20);
                    item.SetStat("random_flavor", 10);
                });

            CreateOrUpdate(db, "fat_mushroom", "肥大蘑菇", "蘑菇的变种，产量更高。",
                IngredientCategory.Vegetable, new[] { "素食", "蘑菇", "随机" }, 15, 1,
                item =>
                {
                    item.SetStat("soft", 50);
                    item.SetStat("random_flavor", 10);
                });

            CreateOrUpdate(db, "strange_mushroom", "奇异蘑菇", "蘑菇的变种，风味极其丰富。",
                IngredientCategory.Vegetable, new[] { "素食", "蘑菇", "随机" }, 18, 2,
                item =>
                {
                    item.SetStat("soft", 30);
                    item.SetStat("random_flavor", 20);
                });

            // —— 肉类 ——
            CreateOrUpdate(db, "sweet_bun", "甜团团", "套着两层皮的大福气团。",
                IngredientCategory.Meat, new[] { "肉类" }, 18, 1,
                item =>
                {
                    item.SetStat("soft", 20);
                    item.SetStat("tough", 20);
                    item.SetStat("solid", 20);
                });

            CreateOrUpdate(db, "big_horn_beast", "大角兽", "长着螺旋大角的小怪物。",
                IngredientCategory.Meat, new[] { "肉类" }, 25, 2,
                item =>
                {
                    item.SetStat("soft", 50);
                    item.SetStat("solid", 100);
                });

            CreateOrUpdate(db, "nian_papa", "黏爬爬", "软软的长条型怪物。",
                IngredientCategory.Meat, new[] { "肉类" }, 12, 0,
                item =>
                {
                    item.SetStat("soft", 20);
                    item.SetStat("tough", 15);
                });

            CreateOrUpdate(db, "little_spiky_ball", "小刺球", "绿色针状外壳的凶小球。",
                IngredientCategory.Meat, new[] { "肉类", "随机" }, 10, 1,
                item => item.SetStat("random", 10));

            CreateOrUpdate(db, "silver_fish", "小银鱼", "银肚浅背的小河豚。",
                IngredientCategory.Meat, new[] { "肉类", "鱼", "鲜美" }, 15, 1,
                item =>
                {
                    item.SetStat("soft", 20);
                    item.SetStat("magic", 4);
                });

            CreateOrUpdate(db, "happy_blob", "快乐坨坨", "亮橙色的开心大便。",
                IngredientCategory.Meat, new[] { "肉类" }, 10, 0,
                item => item.SetStat("soft", 30));

            CreateOrUpdate(db, "twin_tail_snake", "双尾蛇", "红蓝双尾的蛇。",
                IngredientCategory.Meat, new[] { "肉类" }, 20, 1,
                item =>
                {
                    item.SetStat("soft", 40);
                    item.SetStat("tough", 25);
                });

            CreateOrUpdate(db, "stick_bug", "棍棍虫", "像干竹棍一样的虫子。",
                IngredientCategory.Meat, new[] { "肉类" }, 8, 0,
                item => item.SetStat("solid", 10));
        }

        private static void CreateOrUpdate(
            IngredientDatabase db,
            string id,
            string displayName,
            string description,
            IngredientCategory category,
            string[] tags,
            int price,
            int rarity,
            System.Action<IngredientItem> configureStats)
        {
            string path = IngredientFolder + "/Ingredient_" + id + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<IngredientItem>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<IngredientItem>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.SetIdentity(id, displayName);
            item.SetDescription(description);
            item.SetCategory(category);
            item.SetTags(tags);
            item.SetCoreValues(price, rarity, 99, 1f);
            configureStats?.Invoke(item);
            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                parent = parent.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
            }

            var name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
