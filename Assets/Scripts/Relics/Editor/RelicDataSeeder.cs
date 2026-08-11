using Soup.Items;
using Soup.Jobs;
using UnityEditor;
using UnityEngine;

namespace Soup.Relics.Editor
{
    /// <summary>
    /// Seeds the four sample relics from the design brief.
    /// </summary>
    public static class RelicDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/RelicDatabase.asset";
        private const string RelicFolder = "Assets/Data/Relics";
        private const string IngredientFolder = "Assets/Data/Ingredients";

        [MenuItem("Soup/Relic Manager/Seed Sample Relics")]
        public static void SeedSamplesMenu()
        {
            SeedSamples(openWindow: true);
        }

        public static RelicDatabase SeedSamples(bool openWindow = false)
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(RelicFolder);
            EnsureFolder(IngredientFolder);

            var db = AssetDatabase.LoadAssetAtPath<RelicDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<RelicDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            var mushroom = EnsureMushroomIngredient();

            CreateOrUpdate(
                db,
                "vegetarian_heart",
                "素斋之心",
                "若本回合没有采集肉类食材，最终倍率 +0.6。",
                RelicAcquireStage.Stage1,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.NoCategoryGathered, IngredientCategory.Meat, 0);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplier, 0.6f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "unlimited_chili",
                "无限辣椒",
                "热辣的加成没有上限。",
                RelicAcquireStage.Stage2,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.BeforeSpicy);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.DisableSpicyCap, 0f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "mushroom_companion",
                "蘑菇伴生",
                "每采集 5 个采集物，产出 1 份蘑菇食材（按蘑菇产量结算）。",
                RelicAcquireStage.Stage2,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterGather);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.GrantIngredientPerGather, 0f, 5, 1, mushroom);
                });

            CreateOrUpdate(
                db,
                "five_flavor_harmony",
                "五味调和",
                "每有一种风味存量，最终倍率 +0.2。",
                RelicAcquireStage.Stage3,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplierPerPresentFlavor, 0.2f, 0, 0, null);
                });

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openWindow)
                RelicManagerWindow.Open();

            Debug.Log("[遗物管理器] 示例遗物已填充，当前数量: " + db.Count);
            return db;
        }

        private static void CreateOrUpdate(
            RelicDatabase db,
            string id,
            string displayName,
            string description,
            RelicAcquireStage stage,
            System.Action<RelicRule> configureRule)
        {
            string path = $"{RelicFolder}/Relic_{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<RelicItem>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<RelicItem>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.SetIdentity(id, displayName);
            item.SetDescription(description);
            item.SetAcquireStage(stage);
            item.ClearRules();

            var rule = new RelicRule();
            configureRule?.Invoke(rule);
            item.AddRule(rule);

            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static IngredientItem EnsureMushroomIngredient()
        {
            // Prefer gather job link.
            foreach (var guid in AssetDatabase.FindAssets("t:JobItem"))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (job == null || job.JobType != JobType.Gather) continue;
                if (job.Id == "mushroom" || job.DisplayName == "蘑菇")
                {
                    if (job.OutputIngredient != null)
                        return job.OutputIngredient;
                }
            }

            // Prefer existing ingredient by display name or id.
            IngredientItem byName = null;
            IngredientItem byId = null;
            foreach (var guid in AssetDatabase.FindAssets("t:IngredientItem"))
            {
                var item = AssetDatabase.LoadAssetAtPath<IngredientItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;
                if (item.DisplayName == "蘑菇")
                    byName = item;
                if (item.Id == "mushroom")
                    byId = item;
            }

            if (byName != null)
                return byName;

            // Create a dedicated mushroom ingredient (avoid reusing mis-id potato assets).
            string path = $"{IngredientFolder}/Ingredient_mushroom.asset";
            var created = AssetDatabase.LoadAssetAtPath<IngredientItem>(path);
            if (created == null)
            {
                created = ScriptableObject.CreateInstance<IngredientItem>();
                AssetDatabase.CreateAsset(created, path);
            }

            created.SetIdentity("mushroom_relic", "蘑菇");
            created.SetDescription("遗物蘑菇伴生用食材。");
            created.SetCategory(IngredientCategory.Vegetable);
            created.SetCoreValues(0, 0, 99, 1f);
            created.SetStat("柔软食材", 2f);
            EditorUtility.SetDirty(created);

            // Also register in ingredient database if present.
            var ingredientDb = AssetDatabase.LoadAssetAtPath<IngredientDatabase>("Assets/Resources/IngredientDatabase.asset");
            if (ingredientDb != null)
            {
                ingredientDb.Add(created);
                EditorUtility.SetDirty(ingredientDb);
            }

            // Wire gather job if found.
            foreach (var guid in AssetDatabase.FindAssets("t:JobItem"))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (job == null || job.JobType != JobType.Gather) continue;
                if (job.Id == "mushroom" || job.DisplayName == "蘑菇")
                {
                    if (job.OutputIngredient == null)
                    {
                        job.SetGather(job.GatherAmountPerWorker, created);
                        EditorUtility.SetDirty(job);
                    }
                }
            }

            if (byId != null && byId != created && byId.DisplayName != "蘑菇")
            {
                Debug.LogWarning(
                    $"[遗物管理器] 发现 id=mushroom 但显示名不是蘑菇的食材「{byId.DisplayName}」，已新建 Ingredient_mushroom。");
            }

            return created;
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
