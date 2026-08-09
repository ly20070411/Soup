using UnityEditor;
using UnityEngine;

namespace Soup.Items.Editor
{
    /// <summary>
    /// One-click sample data bootstrap for the ingredient manager.
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

            CreateOrUpdate(db, "tomato", "番茄", "酸甜多汁的基础汤底食材。",
                IngredientCategory.Vegetable, new[] { "蔬菜", "汤底", "红色" }, 8, 1,
                item =>
                {
                    item.SetStat("salty", 0.2f);
                    item.SetStat("sweet", 1.5f);
                    item.SetStat("sour", 2.0f);
                    item.SetStat("umami", 1.0f);
                    item.SetStat("cookTime", 1.2f);
                });

            CreateOrUpdate(db, "potato", "土豆", "厚实软糯，增加汤的饱腹感。",
                IngredientCategory.Vegetable, new[] { "蔬菜", "淀粉" }, 6, 0,
                item =>
                {
                    item.SetStat("salty", 0.1f);
                    item.SetStat("sweet", 0.6f);
                    item.SetStat("umami", 0.4f);
                    item.SetStat("cookTime", 2.0f);
                });

            CreateOrUpdate(db, "beef", "牛肉", "浓郁肉香，提升鲜味。",
                IngredientCategory.Meat, new[] { "肉类", "高蛋白" }, 28, 2,
                item =>
                {
                    item.SetStat("salty", 0.5f);
                    item.SetStat("umami", 3.0f);
                    item.SetStat("heat", 0.2f);
                    item.SetStat("cookTime", 3.5f);
                });

            CreateOrUpdate(db, "ginger", "生姜", "驱寒提味，常用香辛料。",
                IngredientCategory.Spice, new[] { "香料", "辛香" }, 5, 1,
                item =>
                {
                    item.SetStat("spicy", 1.2f);
                    item.SetStat("heat", 1.5f);
                    item.SetStat("bitter", 0.4f);
                    item.SetStat("cookTime", 0.5f);
                });

            CreateOrUpdate(db, "kelp", "海带", "天然鲜味来源，适合清汤。",
                IngredientCategory.Seafood, new[] { "海鲜", "汤底", "鲜味" }, 10, 1,
                item =>
                {
                    item.SetStat("salty", 1.0f);
                    item.SetStat("umami", 2.8f);
                    item.SetStat("cookTime", 1.8f);
                });

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openWindow)
                IngredientManagerWindow.Open();

            Debug.Log("[物品管理器] 示例食材已填充，当前数量: " + db.Count);
            return db;
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
