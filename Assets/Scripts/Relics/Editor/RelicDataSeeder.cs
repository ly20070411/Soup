using System.Collections.Generic;
using Soup.Employees;
using Soup.Items;
using Soup.Jobs;
using UnityEditor;
using UnityEngine;

namespace Soup.Relics.Editor
{
    /// <summary>
    /// Seeds the full design relic set into Relic Manager / RelicDatabase.
    /// </summary>
    public static class RelicDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/RelicDatabase.asset";
        private const string RelicFolder = "Assets/Data/Relics";
        private const string IngredientFolder = "Assets/Data/Ingredients";

        private static readonly string[] ObsoleteRelicIds =
        {
            "vegetarian_heart",
            "unlimited_chili",
            "mushroom_companion",
            "five_flavor_harmony",
            "flavor_salt",
            "lost"
        };

        [MenuItem("Soup/Relic Manager/Seed Sample Relics")]
        public static void SeedSamplesMenu()
        {
            SeedAll(openWindow: true);
        }

        [MenuItem("Soup/Relic Manager/Seed All Relics")]
        public static void SeedAllMenu()
        {
            SeedAll(openWindow: true);
        }

        public static RelicDatabase SeedSamples(bool openWindow = false) => SeedAll(openWindow);

        public static RelicDatabase SeedAll(bool openWindow = false)
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

            DeleteObsolete(db);

            var mushroom = EnsureMushroomIngredient();
            var incentive = CreateOrUpdate(
                db,
                "incentive",
                "激励",
                "全局工作效率加 0.1。可重复获得。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddGlobalLaborEfficiency, 0.1f, 0, 0, null);
                });

            // —— 开局 ——
            CreateOrUpdate(
                db,
                "vegetarianism",
                "素食主义",
                "若没有肉类食材，最终倍率 +0.6。",
                RelicAcquireStage.Starting,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.NoCategoryGathered, IngredientCategory.Meat, 0);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplier, 0.6f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "no_spice_no_joy",
                "无辣不欢",
                "热辣的加成没有上限。",
                RelicAcquireStage.Starting,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.BeforeSpicy);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.DisableSpicyCap, 0f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "spore_invasion",
                "孢子入侵",
                "每采集 5 个采集物，就会产出 1 份蘑菇食材。",
                RelicAcquireStage.Starting,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterGather);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.GrantIngredientPerGather, 0f, 5, 1, mushroom);
                });

            // —— 事件 ——
            CreateOrUpdate(
                db,
                "compound_salt",
                "复合香盐",
                "每有一种风味，最终倍率增加 0.2。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplierPerPresentFlavor, 0.2f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "big_warehouse",
                "大仓库",
                "仓库存储增加 200。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ModifyWarehouseCapacity, 0f, 0, 200, null);
                });

            CreateOrUpdate(
                db,
                "pure_salt",
                "纯正香盐",
                "在风味小于等于 1 种时，最终倍率增加 0.5。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.HasFlavorCountAtMost, IngredientCategory.Other, 1);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplier, 0.5f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "ghost_urn",
                "幽灵瓮",
                "幽灵的工作效率加 0.2。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(
                        RelicEffectType.AddEmployeeTypeLaborEfficiency,
                        0.2f,
                        0,
                        0,
                        null,
                        IngredientMaterial.Soft,
                        EmployeeManager.GhostId);
                });

            CreateOrUpdate(
                db,
                "necronomicon",
                "死灵书",
                "获得后，每损失一个小精灵，获得一个幽灵。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(
                        RelicEffectType.GrantEmployeeOnElfLoss,
                        0f,
                        0,
                        1,
                        null,
                        IngredientMaterial.Soft,
                        EmployeeManager.GhostId);
                });

            CreateOrUpdate(
                db,
                "delicious_powder",
                "美味粉",
                "最终倍率加 0.1。可重复获得。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplier, 0.1f, 0, 0, null);
                });

            // incentive already created above (linked by ritual)

            CreateOrUpdate(
                db,
                "kitchen_accident",
                "厨房事故",
                "最终倍率 -0.1。可重复获得。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplier, -0.1f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "fatigue",
                "疲倦",
                "全局工作效率减 0.1。可重复获得。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddGlobalLaborEfficiency, -0.1f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "moss",
                "苔藓",
                "回合开始时，获得上一回合未使用仓库数量 10% 的柔软食材。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.TurnStart);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.GrantSoftFromUnusedWarehousePercent, 0.1f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "handle_moss",
                "处理苔藓",
                "开局时获得 50 个柔软食物。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(
                        RelicEffectType.AddRawMaterial,
                        0f,
                        0,
                        50,
                        null,
                        IngredientMaterial.Soft);
                });

            CreateOrUpdate(
                db,
                "hero_expedition",
                "勇者出征",
                "回合开始时，有 50% 的概率获得随机一种未处理食材 40 份。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.TurnStart);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ChanceGrantRandomRaw, 0.5f, 0, 40, null);
                });

            CreateOrUpdateMulti(
                db,
                "strange_scent_stone",
                "异香石",
                "前 5 个回合全局工作效率加 0.5，后 5 个回合全局工作效率减 0.5。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.TurnIndexInRange, IngredientCategory.Other, 1, 5);
                    rule.SetEffect(RelicEffectType.AddGlobalLaborEfficiency, 0.5f, 0, 0, null);
                },
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.TurnIndexInRange, IngredientCategory.Other, 6, 10);
                    rule.SetEffect(RelicEffectType.AddGlobalLaborEfficiency, -0.5f, 0, 0, null);
                });

            CreateOrUpdateMulti(
                db,
                "cruel_delicious",
                "残酷的美味",
                "关卡开始时，小精灵数量减 2；最终分数倍率乘以 1.3（独立乘区）。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.LevelStart);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ModifyElfCount, 0f, 0, -2, null);
                },
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.MultiplyIndependentScore, 1.3f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "stewed_zhizhi",
                "炖煮吱吱",
                "开局获得 100 处理食材。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddProcessed, 0f, 0, 100, null);
                });

            CreateOrUpdate(
                db,
                "penguin_blessing",
                "凑企鹅的祝福",
                "每生产 10 个坚固食材，额外获得 1 个坚固食材。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterGather);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(
                        RelicEffectType.GrantRawPerRawProduced,
                        0f,
                        10,
                        1,
                        null,
                        IngredientMaterial.Solid);
                });

            CreateOrUpdate(
                db,
                "nanbei_ludou",
                "南北绿豆",
                "使最终分数倍率增加 0.2。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplier, 0.2f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "ritual",
                "仪式",
                "每关开始时获得 1 个 激励。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.LevelStart);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(
                        RelicEffectType.GrantLinkedRelic,
                        0f,
                        0,
                        0,
                        null,
                        IngredientMaterial.Soft,
                        null,
                        incentive);
                });

            // Keep only seeded relics in database.
            PruneDatabaseToKnown(db);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openWindow)
                RelicManagerWindow.Open();

            Debug.Log("[遗物管理器] 正式遗物已填充，当前数量: " + db.Count);
            return db;
        }

        private static void PruneDatabaseToKnown(RelicDatabase db)
        {
            if (db == null) return;
            var keep = new HashSet<string>
            {
                "vegetarianism", "no_spice_no_joy", "spore_invasion",
                "compound_salt", "big_warehouse", "pure_salt", "ghost_urn", "necronomicon",
                "delicious_powder", "incentive", "kitchen_accident", "fatigue",
                "moss", "handle_moss", "hero_expedition", "strange_scent_stone",
                "cruel_delicious", "stewed_zhizhi", "penguin_blessing", "nanbei_ludou", "ritual"
            };

            var all = new List<RelicItem>(db.Relics);
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i];
                if (item == null || !keep.Contains(item.Id))
                    db.Remove(item);
            }
        }

        private static void DeleteObsolete(RelicDatabase db)
        {
            for (int i = 0; i < ObsoleteRelicIds.Length; i++)
            {
                string id = ObsoleteRelicIds[i];
                string path = $"{RelicFolder}/Relic_{id}.asset";
                var item = AssetDatabase.LoadAssetAtPath<RelicItem>(path);
                if (item != null)
                    db?.Remove(item);
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }
        }

        private static RelicItem CreateOrUpdate(
            RelicDatabase db,
            string id,
            string displayName,
            string description,
            RelicAcquireStage stage,
            System.Action<RelicRule> configureRule)
        {
            return CreateOrUpdateMulti(db, id, displayName, description, stage, configureRule);
        }

        private static RelicItem CreateOrUpdateMulti(
            RelicDatabase db,
            string id,
            string displayName,
            string description,
            RelicAcquireStage stage,
            params System.Action<RelicRule>[] configureRules)
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

            if (configureRules != null)
            {
                for (int i = 0; i < configureRules.Length; i++)
                {
                    if (configureRules[i] == null) continue;
                    var rule = new RelicRule();
                    configureRules[i].Invoke(rule);
                    item.AddRule(rule);
                }
            }

            EditorUtility.SetDirty(item);
            db.Add(item);
            return item;
        }

        private static IngredientItem EnsureMushroomIngredient()
        {
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

            IngredientItem byName = null;
            foreach (var guid in AssetDatabase.FindAssets("t:IngredientItem"))
            {
                var item = AssetDatabase.LoadAssetAtPath<IngredientItem>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;
                if (item.DisplayName == "蘑菇" || item.Id == "mushroom")
                    byName = item;
            }

            if (byName != null)
                return byName;

            string path = $"{IngredientFolder}/Ingredient_mushroom.asset";
            var created = AssetDatabase.LoadAssetAtPath<IngredientItem>(path);
            if (created == null)
            {
                created = ScriptableObject.CreateInstance<IngredientItem>();
                AssetDatabase.CreateAsset(created, path);
            }

            created.SetIdentity("mushroom_relic", "蘑菇");
            created.SetDescription("孢子入侵产出用蘑菇食材。");
            created.SetCategory(IngredientCategory.Vegetable);
            created.SetCoreValues(0, 0, 99, 1f);
            created.SetStat("柔软食材", 2f);
            EditorUtility.SetDirty(created);

            var ingredientDb = AssetDatabase.LoadAssetAtPath<IngredientDatabase>("Assets/Resources/IngredientDatabase.asset");
            if (ingredientDb != null)
            {
                ingredientDb.Add(created);
                EditorUtility.SetDirty(ingredientDb);
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
