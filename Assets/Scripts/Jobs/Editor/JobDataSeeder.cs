using Soup.Items;
using UnityEditor;
using UnityEngine;

namespace Soup.Jobs.Editor
{
    /// <summary>
    /// One-click bootstrap for job/station sample data from the design table.
    /// </summary>
    public static class JobDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/JobDatabase.asset";
        private const string JobFolder = "Assets/Data/Jobs";
        private const string IngredientDatabasePath = "Assets/Resources/IngredientDatabase.asset";

        private const int DefaultGatherMaxWorkers = 5;
        private const int DefaultProcessMaxWorkers = 5;

        [MenuItem("Soup/Job Manager/Seed Sample Jobs")]
        public static void SeedSamplesMenu()
        {
            SeedSamples(openWindow: true);
        }

        public static JobDatabase SeedSamples(bool openWindow = false)
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(JobFolder);

            var db = AssetDatabase.LoadAssetAtPath<JobDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<JobDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            var ingredients = AssetDatabase.LoadAssetAtPath<IngredientDatabase>(IngredientDatabasePath);
            ingredients?.RebuildIndex();

            // Gather — 岗位名即产出食材名，按显示名关联物品管理器中的食材
            CreateGather(db, ingredients, "mushroom", "蘑菇", "采集蘑菇。", 5,
                IngredientMaterial.Soft, 2);
            CreateGather(db, ingredients, "berry", "小甜果", "采集小甜果。", 5,
                IngredientMaterial.Soft, 1);
            CreateGather(db, ingredients, "ice_fruit", "冰晶果", "采集冰晶果。", 2,
                IngredientMaterial.Soft, 1, cold: 1);
            CreateGather(db, ingredients, "hot_fruit", "爆辣果", "采集爆辣果。", 2,
                IngredientMaterial.Soft, 1, spicy: 1);
            CreateGather(db, ingredients, "sour_fruit", "青酸果", "采集青酸果。", 2,
                IngredientMaterial.Soft, 1, sour: 1);
            CreateGather(db, ingredients, "magic_leaf", "魔法叶", "采集魔法叶。", 2,
                IngredientMaterial.Soft, 1);
            CreateGather(db, ingredients, "sweet_bun", "甜团团", "采集甜团团。", 4,
                IngredientMaterial.Soft, 1);
            CreateGather(db, ingredients, "big_horn_beast", "大角兽", "采集大角兽。", 1,
                IngredientMaterial.Tough, 1);
            CreateGather(db, ingredients, "nian_papa", "黏爬爬", "采集黏爬爬。", 4,
                IngredientMaterial.Tough, 1);
            CreateGather(db, ingredients, "little_spiky_ball", "小刺球", "采集小刺球。", 15,
                IngredientMaterial.Solid, 1);

            // Process
            CreateProcess(db, "knife_cut", "刀切", "优先处理柔软食材，其他材质效率减半。",
                10, IngredientMaterial.Soft, 0.5f, false);
            CreateProcess(db, "chainsaw", "电锯", "优先处理强韧食材，其他材质效率减半。",
                10, IngredientMaterial.Tough, 0.5f, false);
            CreateProcess(db, "drill", "钻头", "优先处理坚固食材，其他材质效率减半。",
                10, IngredientMaterial.Solid, 0.5f, false);
            CreateProcess(db, "explosion", "爆炸", "处理任意食材（每精灵 8 份），优先处理其他岗位难以处理的材质。",
                8, IngredientMaterial.Any, 1f, true);

            // Cook (unlimited workers)
            CreateCook(db, "low_heat", "小火", "小火慢炖，满分倍率。", 10, 1.0f);
            CreateCook(db, "medium_heat", "中火", "中火烹饪，效率与倍率折中。", 18, 0.8f);
            CreateCook(db, "high_heat", "大火", "大火快煮，消耗更多处理食材。", 30, 0.6f);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openWindow)
                JobManagerWindow.Open();

            Debug.Log("[岗位管理器] 示例岗位已填充，当前数量: " + db.Count);
            return db;
        }

        [MenuItem("Soup/Job Manager/Link Gather Jobs By Ingredient Name")]
        public static void LinkGatherJobsByNameMenu()
        {
            int linked = LinkGatherJobsByIngredientName();
            Debug.Log($"[岗位管理器] 已按名称关联采集岗位食材，成功 {linked} 个。");
        }

        /// <summary>
        /// Match each gather job's display name to an IngredientItem display name and wire OutputIngredient.
        /// </summary>
        public static int LinkGatherJobsByIngredientName()
        {
            var ingredients = new System.Collections.Generic.List<IngredientItem>();
            foreach (var guid in AssetDatabase.FindAssets("t:IngredientItem"))
            {
                var ingredient = AssetDatabase.LoadAssetAtPath<IngredientItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (ingredient != null)
                    ingredients.Add(ingredient);
            }

            int linked = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:JobItem"))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (job == null || job.JobType != JobType.Gather)
                    continue;

                var match = FindIngredientByDisplayName(ingredients, job.DisplayName);
                if (match == null)
                {
                    Debug.LogWarning($"[岗位管理器] 未找到与采集岗位「{job.DisplayName}」同名的食材。");
                    continue;
                }

                job.SetGather(job.GatherAmountPerWorker, match);
                EditorUtility.SetDirty(job);
                linked++;
            }

            AssetDatabase.SaveAssets();
            return linked;
        }

        private static IngredientItem FindIngredientByDisplayName(
            System.Collections.Generic.List<IngredientItem> ingredients,
            string displayName)
        {
            if (ingredients == null || string.IsNullOrWhiteSpace(displayName))
                return null;

            for (int i = 0; i < ingredients.Count; i++)
            {
                var ingredient = ingredients[i];
                if (ingredient != null &&
                    string.Equals(ingredient.DisplayName, displayName, System.StringComparison.Ordinal))
                    return ingredient;
            }

            return null;
        }

        private static void CreateGather(
            JobDatabase db,
            IngredientDatabase ingredients,
            string id,
            string displayName,
            string description,
            int amountPerWorker,
            IngredientMaterial material = IngredientMaterial.Soft,
            int materialPerUnit = 1,
            int spicy = 0,
            int sour = 0,
            int cold = 0,
            int magic = 0)
        {
            var item = GetOrCreate(db, id, displayName);
            item.SetDescription(description);
            item.SetMaxWorkers(DefaultGatherMaxWorkers);

            IngredientItem ingredient = null;
            if (ingredients != null)
                ingredient = ingredients.FindByName(displayName);

            if (ingredient == null)
            {
                var all = new System.Collections.Generic.List<IngredientItem>();
                foreach (var guid in AssetDatabase.FindAssets("t:IngredientItem"))
                {
                    var found = AssetDatabase.LoadAssetAtPath<IngredientItem>(AssetDatabase.GUIDToAssetPath(guid));
                    if (found != null)
                        all.Add(found);
                }
                ingredient = FindIngredientByDisplayName(all, displayName);
            }

            if (ingredient == null)
                Debug.LogWarning($"[岗位管理器] 采集岗位「{displayName}」未在物品管理器中找到同名食材。");

            item.SetGather(amountPerWorker, ingredient);
            item.SetGatherConversion(material, materialPerUnit, spicy, sour, cold, magic);
            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static void CreateProcess(
            JobDatabase db,
            string id,
            string displayName,
            string description,
            int amountPerWorker,
            IngredientMaterial preferred,
            float otherEfficiency,
            bool random)
        {
            var item = GetOrCreate(db, id, displayName);
            item.SetDescription(description);
            item.SetMaxWorkers(DefaultProcessMaxWorkers);
            item.SetProcess(amountPerWorker, preferred, otherEfficiency, random);
            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static void CreateCook(
            JobDatabase db,
            string id,
            string displayName,
            string description,
            int amountPerWorker,
            float scoreMultiplier)
        {
            var item = GetOrCreate(db, id, displayName);
            item.SetDescription(description);
            item.SetCook(amountPerWorker, scoreMultiplier);
            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static JobItem GetOrCreate(JobDatabase db, string id, string displayName)
        {
            string path = JobFolder + "/Job_" + id + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<JobItem>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.SetIdentity(id, displayName);
            return item;
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
