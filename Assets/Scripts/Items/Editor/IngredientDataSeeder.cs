using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Soup.Items.Editor
{
    /// <summary>
    /// Seeds ingredient definitions from the design table「采集物对应数据表」.
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

        [MenuItem("Soup/Ingredient Manager/Seed All Ingredients From Table")]
        public static void SeedAllFromTableMenu()
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

            // —— 采集物对应数据表 ——
            Seed(db, "mushroom", "蘑菇", "素食，20 份柔软食材。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 20f));

            Seed(db, "berry", "小甜果", "素食，30 份柔软食材。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 30f));

            Seed(db, "ice_fruit", "冰晶果", "素食，50 份坚固食材，10 份寒冷。",
                IngredientCategory.Vegetable, veg: true,
                ("坚固食材", 50f), ("寒冷", 10f));

            Seed(db, "hot_fruit", "爆辣果", "素食，50 份强韧食材，10 份热辣。",
                IngredientCategory.Vegetable, veg: true,
                ("强韧食材", 50f), ("热辣", 10f));

            Seed(db, "sour_fruit", "青酸果", "素食，50 份柔软食材，10 份酸涩。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 50f), ("酸涩", 10f));

            Seed(db, "magic_leaf", "魔法叶", "素食，50 份柔软食材，8 份随机风味。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 50f), ("随机风味", 8f));

            Seed(db, "lampwick_grass", "灯芯草", "素食，15 份柔软食材，10 份强韧食材。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 15f), ("强韧食材", 10f));

            Seed(db, "little_white_flower", "小白花", "素食，20 份柔软食材。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 20f));

            Seed(db, "sweet_bun", "甜团团", "肉类，柔软 / 强韧 / 坚固各 20 份。",
                IngredientCategory.Meat, veg: false,
                ("柔软食材", 20f), ("强韧食材", 20f), ("坚固食材", 20f));

            Seed(db, "big_horn_beast", "大角兽", "肉类，30 份柔软食材，100 份坚固食材。",
                IngredientCategory.Meat, veg: false,
                ("柔软食材", 30f), ("坚固食材", 100f));

            Seed(db, "nian_papa", "黏爬爬", "肉类，30 份强韧食材。",
                IngredientCategory.Meat, veg: false,
                ("强韧食材", 30f));

            Seed(db, "little_spiky_ball", "小刺球", "肉类，10 份随机食材。",
                IngredientCategory.Meat, veg: false,
                ("随机食材", 10f));

            Seed(db, "little_silver_fish", "小银鱼", "肉类，50 份柔软食材，10 份鲜美。",
                IngredientCategory.Meat, veg: false,
                ("柔软食材", 50f), ("鲜美", 10f));

            Seed(db, "happy_tuotuo", "快乐坨坨", "肉类，20 份柔软食材。",
                IngredientCategory.Meat, veg: false,
                ("柔软食材", 20f));

            Seed(db, "double_tail_snake", "双尾蛇", "肉类，25 份柔软食材，8 份热辣和 8 份寒冷。",
                IngredientCategory.Meat, veg: false,
                ("柔软食材", 25f), ("热辣", 8f), ("寒冷", 8f));

            Seed(db, "stick_bug", "棍棍虫", "肉类，10 份坚固食材。",
                IngredientCategory.Meat, veg: false,
                ("坚固食材", 10f));

            Seed(db, "mutant_mushroom", "变异蘑菇", "蘑菇进阶获得。20 份柔软，10 份随机风味。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 20f), ("随机风味", 10f));

            Seed(db, "fat_mushroom", "肥大蘑菇", "蘑菇进阶获得。50 份柔软，10 份随机风味。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 50f), ("随机风味", 10f));

            Seed(db, "strange_mushroom", "奇异蘑菇", "蘑菇进阶获得。30 份柔软，20 份随机风味。",
                IngredientCategory.Vegetable, veg: true,
                ("柔软食材", 30f), ("随机风味", 20f));

            Seed(db, "big_ball", "大团球", "甜团团进阶获得。柔软 / 强韧 / 坚固各 80 份。",
                IngredientCategory.Meat, veg: false,
                ("柔软食材", 80f), ("强韧食材", 80f), ("坚固食材", 80f));

            Seed(db, "giant_mountain", "巨团山", "甜团团进阶获得。柔软 / 强韧 / 坚固各 200 份。",
                IngredientCategory.Meat, veg: false,
                ("柔软食材", 200f), ("强韧食材", 200f), ("坚固食材", 200f));

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openWindow)
                IngredientManagerWindow.Open();

            Debug.Log("[物品管理器] 表格食材已填充，当前数量: " + db.Count);
            return db;
        }

        private static void Seed(
            IngredientDatabase db,
            string id,
            string displayName,
            string description,
            IngredientCategory category,
            bool veg,
            params (string key, float value)[] stats)
        {
            var item = FindExisting(id, displayName);
            string path = $"{IngredientFolder}/Ingredient_{id}.asset";
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<IngredientItem>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.SetIdentity(id, displayName);
            item.SetDescription(description);
            item.SetCategory(category);
            item.SetTags(veg
                ? new[] { "素食", "采集物" }
                : new[] { "肉类", "采集物" });
            item.SetCoreValues(0, 0, 99, 1f);
            item.SetStats(stats);
            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static IngredientItem FindExisting(string id, string displayName)
        {
            string preferred = $"{IngredientFolder}/Ingredient_{id}.asset";
            var atPath = AssetDatabase.LoadAssetAtPath<IngredientItem>(preferred);
            if (atPath != null) return atPath;

            foreach (var guid in AssetDatabase.FindAssets("t:IngredientItem"))
            {
                var item = AssetDatabase.LoadAssetAtPath<IngredientItem>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;
                if (!string.IsNullOrEmpty(id) && item.Id == id)
                    return item;
                if (!string.IsNullOrEmpty(displayName)
                    && string.Equals(item.DisplayName, displayName, System.StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                parent = parent.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent))
                    EnsureFolder(parent);
            }

            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
