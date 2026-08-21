using Soup.Employees;
using Soup.Jobs;
using Soup.Relics;
using Soup.Relics.Editor;
using UnityEditor;
using UnityEngine;

namespace Soup.Events.Editor
{
    /// <summary>
    /// Seeds narrative events from the design event table into Event Manager.
    /// </summary>
    public static class EventDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/EventDatabase.asset";
        private const string EventFolder = "Assets/Data/Events";
        private const string RelicDatabasePath = "Assets/Resources/RelicDatabase.asset";
        private const string RelicFolder = "Assets/Data/Relics";
        private const string EmployeeDatabasePath = "Assets/Resources/EmployeeDatabase.asset";
        private const string EmployeeFolder = "Assets/Data/Employees";

        [MenuItem("Soup/Event Manager/Seed Sample Events")]
        public static void SeedSamplesMenu()
        {
            SeedSamples(openWindow: true);
        }

        public static EventDatabase SeedSamples(bool openWindow = false)
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Data");
            EnsureFolder(EventFolder);
            EnsureFolder(RelicFolder);
            EnsureFolder(EmployeeFolder);

            var db = AssetDatabase.LoadAssetAtPath<EventDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<EventDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            // Ensure full relic definitions (rules + acquire stage) exist first.
            RelicDataSeeder.SeedAll(openWindow: false);

            var blessingPenguin = LoadRelic("penguin_blessing");
            var nanbeiLudou = LoadRelic("nanbei_ludou");
            var ritual = LoadRelic("ritual");
            var fatigue = LoadRelic("fatigue");
            var compoundSalt = LoadRelic("compound_salt");
            var pureSalt = LoadRelic("pure_salt");
            var ghostUrn = LoadRelic("ghost_urn");
            var necronomicon = LoadRelic("necronomicon");
            var handleMoss = LoadRelic("handle_moss");
            var moss = LoadRelic("moss");
            var kitchenAccident = LoadRelic("kitchen_accident");
            var heroExpedition = LoadRelic("hero_expedition");
            var strangeStone = LoadRelic("strange_scent_stone");
            var cruelDelicious = LoadRelic("cruel_delicious");
            var stewedZhizhi = LoadRelic("stewed_zhizhi");
            var abundanceBlessing = LoadRelic("abundance_blessing");
            var incentive = LoadRelic("incentive");

            var ghost = EnsureEmployee("ghost", "幽灵", 0.8f, occupiesJobSlot: false, canPlayerAssign: true, lockedJob: null);
            var mushroomPerson = EnsureEmployee(
                "mushroom_person",
                "蘑菇人",
                1.5f,
                occupiesJobSlot: true,
                canPlayerAssign: false,
                lockedJob: FindJobByDisplayName("蘑菇"));
            var otherworldHero = EnsureEmployee(
                "otherworld_hero",
                "异世界勇者",
                3f,
                occupiesJobSlot: true,
                canPlayerAssign: true,
                lockedJob: null);
            var zhizhi = EnsureEmployee(
                "zhizhi",
                "吱吱",
                2.5f,
                occupiesJobSlot: true,
                canPlayerAssign: true,
                lockedJob: null,
                restrictToJobType: true,
                allowedJobType: JobType.Process,
                consumeOwnProcessedFraction: 0.1f);

            var jobMushroom = FindJobByDisplayName("蘑菇");
            var jobBerry = FindJobByDisplayName("小甜果");
            var jobSpiky = FindJobByDisplayName("小刺球");
            var jobMagicLeaf = FindJobByDisplayName("魔法叶");
            var jobIceFruit = FindJobByDisplayName("冰晶果");

            // —— 一般事件 ——

            CreateOrUpdate(
                db,
                "penguin_gather",
                "凑企鹅",
                "一个长得奇怪脑袋，黑皮白腹的怪异生物出现在了你面前，嘴里一直在叫嚷着奇怪的句子：“咕咕嘎嘎？咕咕嘎嘎？”\n你突然有了开腔的欲望",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 2,
                weight: 1f,
                canRepeat: false,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("“咕咕嘎嘎！”——获得 凑企鹅的祝福");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetGrantRelic(blessingPenguin);
                    opt.AddEffect(e);
                },
                opt =>
                {
                    opt.SetLabel("“哈基米啵南北绿豆~”——获得 南北绿豆");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetGrantRelic(nanbeiLudou);
                    opt.AddEffect(e);
                },
                opt =>
                {
                    opt.SetLabel("“咔咔——”——获得 仪式");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetGrantRelic(ritual);
                    opt.AddEffect(e);
                });

            CreateOrUpdate(
                db,
                "more_hands",
                "更多人手？",
                "在一次工作后，小精灵全都累的瘫软在地。看着他们精疲力尽的样子，作为组长，你意识到必须做点什么。",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("多召唤一些小精灵——获得5个小精灵，1个 疲倦");
                    opt.ClearEffects();
                    var e1 = new EventEffect();
                    e1.Set(EventEffectType.AddElves, 5);
                    opt.AddEffect(e1);
                    var e2 = new EventEffect();
                    e2.SetGrantRelic(fatigue);
                    opt.AddEffect(e2);
                },
                opt =>
                {
                    opt.SetLabel("激励他们——获得2个 族长的激励");
                    opt.ClearEffects();
                    AddGrantRelicStacks(opt, incentive, 2);
                },
                opt =>
                {
                    opt.SetLabel("杀鸡儆猴——小精灵数量减3，获得4个组长的激励");
                    opt.ClearEffects();
                    var e1 = new EventEffect();
                    e1.Set(EventEffectType.AddElves, -3);
                    opt.AddEffect(e1);
                    AddGrantRelicStacks(opt, incentive, 4);
                });

            CreateOrUpdate(
                db,
                "flavor_salt",
                "风味盐",
                "一对盐矿精灵兄弟找到了你，“尝尝我们的盐吧！”哥哥首先开口，“他们能激发你料理的风味！”弟弟也补充道。",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("砸碎哥哥——获得 复合香盐");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetGrantRelic(compoundSalt);
                    opt.AddEffect(e);
                },
                opt =>
                {
                    opt.SetLabel("砸碎弟弟——获得 纯正香盐");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetGrantRelic(pureSalt);
                    opt.AddEffect(e);
                });

            CreateOrUpdate(
                db,
                "mysterious_tomb",
                "神秘墓穴",
                "小精灵在采集途中发现了一座神秘墓穴，我们要深入探索一下吗？",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 1,
                weight: 1f,
                canRepeat: false,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("在浅层稍微找一找——获得 幽灵瓮和2个员工 幽灵");
                    opt.ClearEffects();
                    var e1 = new EventEffect();
                    e1.SetGrantRelic(ghostUrn);
                    opt.AddEffect(e1);
                    var e2 = new EventEffect();
                    e2.SetAddEmployee(ghost, 2);
                    opt.AddEffect(e2);
                },
                opt =>
                {
                    opt.SetLabel("派点人去深层找一找——小精灵减3，获得 死灵书");
                    opt.ClearEffects();
                    var e1 = new EventEffect();
                    e1.Set(EventEffectType.AddElves, -3);
                    opt.AddEffect(e1);
                    var e2 = new EventEffect();
                    e2.SetGrantRelic(necronomicon);
                    opt.AddEffect(e2);
                },
                opt =>
                {
                    opt.SetLabel("不去了，安全要紧——获得1个 激励");
                    opt.ClearEffects();
                    AddGrantRelicStacks(opt, incentive, 1);
                });

            CreateOrUpdate(
                db,
                "moss_everywhere",
                "！苔藓！",
                "苔藓！苔藓！到处都是苔藓！\n我们的仓库里长满了苔藓！快想想办法！",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("命令小精灵连夜清扫——获得2个 厨房事故，1个 处理苔藓");
                    opt.ClearEffects();
                    AddGrantRelicStacks(opt, kitchenAccident, 2);
                    var e3 = new EventEffect();
                    e3.SetGrantRelic(handleMoss);
                    opt.AddEffect(e3);
                },
                opt =>
                {
                    opt.SetLabel("也许可以煮汤——仓库减少1000，获得 苔藓");
                    opt.ClearEffects();
                    var e1 = new EventEffect();
                    e1.SetWarehouseCapacity(-1000);
                    opt.AddEffect(e1);
                    var e2 = new EventEffect();
                    e2.SetGrantRelic(moss);
                    opt.AddEffect(e2);
                });

            CreateOrUpdate(
                db,
                "otherworld_hero",
                "异世界勇者",
                "召唤仪式中途，突然一阵雷光闪过，一个潇洒的身影出现在了法阵中间。“我是来拯救这个世界的！”这名自称来自异世界的勇者说道。",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("告诉他拯救世界的方式就是一起做汤——获得1个员工 异世界勇者");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetAddEmployee(otherworldHero, 1);
                    opt.AddEffect(e);
                },
                opt =>
                {
                    opt.SetLabel("打发他出去探险，记得带战利品回来——获得 勇者出征");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetGrantRelic(heroExpedition);
                    opt.AddEffect(e);
                });

            CreateOrUpdate(
                db,
                "suohai",
                "嗦嗨！！！",
                "小精灵们不知道从哪弄来了一种散发奇异香味的石头，闻一闻，顿时感觉精神舒畅，工作效率都提高了，但你感觉他们的精神状态好像不太正常",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("挺好的，让他们闻吧——获得 异香石");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetGrantRelic(strangeStone);
                    opt.AddEffect(e);
                },
                opt =>
                {
                    opt.SetLabel("万一有副作用呢，还是让他们别闻了——获得1个 激励");
                    opt.ClearEffects();
                    AddGrantRelicStacks(opt, incentive, 1);
                });

            CreateOrUpdate(
                db,
                "tastier_soup",
                "更美味的汤",
                "在烹饪时，2只小精灵失足掉进了汤里，直到汤被端上餐桌被才发现，巨人们却认为今天的汤更加好喝",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 2,
                weight: 1f,
                canRepeat: false,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("安抚小精灵，让他们多注意——小精灵减2，获得3个 激励");
                    opt.ClearEffects();
                    var e1 = new EventEffect();
                    e1.Set(EventEffectType.AddElves, -2);
                    opt.AddEffect(e1);
                    AddGrantRelicStacks(opt, incentive, 3);
                },
                opt =>
                {
                    opt.SetLabel("这也是一种让汤变美味的方法——小精灵减2，获得 残酷的美味");
                    opt.ClearEffects();
                    var e1 = new EventEffect();
                    e1.Set(EventEffectType.AddElves, -2);
                    opt.AddEffect(e1);
                    var e2 = new EventEffect();
                    e2.SetGrantRelic(cruelDelicious);
                    opt.AddEffect(e2);
                });

            CreateOrUpdate(
                db,
                "two_monsters",
                "这是小精灵首次同时对战两只怪兽",
                "两只巨大的吱吱闯入了我们的厨房，残忍的吃掉了我们的食材，必须要让他们付出代价。",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("把他们丢到汤里去——获得 炖煮吱吱");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetGrantRelic(stewedZhizhi);
                    opt.AddEffect(e);
                },
                opt =>
                {
                    opt.SetLabel("把他们扣押起来，让他们干活——获得2个员工 吱吱");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetAddEmployee(zhizhi, 2);
                    opt.AddEffect(e);
                });

            CreateOrUpdate(
                db,
                "haunted",
                "闹鬼",
                "一位小精灵说家里闹鬼了，“昨天我在床上睡觉，然后就听到什么声音，然后，然后，哇——”",
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("去捉鬼——获得3个员工 幽灵");
                    opt.ClearEffects();
                    var e = new EventEffect();
                    e.SetAddEmployee(ghost, 3);
                    opt.AddEffect(e);
                },
                opt =>
                {
                    opt.SetLabel("组建鬼杀队——获得2个 激励");
                    opt.ClearEffects();
                    AddGrantRelicStacks(opt, incentive, 2);
                });

            // —— 进阶专属事件 ——

            CreateOrUpdate(
                db,
                "spore_infection",
                "孢子感染",
                "你发现最近从事蘑菇采集的小精灵变得有一些奇怪，他们的皮肤变蓝，甚至长出了小蘑菇",
                EventCategory.AdvancedExclusive,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: jobMushroom,
                opt =>
                {
                    opt.SetLabel("立即开始隔离，优化采集流程——蘑菇岗位采集上限减五，产量增加30%");
                    opt.ClearEffects();
                    AddModifyJobMaxWorkers(opt, jobMushroom, -5);
                    AddModifyJobYieldBonus(opt, jobMushroom, 0.3f);
                },
                opt =>
                {
                    opt.SetLabel("不管，继续采集——小精灵减4，获得3个员工 蘑菇人");
                    opt.ClearEffects();
                    var e1 = new EventEffect();
                    e1.Set(EventEffectType.AddElves, -4);
                    opt.AddEffect(e1);
                    var e2 = new EventEffect();
                    e2.SetAddEmployee(mushroomPerson, 3);
                    opt.AddEffect(e2);
                },
                opt =>
                {
                    opt.SetLabel("开发解药——获得1个 激励");
                    opt.ClearEffects();
                    AddGrantRelicStacks(opt, incentive, 1);
                });

            CreateOrUpdate(
                db,
                "tasty_berry",
                "好吃的小甜果",
                "小甜果，好吃，爱吃，当你发现的时候，有几个小精灵甚至因为偷吃的太多，连路都走不动了",
                EventCategory.AdvancedExclusive,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: jobBerry,
                opt =>
                {
                    opt.SetLabel("狠狠的惩罚他们——小甜果产量增加30%，获得2个 疲倦");
                    opt.ClearEffects();
                    AddModifyJobYieldBonus(opt, jobBerry, 0.3f);
                    AddGrantRelicStacks(opt, fatigue, 2);
                },
                opt =>
                {
                    opt.SetLabel("多吃一点也不怕——小甜果产量减20%，获得3个 激励");
                    opt.ClearEffects();
                    AddModifyJobYieldBonus(opt, jobBerry, -0.2f);
                    AddGrantRelicStacks(opt, incentive, 3);
                });

            CreateOrUpdate(
                db,
                "ouch_hurt",
                "啊，好痛！",
                "有个小精灵在小刺球采集场里唱歌，唱的太难听了，小刺球们都抓狂了，开始攻击小精灵",
                EventCategory.AdvancedExclusive,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: jobSpiky,
                opt =>
                {
                    opt.SetLabel("把唱歌的人打一顿——小刺球产量加10%");
                    opt.ClearEffects();
                    AddModifyJobYieldBonus(opt, jobSpiky, 0.1f);
                },
                opt =>
                {
                    opt.SetLabel("必须让你们见识一下什么是真正的音乐——50%小精灵数量减3，50%小刺球产量加40%");
                    opt.ClearEffects();
                    AddChanceElfDeltaOrJobYield(opt, jobSpiky, -3, 0.4f);
                });

            CreateOrUpdate(
                db,
                "god_said_magic_leaf",
                "神说，要有魔法叶",
                "你看到一群小精灵围着魔法叶跳舞，嘴里叽里咕噜不知道说什么，他们肯定是被这该死的叶子蛊惑了。",
                EventCategory.AdvancedExclusive,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: jobMagicLeaf,
                opt =>
                {
                    opt.SetLabel("异端！净化！——永久失去魔法叶采集岗位，获得5个 激励");
                    opt.ClearEffects();
                    AddDestroyGatherJob(opt, jobMagicLeaf);
                    AddGrantRelicStacks(opt, incentive, 5);
                },
                opt =>
                {
                    opt.SetLabel("于是就有了魔法叶——魔法叶可以同时生产四种风味，获得5个 疲倦");
                    opt.ClearEffects();
                    AddEnableJobAllFourFlavors(opt, jobMagicLeaf);
                    AddGrantRelicStacks(opt, fatigue, 5);
                },
                opt =>
                {
                    opt.SetLabel("把他们全部打一顿——获得1个 激励");
                    opt.ClearEffects();
                    AddGrantRelicStacks(opt, incentive, 1);
                });

            CreateOrUpdate(
                db,
                "cold_joke",
                "冷笑话",
                "一名小精灵认为给冰晶果讲冷笑话能促进寒冷风味产出，经过测试，发现他讲的冷笑话确实很冷",
                EventCategory.AdvancedExclusive,
                EventTriggerMoment.AfterStage,
                requiredStage: 0,
                weight: 1f,
                canRepeat: true,
                relatedJob: jobIceFruit,
                opt =>
                {
                    opt.SetLabel("让他去讲——冰晶果食材产量减20，风味产量加10点");
                    opt.ClearEffects();
                    AddModifyJobRawAndColdPerUnit(opt, jobIceFruit, -20, 10);
                },
                opt =>
                {
                    opt.SetLabel("呵呵——获得1个 激励");
                    opt.ClearEffects();
                    AddGrantRelicStacks(opt, incentive, 1);
                });

            SeedBlessingGoddess(
                db,
                "blessing_goddess_1",
                "“你就是小精灵的族长吗？很可爱哟！”因为身形丰满，面带微笑的女神出现在你面前，“呐呐~我可以给你们一个祝福哟！”",
                requiredStage: 0,
                abundanceBlessing,
                incentive);

            SeedBlessingGoddess(
                db,
                "blessing_goddess_2",
                "“你好，我是族长……额额额不对，族长你好，我是女神，是来给你祝福的。”因为造型邋遢，睡眼惺忪的女神出现在你面前，“赶紧告诉我你需要的祝福吧，我好回去……休息。”",
                requiredStage: 0,
                abundanceBlessing,
                incentive);

            SeedBlessingGoddess(
                db,
                "blessing_goddess_3",
                "“因为你没有其他神明祝福，才轮到我来的！”一位努着嘴的娇小女神出现在你面前，“才不是看你可怜，专门来给你祝福呢！”",
                requiredStage: 4,
                abundanceBlessing,
                incentive);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (openWindow)
                EventManagerWindow.Open();

            Debug.Log("[事件管理器] 事件表已填充，当前数量: " + db.Count);
            return db;
        }

        private static void SeedBlessingGoddess(
            EventDatabase db,
            string id,
            string description,
            int requiredStage,
            RelicItem abundanceBlessing,
            RelicItem incentive)
        {
            CreateOrUpdate(
                db,
                id,
                "祝福女神",
                description,
                EventCategory.General,
                EventTriggerMoment.AfterStage,
                requiredStage,
                weight: 1f,
                canRepeat: false,
                relatedJob: null,
                opt =>
                {
                    opt.SetLabel("我希望能找到更多采集物——获得 丰饶祝福");
                    opt.ClearEffects();
                    if (abundanceBlessing != null)
                    {
                        var e = new EventEffect();
                        e.SetGrantRelic(abundanceBlessing);
                        opt.AddEffect(e);
                    }
                },
                opt =>
                {
                    opt.SetLabel("我希望手下人干劲满满——消除所有 疲倦，获得一个 激励");
                    opt.ClearEffects();
                    var clear = new EventEffect();
                    clear.SetRemoveAllFatigue();
                    opt.AddEffect(clear);
                    AddGrantRelicStacks(opt, incentive, 1);
                });

            var path = $"{EventFolder}/Event_{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<EventItem>(path);
            if (item != null)
            {
                item.SetExclusionGroup("blessing_goddess");
                EditorUtility.SetDirty(item);
            }
        }

        private static RelicItem LoadRelic(string id)
        {
            string path = $"{RelicFolder}/Relic_{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<RelicItem>(path);
            if (item == null)
                Debug.LogError($"[事件管理器] 缺少遗物资源: {path}，请先执行 Soup/Relic Manager/Seed All Relics");
            return item;
        }

        private static void AddGrantRelicStacks(EventOption opt, RelicItem relic, int stacks)
        {
            if (opt == null || relic == null || stacks <= 0) return;
            for (int i = 0; i < stacks; i++)
            {
                var e = new EventEffect();
                e.SetGrantRelic(relic);
                opt.AddEffect(e);
            }
        }

        private static void AddModifyJobYieldBonus(EventOption opt, JobItem job, float bonus)
        {
            if (opt == null || job == null || Mathf.Approximately(bonus, 0f)) return;
            var e = new EventEffect();
            e.SetModifyJobYieldBonus(job, bonus);
            opt.AddEffect(e);
        }

        private static void AddModifyJobMaxWorkers(EventOption opt, JobItem job, int delta)
        {
            if (opt == null || job == null || delta == 0) return;
            var e = new EventEffect();
            e.SetModifyJobMaxWorkers(job, delta);
            opt.AddEffect(e);
        }

        private static void AddModifyJobRawAndColdPerUnit(EventOption opt, JobItem job, int rawDelta, int coldDelta)
        {
            if (opt == null || job == null) return;
            if (rawDelta == 0 && coldDelta == 0) return;
            var e = new EventEffect();
            e.SetModifyJobRawAndColdPerUnit(job, rawDelta, coldDelta);
            opt.AddEffect(e);
        }

        private static void AddEnableJobAllFourFlavors(EventOption opt, JobItem job)
        {
            if (opt == null || job == null) return;
            var e = new EventEffect();
            e.SetEnableJobAllFourFlavors(job);
            opt.AddEffect(e);
        }

        private static void AddDestroyGatherJob(EventOption opt, JobItem job)
        {
            if (opt == null || job == null) return;
            var e = new EventEffect();
            e.SetDestroyGatherJob(job);
            opt.AddEffect(e);
        }

        private static void AddChanceElfDeltaOrJobYield(EventOption opt, JobItem job, int elfDelta, float yieldBonus)
        {
            if (opt == null || job == null) return;
            var e = new EventEffect();
            e.SetChanceElfDeltaOrJobYield(job, elfDelta, yieldBonus);
            opt.AddEffect(e);
        }

        private static RelicItem EnsureRelic(string id, string displayName)
        {
            // Prefer fully seeded relics; fall back to stub only if missing.
            var existing = LoadRelic(id);
            if (existing != null)
                return existing;

            EnsureFolder(RelicFolder);
            EnsureFolder("Assets/Resources");

            string path = $"{RelicFolder}/Relic_{id}.asset";
            var item = ScriptableObject.CreateInstance<RelicItem>();
            AssetDatabase.CreateAsset(item, path);

            item.SetIdentity(id, displayName);
            item.SetDescription($"事件奖励：{displayName}");
            item.SetAcquireStage(RelicAcquireStage.Event);
            EditorUtility.SetDirty(item);

            var db = AssetDatabase.LoadAssetAtPath<RelicDatabase>(RelicDatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<RelicDatabase>();
                AssetDatabase.CreateAsset(db, RelicDatabasePath);
            }

            db.Add(item);
            EditorUtility.SetDirty(db);
            return item;
        }

        private static EmployeeItem EnsureEmployee(
            string id,
            string displayName,
            float efficiency,
            bool occupiesJobSlot,
            bool canPlayerAssign,
            JobItem lockedJob,
            bool restrictToJobType = false,
            JobType allowedJobType = JobType.Process,
            float consumeOwnProcessedFraction = 0f)
        {
            EnsureFolder(EmployeeFolder);
            EnsureFolder("Assets/Resources");

            string path = $"{EmployeeFolder}/Employee_{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<EmployeeItem>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<EmployeeItem>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.SetIdentity(id, displayName);
            item.SetDescription($"事件相关员工：{displayName}");
            item.SetWorkEfficiency(efficiency);
            item.SetOccupiesJobSlot(occupiesJobSlot);
            item.SetCanPlayerAssign(canPlayerAssign);
            item.SetLockedJob(lockedJob);
            item.SetRestrictToJobType(restrictToJobType, allowedJobType);
            item.SetConsumeOwnProcessedFraction(consumeOwnProcessedFraction);
            if (id == "mushroom_person")
                item.SetDescription("蘑菇人会占用蘑菇岗位人口，一直生产蘑菇，玩家无法变更岗位。");
            if (id == "zhizhi")
                item.SetDescription("只能用于处理工作，会吃掉自身产出处理食材的10%。");
            if (id == "ghost")
                item.SetDescription("不占用工作岗位。");
            if (id == "otherworld_hero")
                item.SetDescription("无。");
            EditorUtility.SetDirty(item);

            var db = AssetDatabase.LoadAssetAtPath<EmployeeDatabase>(EmployeeDatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<EmployeeDatabase>();
                AssetDatabase.CreateAsset(db, EmployeeDatabasePath);
            }

            db.Add(item);
            EditorUtility.SetDirty(db);
            return item;
        }

        private static JobItem FindJobByDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return null;
            var guids = AssetDatabase.FindAssets("t:JobItem");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var job = AssetDatabase.LoadAssetAtPath<JobItem>(path);
                if (job != null && job.DisplayName == displayName)
                    return job;
            }

            Debug.LogWarning($"[事件管理器] 未找到岗位「{displayName}」，进阶专属事件将缺少 relatedJob。");
            return null;
        }

        private static void CreateOrUpdate(
            EventDatabase db,
            string id,
            string displayName,
            string description,
            EventCategory category,
            EventTriggerMoment trigger,
            int requiredStage,
            float weight,
            bool canRepeat,
            JobItem relatedJob,
            params System.Action<EventOption>[] optionBuilders)
        {
            string path = $"{EventFolder}/Event_{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<EventItem>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<EventItem>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.SetIdentity(id, displayName);
            item.SetDescription(description);
            item.SetCategory(category);
            item.SetTriggerMoment(trigger);
            item.SetRequiredStageIndex(requiredStage);
            item.SetWeight(weight);
            item.SetCanRepeat(canRepeat);
            item.SetRelatedJob(relatedJob);
            item.SetExclusionGroup(string.Empty);
            item.ClearOptions();

            if (optionBuilders != null)
            {
                for (int i = 0; i < optionBuilders.Length; i++)
                {
                    var option = new EventOption();
                    optionBuilders[i]?.Invoke(option);
                    item.AddOption(option);
                }
            }

            EditorUtility.SetDirty(item);
            db.Add(item);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
