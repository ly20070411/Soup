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

        [MenuItem("Soup/Job Manager/Seed Sweet Bun Advance Tree")]
        public static void SeedSweetBunAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_sweet_bun.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedSweetBunAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入甜团团进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Big Horn Beast Advance Tree")]
        public static void SeedBigHornBeastAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_big_horn_beast.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedBigHornBeastAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入大角兽进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Nian Papa Advance Tree")]
        public static void SeedNianPapaAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_nian_papa.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedNianPapaAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入黏爬爬进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Little Spiky Ball Advance Tree")]
        public static void SeedLittleSpikyBallAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_little_spiky_ball.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedLittleSpikyBallAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入小刺球进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Little Silver Fish Advance Tree")]
        public static void SeedLittleSilverFishAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_little_silver_fish.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedLittleSilverFishAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入小银鱼进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Happy Tuotuo Advance Tree")]
        public static void SeedHappyTuotuoAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_happy_tuotuo.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedHappyTuotuoAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入快乐坨坨进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Double Tail Snake Advance Tree")]
        public static void SeedDoubleTailSnakeAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_double_tail_snake.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedDoubleTailSnakeAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入双尾蛇进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Stick Bug Advance Tree")]
        public static void SeedStickBugAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_stick_bug.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedStickBugAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入棍棍虫进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Knife Cut Advance Tree")]
        public static void SeedKnifeCutAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_knife_cut.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedKnifeCutAdvanceTree();
            item.SetDescription("优先处理 120 份柔软食材，其他食材效率减半。");
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入刀切进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Chainsaw Advance Tree")]
        public static void SeedChainsawAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_chainsaw.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedChainsawAdvanceTree();
            item.SetDescription("优先处理 120 份强韧食材，其他食材效率减半。");
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入电锯进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Drill Advance Tree")]
        public static void SeedDrillAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_drill.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedDrillAdvanceTree();
            item.SetDescription("优先处理 120 份坚固食材，其他食材效率减半。");
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入钻头进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Explosion Advance Tree")]
        public static void SeedExplosionAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_explosion.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedExplosionAdvanceTree();
            item.SetDescription("随机处理任意 100 份食材，优先处理其他岗位难以处理的食材。");
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入爆炸进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed High Heat Advance Tree")]
        public static void SeedHighHeatAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_high_heat.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedHighHeatAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入大火进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Medium Heat Advance Tree")]
        public static void SeedMediumHeatAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_medium_heat.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedMediumHeatAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入中火进阶树。");
        }

        [MenuItem("Soup/Job Manager/Seed Low Heat Advance Tree")]
        public static void SeedLowHeatAdvanceMenu()
        {
            string path = $"{JobFolder}/Job_low_heat.asset";
            var item = AssetDatabase.LoadAssetAtPath<JobItem>(path);
            if (item == null)
            {
                Debug.LogError($"[岗位管理器] 未找到 {path}");
                return;
            }

            item.SeedLowHeatAdvanceTree();
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Debug.Log("[岗位管理器] 已写入小火进阶树。");
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
            // 变异/奇异/肥大蘑菇衍生物由蘑菇岗进阶获取（岗位不单独存在）
            // materialPerUnit / flavors 为无食材资产时的回退；有食材时走 IngredientYieldResolver
            CreateGather(db, ingredients, "mushroom", "蘑菇", "采集蘑菇。", 5,
                IngredientMaterial.Soft, 20);
            CreateGather(db, ingredients, "berry", "小甜果", "采集小甜果。", 5,
                IngredientMaterial.Soft, 30);
            CreateGather(db, ingredients, "little_white_flower", "小白花", "采集小白花。", 5,
                IngredientMaterial.Soft, 20);
            CreateGather(db, ingredients, "little_silver_fish", "小银鱼", "采集小银鱼。", 2,
                IngredientMaterial.Soft, 50, magic: 10);
            CreateGather(db, ingredients, "happy_tuotuo", "快乐坨坨", "采集快乐坨坨。", 5,
                IngredientMaterial.Soft, 20);
            CreateGather(db, ingredients, "double_tail_snake", "双尾蛇", "采集双尾蛇。", 2,
                IngredientMaterial.Soft, 25, spicy: 8, cold: 8);
            CreateGather(db, ingredients, "stick_bug", "棍棍虫", "采集棍棍虫。", 12,
                IngredientMaterial.Solid, 10);
            CreateGather(db, ingredients, "ice_fruit", "冰晶果", "采集冰晶果。", 2,
                IngredientMaterial.Solid, 50, cold: 10);
            CreateGather(db, ingredients, "hot_fruit", "爆辣果", "采集爆辣果。", 2,
                IngredientMaterial.Tough, 50, spicy: 10);
            CreateGather(db, ingredients, "sour_fruit", "青酸果", "采集青酸果。", 2,
                IngredientMaterial.Soft, 50, sour: 10);
            CreateGather(db, ingredients, "magic_leaf", "魔法叶", "采集魔法叶。", 2,
                IngredientMaterial.Soft, 50);
            CreateGather(db, ingredients, "lampwick_grass", "灯芯草", "采集灯芯草。", 3,
                IngredientMaterial.Soft, 15);
            CreateGather(db, ingredients, "sweet_bun", "甜团团", "采集甜团团。", 3,
                IngredientMaterial.Soft, 20);
            CreateGather(db, ingredients, "big_horn_beast", "大角兽", "采集大角兽。", 1,
                IngredientMaterial.Soft, 30);
            CreateGather(db, ingredients, "nian_papa", "黏爬爬", "采集黏爬爬。", 4,
                IngredientMaterial.Tough, 30);
            CreateGather(db, ingredients, "little_spiky_ball", "小刺球", "采集小刺球。", 15,
                IngredientMaterial.Solid, 10);

            // Process
            CreateProcess(db, "knife_cut", "刀切", "优先处理 120 份柔软食材，其他食材效率减半。",
                120, IngredientMaterial.Soft, 0.5f, false, 100);
            CreateProcess(db, "chainsaw", "电锯", "优先处理 120 份强韧食材，其他食材效率减半。",
                120, IngredientMaterial.Tough, 0.5f, false, 100);
            CreateProcess(db, "drill", "钻头", "优先处理 120 份坚固食材，其他食材效率减半。",
                120, IngredientMaterial.Solid, 0.5f, false, 100);
            CreateProcess(db, "explosion", "爆炸",
                "随机处理任意 100 份食材，优先处理其他岗位难以处理的食材。",
                100, IngredientMaterial.Any, 1f, true, 0);

            // Cook (unlimited workers)
            CreateCook(db, "low_heat", "小火", "小火慢炖，满分倍率。", 200, 1.5f);
            CreateCook(db, "medium_heat", "中火", "烹饪360份食材，分数倍率1.0", 360, 1.0f);
            CreateCook(db, "high_heat", "大火", "烹饪500份食材，分数倍率0.8", 500, 0.8f);

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
            if (id == "mushroom")
                item.SeedMushroomAdvanceTree();
            else if (id == "berry")
                item.SeedBerryAdvanceTree();
            else if (id == "ice_fruit")
                item.SeedIceFruitAdvanceTree();
            else if (id == "hot_fruit")
                item.SeedHotFruitAdvanceTree();
            else if (id == "sour_fruit")
                item.SeedSourFruitAdvanceTree();
            else if (id == "magic_leaf")
                item.SeedMagicLeafAdvanceTree();
            else if (id == "lampwick_grass")
                item.SeedLampwickGrassAdvanceTree();
            else if (id == "little_white_flower")
                item.SeedLittleWhiteFlowerAdvanceTree();
            else if (id == "sweet_bun")
                item.SeedSweetBunAdvanceTree();
            else if (id == "big_horn_beast")
                item.SeedBigHornBeastAdvanceTree();
            else if (id == "nian_papa")
                item.SeedNianPapaAdvanceTree();
            else if (id == "little_spiky_ball")
                item.SeedLittleSpikyBallAdvanceTree();
            else if (id == "little_silver_fish")
                item.SeedLittleSilverFishAdvanceTree();
            else if (id == "happy_tuotuo")
                item.SeedHappyTuotuoAdvanceTree();
            else if (id == "double_tail_snake")
                item.SeedDoubleTailSnakeAdvanceTree();
            else if (id == "stick_bug")
                item.SeedStickBugAdvanceTree();
            else if (id == "knife_cut")
                item.SeedKnifeCutAdvanceTree();
            else if (id == "chainsaw")
                item.SeedChainsawAdvanceTree();
            else
                item.SeedDefaultAdvanceTree();
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
            bool random,
            int processPriority = 100)
        {
            var item = GetOrCreate(db, id, displayName);
            item.SetDescription(description);
            item.SetMaxWorkers(DefaultProcessMaxWorkers);
            item.SetProcess(amountPerWorker, preferred, otherEfficiency, random, processPriority);
            if (id == "knife_cut")
                item.SeedKnifeCutAdvanceTree();
            else if (id == "chainsaw")
                item.SeedChainsawAdvanceTree();
            else if (id == "drill")
                item.SeedDrillAdvanceTree();
            else if (id == "explosion")
                item.SeedExplosionAdvanceTree();
            else
                item.SeedDefaultAdvanceTree();
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
            if (id == "low_heat")
                item.SeedLowHeatAdvanceTree();
            else if (id == "medium_heat")
                item.SeedMediumHeatAdvanceTree();
            else if (id == "high_heat")
                item.SeedHighHeatAdvanceTree();
            else
                item.SeedDefaultAdvanceTree();
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
