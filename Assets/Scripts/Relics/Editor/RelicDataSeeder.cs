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
                "全局工作效率加0.1，可重复获得。",
                RelicAcquireStage.Event,
                allowMultiple: true,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddGlobalLaborEfficiency, 0.1f, 0, 0, null);
                });

            // —— 原开局，现商店 ——
            CreateOrUpdate(
                db,
                "vegetarianism",
                "素食主义",
                "若没有肉类食材，最终倍率加0.4。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterScore);
                    rule.SetCondition(RelicConditionType.NoCategoryGathered, IngredientCategory.Meat, 0);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplier, 0.4f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "no_spice_no_joy",
                "无辣不欢",
                "热辣提供的倍数变为1.5倍。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.BeforeSpicy);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    // float 0.5 → 结算里 spicyMult *= 1.5（即变为 1.5 倍）
                    rule.SetEffect(RelicEffectType.AddSpicyScoreMultiplier, 0.5f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "spore_invasion",
                "孢子入侵",
                "每采集5个采集物，就会产出1份蘑菇食材。",
                RelicAcquireStage.Shop,
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
                "每有一种风味，最终倍率增加0.2。",
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
                "仓库存储增加4000。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ModifyWarehouseCapacity, 0f, 0, 4000, null);
                });

            CreateOrUpdate(
                db,
                "clear_stock",
                "清库存",
                "当空闲仓库量少于仓库总量一半时，处理岗位效率加50%。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.WarehouseSpaceBelowHalf, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddProcessLaborEfficiency, 0.5f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "grave_blast",
                "坟冢爆射",
                "获得5个 幽灵。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(
                        RelicEffectType.GrantEmployee,
                        0f,
                        0,
                        5,
                        null,
                        IngredientMaterial.Soft,
                        EmployeeManager.GhostId);
                });

            CreateOrUpdate(
                db,
                "high_heat_reduce",
                "大火收汁",
                "后5个回合，烹饪岗位效率加50%。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.LastNLevelTurns, IngredientCategory.Other, 5);
                    rule.SetEffect(RelicEffectType.AddCookLaborEfficiency, 0.5f, 0, 0, null);
                });

            CreateOrUpdateMulti(
                db,
                "oil_on_fire",
                "火上浇油",
                "烹饪岗位效率加75%，但会造成20%的浪费。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddCookLaborEfficiency, 0.75f, 0, 0, null);
                },
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddCookOutputWasteFraction, 0.2f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "love_tuotuo",
                "我爱坨坨",
                "把已有的所有采集岗全部变成快乐坨坨采集岗，进阶分支继承原岗位。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ConvertAllGatherToHappyTuotuo, 0f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "bonfire_party",
                "篝火晚会",
                "所有员工都参与烹饪时，烹饪岗位效率加100%。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.AllEmployeesOnCook, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddCookLaborEfficiency, 1f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "elf_crowd",
                "一群小精灵",
                "获得6个小精灵。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ModifyElfCount, 0f, 0, 6, null);
                });

            CreateOrUpdate(
                db,
                "construction_crew",
                "施工队",
                "三个环节都获得一次额外进阶机会。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddAdvanceChargesAllZones, 0f, 0, 1, null);
                });

            CreateOrUpdate(
                db,
                "blender",
                "搅拌机",
                "回合结束时消耗3/4的强韧和坚固食材，生成等量的柔软食材。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.TurnEnd);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ConvertToughSolidFractionToSoft, 0.75f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "three_question_buttons",
                "三个问号按钮",
                "立刻获得3个事件，获得时直接进入事件。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.OnAcquire);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.PresentBonusStageEvents, 0f, 0, 3, null);
                });

            CreateOrUpdate(
                db,
                "sublimation",
                "升华",
                "关卡结束时，将3个小精灵转换成4个幽灵。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.LevelEnd);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ConvertElvesToGhosts, 0f, 3, 4, null);
                });

            CreateOrUpdate(
                db,
                "overcrowded",
                "人满为患",
                "所有岗位人口上限加5。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddAllJobMaxWorkers, 0f, 0, 5, null);
                });

            CreateOrUpdate(
                db,
                "pure_salt",
                "纯正香盐",
                "在风味小于等于1种时，最终倍率增加0.5。",
                RelicAcquireStage.Event,
                rule =>
                {
                    // 回合初判定风味种类，避免鲜美/寒冷结算后数量变少而误触发。
                    rule.SetTrigger(RelicTrigger.TurnStart);
                    rule.SetCondition(RelicConditionType.HasFlavorCountAtMost, IngredientCategory.Other, 1);
                    rule.SetEffect(RelicEffectType.AddFinalMultiplier, 0.5f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "ghost_urn",
                "幽灵瓮",
                "幽灵的工作效率加0.2。",
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
                "最终倍率加0.1，可重复获得。",
                RelicAcquireStage.Event,
                allowMultiple: true,
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
                "最终倍率减0.1，可重复获得。",
                RelicAcquireStage.Event,
                allowMultiple: true,
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
                "全局工作效率减0.1，可重复获得。",
                RelicAcquireStage.Event,
                allowMultiple: true,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddGlobalLaborEfficiency, -0.1f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "abundance_blessing",
                "丰饶祝福",
                "每种采集物的产出份数加1。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddGatherAmountPerWorker, 0f, 0, 1, null);
                });

            CreateOrUpdate(
                db,
                "recycler",
                "回收器",
                "浪费食材变为增加双倍食材。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ConvertWasteToEqualGain, 0f, 0, 2, null);
                });

            CreateOrUpdate(
                db,
                "moss",
                "苔藓",
                "回合开始时，获得上一回合未使用仓库数量8%的柔软食材。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.TurnStart);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.GrantSoftFromUnusedWarehousePercent, 0.08f, 0, 0, null);
                });

            CreateOrUpdate(
                db,
                "handle_moss",
                "处理苔藓",
                "每采集1个采集物，获得1个柔软食材。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterGather);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(
                        RelicEffectType.GrantRawPerGather,
                        0f,
                        1,
                        1,
                        null,
                        IngredientMaterial.Soft);
                });

            CreateOrUpdate(
                db,
                "hero_expedition",
                "勇者出征",
                "回合开始时，获得随机一种未处理食材450份。",
                RelicAcquireStage.Event,
                true,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.TurnStart);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ChanceGrantRandomRaw, 1f, 0, 450, null);
                });

            CreateOrUpdateMulti(
                db,
                "strange_scent_stone",
                "异香石",
                "前5个回合全局工作效率加0.5，后5个回合全局工作效率减0.5。",
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
                "关卡开始时，小精灵数量减2，最终分数倍率乘以1.3（独立乘区）。",
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
                "开局获得3000处理食材。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.LevelStart);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddProcessed, 0f, 0, 3000, null);
                });

            CreateOrUpdate(
                db,
                "penguin_blessing",
                "凑企鹅的祝福",
                "每生产5个坚固食材，额外获得1个坚固食材。",
                RelicAcquireStage.Event,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.AfterGather);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(
                        RelicEffectType.GrantRawPerRawProduced,
                        0f,
                        5,
                        1,
                        null,
                        IngredientMaterial.Solid);
                });

            CreateOrUpdate(
                db,
                "nanbei_ludou",
                "南北绿豆",
                "使最终分数倍率增加0.2。",
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
                "每关开始时获得1个 激励。",
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

            // —— 表格新增（获取方式未定，先入库供调试 / 商店扩展）——
            CreateOrUpdate(
                db,
                "ice_point",
                "冰点",
                "寒冷处理食材后额外加2分。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.AddColdScorePerUnit, 0f, 0, 2, null);
                });

            CreateOrUpdate(
                db,
                "tech_and_hardcore",
                "科技与狠活",
                "每回合鲜美消耗比例降低20%（等效为每回合消耗总量的30%）。",
                RelicAcquireStage.Shop,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.ReduceMagicConsumePercent, 0.2f, 0, 0, null);
                });

            CreateOrUpdateMulti(
                db,
                "sour_candy",
                "酸酸糖",
                "酸涩换算分数最高档的阈值从10%变为30%，第二档的阈值从50%变成70%。",
                RelicAcquireStage.Shop,
                false,
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.OverrideSourTopTierPercent, 0f, 30, 0, null);
                },
                rule =>
                {
                    rule.SetTrigger(RelicTrigger.Passive);
                    rule.SetCondition(RelicConditionType.Always, IngredientCategory.Other, 0);
                    rule.SetEffect(RelicEffectType.OverrideSourSecondTierPercent, 0f, 70, 0, null);
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
                "compound_salt", "big_warehouse", "clear_stock", "grave_blast", "high_heat_reduce", "oil_on_fire", "love_tuotuo", "bonfire_party", "elf_crowd", "construction_crew", "blender", "three_question_buttons", "sublimation", "overcrowded", "pure_salt", "ghost_urn", "necronomicon",
                "delicious_powder", "incentive", "kitchen_accident", "fatigue",
                "abundance_blessing", "recycler",
                "moss", "handle_moss", "hero_expedition", "strange_scent_stone",
                "cruel_delicious", "stewed_zhizhi", "penguin_blessing", "nanbei_ludou", "ritual",
                "ice_point", "tech_and_hardcore", "sour_candy"
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
            return CreateOrUpdateMulti(db, id, displayName, description, stage, false, configureRule);
        }

        private static RelicItem CreateOrUpdate(
            RelicDatabase db,
            string id,
            string displayName,
            string description,
            RelicAcquireStage stage,
            bool allowMultiple,
            System.Action<RelicRule> configureRule)
        {
            return CreateOrUpdateMulti(db, id, displayName, description, stage, allowMultiple, configureRule);
        }

        private static RelicItem CreateOrUpdateMulti(
            RelicDatabase db,
            string id,
            string displayName,
            string description,
            RelicAcquireStage stage,
            params System.Action<RelicRule>[] configureRules)
        {
            return CreateOrUpdateMulti(db, id, displayName, description, stage, false, configureRules);
        }

        private static RelicItem CreateOrUpdateMulti(
            RelicDatabase db,
            string id,
            string displayName,
            string description,
            RelicAcquireStage stage,
            bool allowMultiple,
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
            item.SetAllowMultiple(allowMultiple);
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
