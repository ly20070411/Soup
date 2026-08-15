using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Soup.Events.Editor
{
    /// <summary>
    /// Seeds the design-doc event set (事件一览) into Event Manager / EventDatabase.
    /// </summary>
    public static class EventDataSeeder
    {
        private const string DatabasePath = "Assets/Resources/EventDatabase.asset";
        private const string EventFolder = "Assets/Data/Events";

        [MenuItem("Soup/Event Manager/Seed Sample Events")]
        public static void SeedSamplesMenu()
        {
            SeedAll();
            Debug.Log("[EventDataSeeder] 设计文档事件已填充。");
        }

        public static EventDatabase SeedAll()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder(EventFolder);

            var db = AssetDatabase.LoadAssetAtPath<EventDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<EventDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            // —— 一般事件（回合/阶段随机触发）——
            CreateOrUpdate(db, "more_hands", "更多人手？", 0, null,
                "在一次工作后，小精灵全都累的瘫软在地。看着他们精疲力尽的样子，作为组长，你意识到必须做点什么。",
                Option("多召唤一些小精灵", Effect(EventItem.EffectKind.ModifyElves, "", 5, 1f),
                    Effect(EventItem.EffectKind.GrantRelic, "fatigue", 1, 1f)),
                Option("激励他们", Effect(EventItem.EffectKind.GrantRelic, "incentive", 2, 1f)),
                Option("杀鸡儆猴",
                    Effect(EventItem.EffectKind.ModifyElves, "", -3, 1f),
                    Effect(EventItem.EffectKind.GrantRelic, "incentive", 4, 1f)));

            CreateOrUpdate(db, "flavor_salt", "风味盐", 0, null,
                "一对盐矿精灵兄弟找到了你，“尝尝我们的盐吧！”哥哥首先开口，“他们能激发你料理的风味！”弟弟也补充道。",
                Option("砸碎哥哥", Effect(EventItem.EffectKind.GrantRelic, "compound_salt", 1, 1f)),
                Option("砸碎弟弟", Effect(EventItem.EffectKind.GrantRelic, "pure_salt", 1, 1f)));

            CreateOrUpdate(db, "moss_invasion", "！苔藓！", 0, null,
                "苔藓！苔藓！到处都是苔藓！我们的仓库里长满了苔藓！快想想办法！",
                Option("命令小精灵连夜清扫",
                    Effect(EventItem.EffectKind.GrantRelic, "kitchen_accident", 2, 1f),
                    Effect(EventItem.EffectKind.GrantRelic, "handle_moss", 1, 1f)),
                Option("也许可以煮汤",
                    Effect(EventItem.EffectKind.ModifyWarehouse, "", -1000, 1f),
                    Effect(EventItem.EffectKind.GrantRelic, "moss", 1, 1f)));

            CreateOrUpdate(db, "otherworld_hero", "异世界勇者", 0, null,
                "召唤仪式中途，突然一阵雷光闪过，一个潇洒的身影出现在了法阵中间。“我是来拯救这个世界的！”这名自称来自异世界的勇者说道。",
                Option("告诉他拯救世界的方式就是一起做汤",
                    Effect(EventItem.EffectKind.GrantEmployee, "otherworld_hero", 1, 1f)),
                Option("打发他出去探险，记得带战利品回来",
                    Effect(EventItem.EffectKind.GrantRelic, "hero_expedition", 1, 1f)));

            CreateOrUpdate(db, "sniff_stone", "嗦嗨！！！", 0, null,
                "小精灵们不知道从哪弄来了一种散发奇异香味的石头，闻一闻，顿时感觉精神舒畅，工作效率都提高了，但你感觉他们的精神状态好像不太正常",
                Option("挺好的，让他们闻吧", Effect(EventItem.EffectKind.GrantRelic, "strange_scent_stone", 1, 1f)),
                Option("万一有副作用呢，还是让他们别闻了",
                    Effect(EventItem.EffectKind.GrantRelic, "incentive", 1, 1f)));

            CreateOrUpdate(db, "two_monsters", "这是小精灵首次同时对战两只怪兽", 0, null,
                "两只巨大的吱吱闯入了我们的厨房，残忍的吃掉了我们的食材，必须要让他们付出代价。",
                Option("把他们丢到汤里去", Effect(EventItem.EffectKind.GrantRelic, "stewed_zhizhi", 1, 1f)),
                Option("把他们扣押起来，让他们干活",
                    Effect(EventItem.EffectKind.GrantEmployee, "zhizhi", 2, 1f)));

            CreateOrUpdate(db, "haunted", "闹鬼", 0, null,
                "一位小精灵说家里闹鬼了，“昨天我在床上睡觉，然后就听到什么声音，然后，然后，哇——”",
                Option("去捉鬼", Effect(EventItem.EffectKind.GrantEmployee, "ghost", 3, 1f)),
                Option("组建鬼杀队", Effect(EventItem.EffectKind.GrantRelic, "incentive", 2, 1f)));

            // —— 关卡限定事件（第 N 关结束时触发）——
            CreateOrUpdate(db, "mysterious_tomb", "神秘墓穴", 1, null,
                "小精灵在采集途中发现了一座神秘墓穴，我们要深入探索一下吗？",
                Option("在浅层稍微找一找",
                    Effect(EventItem.EffectKind.GrantRelic, "ghost_urn", 1, 1f),
                    Effect(EventItem.EffectKind.GrantEmployee, "ghost", 2, 1f)),
                Option("派点人去深层找一找",
                    Effect(EventItem.EffectKind.ModifyElves, "", -3, 1f),
                    Effect(EventItem.EffectKind.GrantRelic, "necronomicon", 1, 1f)),
                Option("不去了，安全要紧", Effect(EventItem.EffectKind.GrantRelic, "incentive", 1, 1f)));

            CreateOrUpdate(db, "penguin_gathering", "凑企鹅", 2, null,
                "一个长得奇怪脑袋，黑皮白腹的怪异生物出现在了你面前，嘴里一直在叫嚷着奇怪的句子：“咕咕嘎嘎？咕咕嘎嘎？”你突然有了开腔的欲望",
                Option("“咕咕嘎嘎！”", Effect(EventItem.EffectKind.GrantRelic, "penguin_blessing", 1, 1f)),
                Option("“哈基米啵南北绿豆~”", Effect(EventItem.EffectKind.GrantRelic, "nanbei_ludou", 1, 1f)),
                Option("“咔咔——”", Effect(EventItem.EffectKind.GrantRelic, "ritual", 1, 1f)));

            CreateOrUpdate(db, "tastier_soup", "更美味的汤", 2, null,
                "在烹饪时，2只小精灵失足掉进了汤里，直到汤被端上餐桌被才发现，巨人们却认为今天的汤更加好喝",
                Option("安抚小精灵，让他们多注意",
                    Effect(EventItem.EffectKind.ModifyElves, "", -2, 1f),
                    Effect(EventItem.EffectKind.GrantRelic, "incentive", 3, 1f)),
                Option("这也是一种让汤变美味的方法",
                    Effect(EventItem.EffectKind.ModifyElves, "", -2, 1f),
                    Effect(EventItem.EffectKind.GrantRelic, "cruel_delicious", 1, 1f)));

            CreateOrUpdate(db, "world_cauldron_crack", "锅底的裂缝", 3, null,
                "饥肠王揭开城堡地板：灰色裂缝一路通向正在干烧的世界之釜。",
                Option("记录补给路线（仓库上限 +200）",
                    Effect(EventItem.EffectKind.ModifyWarehouse, "", 200, 1f)),
                Option("先把最后一碗分完（无额外效果）"));

            // —— 进阶专属事件（对应岗位解锁后才会出现）——
            CreateOrUpdate(db, "spore_infection", "孢子感染", 0, "mushroom",
                "你发现最近从事蘑菇采集的小精灵变得有一些奇怪，他们的皮肤变蓝，甚至长出了小蘑菇",
                Option("立即开始隔离，优化采集流程",
                    Effect(EventItem.EffectKind.ModifyJobCapacity, "mushroom", -5, 1f),
                    Effect(EventItem.EffectKind.ModifyJobYield, "mushroom", 0, 1.3f)),
                Option("不管，继续采集",
                    Effect(EventItem.EffectKind.ModifyElves, "", -4, 1f),
                    Effect(EventItem.EffectKind.GrantEmployee, "mushroom_person", 3, 1f)),
                Option("开发解药", Effect(EventItem.EffectKind.GrantRelic, "incentive", 1, 1f)));

            CreateOrUpdate(db, "tasty_berry", "好吃的小甜果", 0, "berry",
                "小甜果，好吃，爱吃，当你发现的时候，有几个小精灵甚至因为偷吃的太多，连路都走不动了",
                Option("狠狠的惩罚他们",
                    Effect(EventItem.EffectKind.ModifyJobYield, "berry", 0, 1.3f),
                    Effect(EventItem.EffectKind.GrantRelic, "fatigue", 2, 1f)),
                Option("多吃一点也不怕",
                    Effect(EventItem.EffectKind.ModifyJobYield, "berry", 0, 0.8f),
                    Effect(EventItem.EffectKind.GrantRelic, "incentive", 2, 1f)));

            CreateOrUpdate(db, "ouch", "啊，好痛！", 0, "little_spiky_ball",
                "有个小精灵在小刺球采集场里唱歌，唱的太难听了，小刺球们都抓狂了，开始攻击小精灵",
                Option("把唱歌的人打一顿",
                    Effect(EventItem.EffectKind.ModifyJobYield, "little_spiky_ball", 0, 1.1f)),
                // 设计为二选一各 50%：两次独立判定近似。
                Option("必须让你们见识一下什么是真正的音乐",
                    Effect(EventItem.EffectKind.ModifyElves, "", -3, 1f, 0.5f),
                    Effect(EventItem.EffectKind.ModifyJobYield, "little_spiky_ball", 0, 1.4f, 0.5f)));

            CreateOrUpdate(db, "magic_leaf_event", "神说，要有魔法叶", 0, "magic_leaf",
                "你看到一群小精灵围着魔法叶跳舞，嘴里叽里咕噜不知道说什么，他们肯定是被这该死的叶子蛊惑了。",
                Option("异端！净化！",
                    Effect(EventItem.EffectKind.DisableJob, "magic_leaf", 1, 1f),
                    Effect(EventItem.EffectKind.GrantRelic, "incentive", 5, 1f)),
                // “同时生产四种风味”以每单位附加固定风味近似表达。
                Option("于是就有了魔法叶",
                    Effect(EventItem.EffectKind.ModifyJobFlavor, "magic_leaf", 2, 1f, 1f, "magic"),
                    Effect(EventItem.EffectKind.GrantRelic, "fatigue", 5, 1f)),
                Option("把他们全部打一顿", Effect(EventItem.EffectKind.GrantRelic, "incentive", 1, 1f)));

            CreateOrUpdate(db, "cold_jokes", "冷笑话", 0, "ice_fruit",
                "一名小精灵认为给冰晶果讲冷笑话能促进寒冷风味产出，经过测试，发现他讲的冷笑话确实很冷",
                Option("让他去讲",
                    Effect(EventItem.EffectKind.ModifyJobYield, "ice_fruit", 0, 0.7f),
                    Effect(EventItem.EffectKind.ModifyJobFlavor, "ice_fruit", 10, 1f)),
                Option("呵呵", Effect(EventItem.EffectKind.GrantRelic, "incentive", 1, 1f)));

            db.RebuildIndex();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        private static EventItem.EventEffect Effect(
            EventItem.EffectKind kind,
            string targetId,
            int intAmount,
            float floatAmount,
            float chance = 1f,
            string secondTargetId = null)
        {
            var effect = new EventItem.EventEffect();
            effect.Set(kind, targetId, intAmount, floatAmount, chance, secondTargetId);
            return effect;
        }

        private static EventItem.EventOption Option(
            string text,
            params EventItem.EventEffect[] effects)
        {
            var option = new EventItem.EventOption();
            option.Set(text);
            option.SetEffects(effects);
            return option;
        }

        private static EventItem CreateOrUpdate(
            EventDatabase db,
            string id,
            string displayName,
            int triggerLevelIndex,
            string requiredJobId,
            string description,
            params EventItem.EventOption[] options)
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
            item.SetTrigger(triggerLevelIndex, requiredJobId);
            item.SetOptions(options);

            EditorUtility.SetDirty(item);
            db.EditorAdd(item);
            return item;
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
