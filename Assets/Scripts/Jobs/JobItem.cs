using System.Collections.Generic;
using System.Text;
using Soup.Items;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// Single job/station definition: gather, process, or cook rules.
    /// </summary>
    [CreateAssetMenu(fileName = "Job_", menuName = "Soup/Jobs/Job", order = 0)]
    public class JobItem : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "New Job";
        [TextArea(2, 5)]
        [SerializeField] private string description = string.Empty;

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color tint = Color.white;

        [Header("Classification")]
        [SerializeField] private JobType jobType = JobType.Gather;

        [Header("Capacity")]
        [Tooltip("Base max workers for this station. 0 = unlimited (e.g. cooking).")]
        [SerializeField, Min(0)] private int maxWorkers;

        [Header("Advancement Tree")]
        [Tooltip("一阶路径 1")]
        [SerializeField] private JobAdvanceNode path1 = new JobAdvanceNode();
        [Tooltip("一阶路径 2")]
        [SerializeField] private JobAdvanceNode path2 = new JobAdvanceNode();
        [Tooltip("二阶：在路径 1 上选 1-1")]
        [SerializeField] private JobAdvanceNode path1_1 = new JobAdvanceNode();
        [Tooltip("二阶：在路径 1 上选 1-2")]
        [SerializeField] private JobAdvanceNode path1_2 = new JobAdvanceNode();
        [Tooltip("二阶：在路径 2 上选 2-1")]
        [SerializeField] private JobAdvanceNode path2_1 = new JobAdvanceNode();
        [Tooltip("二阶：在路径 2 上选 2-2")]
        [SerializeField] private JobAdvanceNode path2_2 = new JobAdvanceNode();

        [Header("Gather")]
        [SerializeField, Min(0)] private int gatherAmountPerWorker = 1;
        [SerializeField] private IngredientItem outputIngredient;
        [Tooltip("Material granted per gathered unit (e.g. mushroom → Soft ×2).")]
        [SerializeField] private IngredientMaterial gatherMaterial = IngredientMaterial.Soft;
        [SerializeField, Min(0)] private int materialPerGatherUnit = 1;
        [SerializeField, Min(0)] private int spicyPerGatherUnit;
        [SerializeField, Min(0)] private int sourPerGatherUnit;
        [SerializeField, Min(0)] private int coldPerGatherUnit;
        [SerializeField, Min(0)] private int magicPerGatherUnit;

        [Header("Process")]
        [SerializeField, Min(0)] private int processAmountPerWorker = 10;
        [SerializeField] private IngredientMaterial preferredMaterial = IngredientMaterial.Soft;
        [SerializeField, Range(0f, 1f)] private float otherMaterialEfficiency = 0.5f;
        [Tooltip("If true, randomly process any materials (e.g. Explosion).")]
        [SerializeField] private bool processRandom;
        [Tooltip("处理结算优先级：数值越大越先结算。爆炸应为最低（0），刀切/电锯/钻头更高。")]
        [SerializeField] private int processPriority = 100;

        [Header("Cook")]
        [SerializeField, Min(0)] private int cookAmountPerWorker = 10;
        [SerializeField, Min(0f)] private float scoreMultiplier = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Tint => tint;
        public JobType JobType => jobType;
        public int MaxWorkers => maxWorkers;
        public bool HasWorkerLimit => maxWorkers > 0;
        public int DesignedMaxUpgradeLevel => JobProgressionRules.MaxUpgradesPerJob(jobType);

        public JobAdvanceNode Path1 => path1;
        public JobAdvanceNode Path2 => path2;
        public JobAdvanceNode Path1_1 => path1_1;
        public JobAdvanceNode Path1_2 => path1_2;
        public JobAdvanceNode Path2_1 => path2_1;
        public JobAdvanceNode Path2_2 => path2_2;

        public int GatherAmountPerWorker => gatherAmountPerWorker;
        public IngredientItem OutputIngredient => outputIngredient;
        public IngredientMaterial GatherMaterial => gatherMaterial;
        public int MaterialPerGatherUnit => materialPerGatherUnit;
        public int SpicyPerGatherUnit => spicyPerGatherUnit;
        public int SourPerGatherUnit => sourPerGatherUnit;
        public int ColdPerGatherUnit => coldPerGatherUnit;
        public int MagicPerGatherUnit => magicPerGatherUnit;

        public int ProcessAmountPerWorker => processAmountPerWorker;
        public IngredientMaterial PreferredMaterial => preferredMaterial;
        public float OtherMaterialEfficiency => otherMaterialEfficiency;
        public bool ProcessRandom => processRandom;
        public int ProcessPriority => processPriority;

        public int CookAmountPerWorker => cookAmountPerWorker;
        public float ScoreMultiplier => scoreMultiplier;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "New Job" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetIcon(Sprite value) => icon = value;

        public void SetTint(Color value) => tint = value;

        public void SetJobType(JobType value) => jobType = value;

        public void SetMaxWorkers(int value) => maxWorkers = Mathf.Max(0, value);

        public JobAdvanceNode GetAdvanceNode(JobAdvanceNodeId nodeId)
        {
            switch (nodeId)
            {
                case JobAdvanceNodeId.Path1: return path1;
                case JobAdvanceNodeId.Path2: return path2;
                case JobAdvanceNodeId.Path1_1: return path1_1;
                case JobAdvanceNodeId.Path1_2: return path1_2;
                case JobAdvanceNodeId.Path2_1: return path2_1;
                case JobAdvanceNodeId.Path2_2: return path2_2;
                default: return null;
            }
        }

        /// <summary>沿已选路径累计人口加成。</summary>
        public int GetWorkersBonusForPath(JobAdvanceNodeId path)
        {
            if (path == JobAdvanceNodeId.None) return 0;

            int bonus = 0;
            var chain = new List<JobAdvanceNodeId>(JobAdvancePath.MaxDepth);
            JobAdvancePath.GetChain(path, chain);
            for (int i = 0; i < chain.Count; i++)
            {
                var node = GetAdvanceNode(chain[i]);
                if (node != null)
                    bonus += node.MaxWorkersBonus;
            }

            return bonus;
        }

        /// <summary>沿路径取最深非零的绝对人口上限；0 = 不覆盖。</summary>
        public int GetWorkersOverrideForPath(JobAdvanceNodeId path)
        {
            if (path == JobAdvanceNodeId.None) return 0;

            int overrideCap = 0;
            var chain = new List<JobAdvanceNodeId>(JobAdvancePath.MaxDepth);
            JobAdvancePath.GetChain(path, chain);
            for (int i = 0; i < chain.Count; i++)
            {
                var node = GetAdvanceNode(chain[i]);
                if (node != null && node.MaxWorkersOverride > 0)
                    overrideCap = node.MaxWorkersOverride;
            }

            return overrideCap;
        }

        public int GetEffectiveMaxWorkers(JobAdvanceNodeId path)
        {
            if (!HasWorkerLimit) return 0;
            int overrideCap = GetWorkersOverrideForPath(path);
            if (overrideCap > 0) return overrideCap;
            return maxWorkers + GetWorkersBonusForPath(path);
        }

        /// <summary>兼容旧调用：按进阶深度估算（不区分分支时仅作上限参考）。</summary>
        public int GetEffectiveMaxWorkers(int upgradeLevel)
        {
            if (!HasWorkerLimit) return 0;
            if (upgradeLevel <= 0) return maxWorkers;

            // 无具体路径时，用默认人口增量估算。
            int perStep = JobProgressionRules.UsesPopulationCap(jobType)
                ? JobProgressionRules.DefaultUpgradeWorkerBonus
                : 0;
            int depth = Mathf.Min(upgradeLevel, DesignedMaxUpgradeLevel);
            return maxWorkers + perStep * depth;
        }

        public string GetUpgradeSummary()
        {
            EnsureAdvanceTreeDefaults();
            var sb = new StringBuilder();
            AppendNodeSummary(sb, JobAdvanceNodeId.Path1, path1);
            AppendNodeSummary(sb, JobAdvanceNodeId.Path2, path2);
            AppendNodeSummary(sb, JobAdvanceNodeId.Path1_1, path1_1);
            AppendNodeSummary(sb, JobAdvanceNodeId.Path1_2, path1_2);
            AppendNodeSummary(sb, JobAdvanceNodeId.Path2_1, path2_1);
            AppendNodeSummary(sb, JobAdvanceNodeId.Path2_2, path2_2);
            return sb.Length > 0 ? sb.ToString() : "无进阶";
        }

        public string BuildTreeDiagram(JobAdvanceNodeId current)
        {
            EnsureAdvanceTreeDefaults();
            var sb = new StringBuilder();
            sb.AppendLine("进阶树：");
            sb.AppendLine(FormatBranchLine(JobAdvanceNodeId.Path1, path1, current, "├─"));
            sb.AppendLine(FormatBranchLine(JobAdvanceNodeId.Path1_1, path1_1, current, "│  ├─"));
            sb.AppendLine(FormatBranchLine(JobAdvanceNodeId.Path1_2, path1_2, current, "│  └─"));
            sb.AppendLine(FormatBranchLine(JobAdvanceNodeId.Path2, path2, current, "└─"));
            sb.AppendLine(FormatBranchLine(JobAdvanceNodeId.Path2_1, path2_1, current, "   ├─"));
            sb.Append(FormatBranchLine(JobAdvanceNodeId.Path2_2, path2_2, current, "   └─"));
            return sb.ToString();
        }

        public void EnsureAdvanceTreeDefaults()
        {
            path1 ??= new JobAdvanceNode();
            path2 ??= new JobAdvanceNode();
            path1_1 ??= new JobAdvanceNode();
            path1_2 ??= new JobAdvanceNode();
            path2_1 ??= new JobAdvanceNode();
            path2_2 ??= new JobAdvanceNode();
        }

        public void SeedDefaultAdvanceTree()
        {
            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            int pop = JobProgressionRules.UsesPopulationCap(jobType)
                ? JobProgressionRules.DefaultUpgradeWorkerBonus
                : 0;

            path1.SetMaxWorkersBonus(pop);
            path2.SetMaxWorkersBonus(pop);
            path1_1.SetMaxWorkersBonus(pop);
            path1_2.SetMaxWorkersBonus(pop);
            path2_1.SetMaxWorkersBonus(pop);
            path2_2.SetMaxWorkersBonus(pop);

            path1.SetDisplayName("路径 1");
            path2.SetDisplayName("路径 2");
            path1_1.SetDisplayName("路径 1-1");
            path1_2.SetDisplayName("路径 1-2");
            path2_1.SetDisplayName("路径 2-1");
            path2_2.SetDisplayName("路径 2-2");
        }

        /// <summary>
        /// 小火进阶：1 分数倍率 2.2；其余无。
        /// </summary>
        public void SeedLowHeatAdvanceTree()
        {
            SeedDefaultAdvanceTree();
            path1.SetDisplayName("提味");
            path1.SetScoreMultiplierOverride(2.2f);
            path1.SetEffectDescription("分数倍率 2.2");
            path2.SetDisplayName("无");
            path2.SetEffectDescription("无");
            path1_1.SetDisplayName("无");
            path1_1.SetEffectDescription("无");
            path1_2.SetDisplayName("无");
            path1_2.SetEffectDescription("无");
            path2_1.SetDisplayName("无");
            path2_1.SetEffectDescription("无");
            path2_2.SetDisplayName("无");
            path2_2.SetEffectDescription("无");
        }

        /// <summary>
        /// 中火进阶：1 烹饪 480 份、分数倍率 1.2；其余无。
        /// </summary>
        public void SeedMediumHeatAdvanceTree()
        {
            SeedDefaultAdvanceTree();
            path1.SetDisplayName("加量");
            path1.SetCookAmountOverride(480);
            path1.SetScoreMultiplierOverride(1.2f);
            path1.SetEffectDescription("烹饪 480 份食材，分数倍率 1.2");
            path2.SetDisplayName("无");
            path2.SetEffectDescription("无");
            path1_1.SetDisplayName("无");
            path1_1.SetEffectDescription("无");
            path1_2.SetDisplayName("无");
            path1_2.SetEffectDescription("无");
            path2_1.SetDisplayName("无");
            path2_1.SetEffectDescription("无");
            path2_2.SetDisplayName("无");
            path2_2.SetEffectDescription("无");
        }

        /// <summary>
        /// 大火进阶：1 烹饪 800 份；其余无。
        /// </summary>
        public void SeedHighHeatAdvanceTree()
        {
            SeedDefaultAdvanceTree();
            path1.SetDisplayName("加量");
            path1.SetCookAmountOverride(800);
            path1.SetEffectDescription("烹饪 800 份食材");
            path2.SetDisplayName("无");
            path2.SetEffectDescription("无");
            path1_1.SetDisplayName("无");
            path1_1.SetEffectDescription("无");
            path1_2.SetDisplayName("无");
            path1_2.SetEffectDescription("无");
            path2_1.SetDisplayName("无");
            path2_1.SetEffectDescription("无");
            path2_2.SetDisplayName("无");
            path2_2.SetEffectDescription("无");
        }

        /// <summary>
        /// 蘑菇岗设计进阶树（衍生物由进阶产出，不单独设岗）。
        /// 1：产出10份 → 1-1 蘑菇人×5 / 1-2 产出15份
        /// 2：50%变异蘑菇 → 2-1 50%肥大 / 2-2 50%奇异
        /// </summary>
        public void SeedMushroomAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;
            var mutant = ResolveIngredient("mutant_mushroom", "变异蘑菇");
            var fat = ResolveIngredient("fat_mushroom", "肥大蘑菇");
            var strange = ResolveIngredient("strange_mushroom", "奇异蘑菇");

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("增产");
            path1.SetMaxWorkersBonus(pop);
            path1.SetGatherAmountOverride(10);
            path1.SetEffectDescription("产出 10 份");

            path2.SetDisplayName("变异");
            path2.SetMaxWorkersBonus(pop);
            path2.SetVariant(mutant, 0.5f);
            path2.SetEffectDescription("全局产出蘑菇时有 50% 的概率产出变异蘑菇");

            path1_1.SetDisplayName("蘑菇人");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGrantEmployee("mushroom_person", 5);
            path1_1.SetEffectDescription("获得 5 个蘑菇人（见员工一览）");

            path1_2.SetDisplayName("高产");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetGatherAmountOverride(15);
            path1_2.SetEffectDescription("产出 15 份");

            path2_1.SetDisplayName("肥大");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetVariant(fat, 0.5f);
            path2_1.SetEffectDescription("全局产出蘑菇时有 50% 的概率产出肥大蘑菇");

            path2_2.SetDisplayName("奇异");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetVariant(strange, 0.5f);
            path2_2.SetEffectDescription("全局产出蘑菇时有 50% 的概率产出奇异蘑菇");
        }

        private static IngredientItem ResolveIngredient(string id, string displayName)
        {
            if (IngredientManager.Instance != null)
            {
                var fromMgr = IngredientManager.Instance.GetById(id)
                              ?? IngredientManager.Instance.FindByName(displayName);
                if (fromMgr != null) return fromMgr;
            }

#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:IngredientItem"))
            {
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<IngredientItem>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;
                if (string.Equals(item.Id, id, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.DisplayName, displayName, System.StringComparison.Ordinal))
                    return item;
            }
#endif
            return null;
        }

        /// <summary>
        /// 小甜果岗设计进阶树。
        /// 基础产出 5 份；1：每份+15 柔软 → 1-1 产出8 / 1-2 产出30但每份-20柔软；
        /// 2：每员+8%效率 → 2-1 每员+15% / 2-2 每员+8%且满员时每份+30柔软。
        /// </summary>
        public void SeedBerryAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("柔软加料");
            path1.SetMaxWorkersBonus(pop);
            path1.SetSoftPerUnitBonus(15);
            path1.SetEffectDescription("每份小甜果额外产出 15 份柔软食材");

            path2.SetDisplayName("效率");
            path2.SetMaxWorkersBonus(pop);
            path2.SetEfficiencyPerWorker(0.08f);
            path2.SetEffectDescription("每个正在采集小甜果的员工都会使得小甜果的采集效率增加 8%");

            path1_1.SetDisplayName("增产");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGatherAmountOverride(8);
            path1_1.SetEffectDescription("产出 8 份");

            path1_2.SetDisplayName("超量");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetGatherAmountOverride(30);
            path1_2.SetSoftPerUnitBonus(-20);
            path1_2.SetEffectDescription("产出 30 份，但每份小甜果少产出 20 份柔软食材");

            path2_1.SetDisplayName("高效");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetEfficiencyPerWorker(0.15f);
            path2_1.SetEffectDescription("每个正在采集小甜果的员工都会使得小甜果的采集效率增加 15%");

            path2_2.SetDisplayName("满员加料");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetEfficiencyPerWorker(0.08f);
            path2_2.SetSoftPerUnitWhenFull(30);
            path2_2.SetEffectDescription(
                "每个正在采集小甜果的员工都会使得小甜果的采集效率增加 8%，当岗位满员时，每份小甜果额外产出 30 份柔软食材");
        }

        /// <summary>
        /// 冰晶果岗设计进阶树。
        /// 基础产出 2 份；1：产出3 → 1-1 产出5 / 1-2 寒冷分+0.5倍；
        /// 2：每份+10寒冷 → 2-1 每份-20坚固+20寒冷 / 2-2 其他采集每份+2寒冷。
        /// </summary>
        public void SeedIceFruitAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("增产");
            path1.SetMaxWorkersBonus(pop);
            path1.SetGatherAmountOverride(3);
            path1.SetEffectDescription("产出 3 份");

            path2.SetDisplayName("寒冷");
            path2.SetMaxWorkersBonus(pop);
            path2.SetColdPerUnitBonus(10);
            path2.SetEffectDescription("额外产出 10 份寒冷");

            path1_1.SetDisplayName("高产");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGatherAmountOverride(5);
            path1_1.SetEffectDescription("产出 5 份");

            path1_2.SetDisplayName("寒分");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetColdScoreMultiplierBonus(0.5f);
            path1_2.SetEffectDescription("寒冷所提供的分数额外加 0.5 倍");

            path2_1.SetDisplayName("极寒");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetSolidPerUnitBonus(-20);
            path2_1.SetColdPerUnitBonus(20);
            path2_1.SetEffectDescription("每份冰晶果少产出 20 份坚固食材，额外产出 20 份寒冷");

            path2_2.SetDisplayName("寒息");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetOtherGatherColdPerUnit(2);
            path2_2.SetEffectDescription("其他采集物每份额外产出 2 份寒冷");
        }

        /// <summary>
        /// 爆辣果岗设计进阶树（对称于冰晶果）。
        /// 基础产出 2 份；1：产出3 → 1-1 产出5 / 1-2 热辣分+0.5倍；
        /// 2：每份+10热辣 → 2-1 每份-20强韧+20热辣 / 2-2 其他采集每份+2热辣。
        /// </summary>
        public void SeedHotFruitAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("增产");
            path1.SetMaxWorkersBonus(pop);
            path1.SetGatherAmountOverride(3);
            path1.SetEffectDescription("产出 3 份");

            path2.SetDisplayName("热辣");
            path2.SetMaxWorkersBonus(pop);
            path2.SetSpicyPerUnitBonus(10);
            path2.SetEffectDescription("额外产出 10 份热辣");

            path1_1.SetDisplayName("高产");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGatherAmountOverride(5);
            path1_1.SetEffectDescription("产出 5 份");

            path1_2.SetDisplayName("辣分");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetSpicyScoreMultiplierBonus(0.5f);
            path1_2.SetEffectDescription("爆辣所提供的分数额外加 0.5 倍");

            path2_1.SetDisplayName("爆辣");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetToughPerUnitBonus(-20);
            path2_1.SetSpicyPerUnitBonus(20);
            path2_1.SetEffectDescription("每份爆辣果少产出 20 份强韧食材，额外产出 20 份热辣");

            path2_2.SetDisplayName("辣息");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetOtherGatherSpicyPerUnit(2);
            path2_2.SetEffectDescription("其他采集物每份额外产出 2 份热辣");
        }

        /// <summary>
        /// 青酸果岗设计进阶树（对称于冰晶果 / 爆辣果）。
        /// 基础产出 2 份；1：产出3 → 1-1 产出5 / 1-2 酸涩分+0.5倍；
        /// 2：每份+10酸涩 → 2-1 每份-20柔软+20酸涩 / 2-2 其他采集每份+2酸涩。
        /// </summary>
        public void SeedSourFruitAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("增产");
            path1.SetMaxWorkersBonus(pop);
            path1.SetGatherAmountOverride(3);
            path1.SetEffectDescription("产出 3 份");

            path2.SetDisplayName("酸涩");
            path2.SetMaxWorkersBonus(pop);
            path2.SetSourPerUnitBonus(10);
            path2.SetEffectDescription("额外产出 10 份酸涩");

            path1_1.SetDisplayName("高产");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGatherAmountOverride(5);
            path1_1.SetEffectDescription("产出 5 份");

            path1_2.SetDisplayName("酸分");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetSourScoreMultiplierBonus(0.5f);
            path1_2.SetEffectDescription("酸涩所提供的分数额外加 0.5 倍");

            path2_1.SetDisplayName("浓酸");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetSoftPerUnitBonus(-20);
            path2_1.SetSourPerUnitBonus(20);
            path2_1.SetEffectDescription("每份青酸果少产出 20 份柔软食材，额外产出 20 份酸涩");

            path2_2.SetDisplayName("酸息");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetOtherGatherSourPerUnit(2);
            path2_2.SetEffectDescription("其他采集物每份额外产出 2 份酸涩");
        }

        /// <summary>
        /// 魔法叶岗设计进阶树。
        /// 基础产出 2 份；1：产出3 → 1-1 产出5 / 1-2 风味分+0.2倍；
        /// 2：每份+10随机风味 → 2-1 每份-20柔软+20随机风味 / 2-2 其他采集每份+2随机风味。
        /// </summary>
        public void SeedMagicLeafAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("增产");
            path1.SetMaxWorkersBonus(pop);
            path1.SetGatherAmountOverride(3);
            path1.SetEffectDescription("产出 3 份");

            path2.SetDisplayName("风味");
            path2.SetMaxWorkersBonus(pop);
            path2.SetRandomFlavorPerUnitBonus(10);
            path2.SetEffectDescription("额外产出 10 份随机风味");

            path1_1.SetDisplayName("高产");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGatherAmountOverride(5);
            path1_1.SetEffectDescription("产出 5 份");

            path1_2.SetDisplayName("风分");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetFlavorScoreMultiplierBonus(0.2f);
            path1_2.SetEffectDescription("风味所提供的分数额外加 0.2 倍");

            path2_1.SetDisplayName("浓香");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetSoftPerUnitBonus(-20);
            path2_1.SetRandomFlavorPerUnitBonus(20);
            path2_1.SetEffectDescription("每份魔法叶少产出 20 份柔软食材，额外产出 20 份随机风味");

            path2_2.SetDisplayName("香息");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetOtherGatherRandomFlavorPerUnit(2);
            path2_2.SetEffectDescription("其他采集物每份额外产出 2 份随机风味");
        }

        /// <summary>
        /// 灯芯草岗设计进阶树。
        /// 基础产出 3 份；1：+10柔软+5强韧且通关精灵永久+1 → 1-1 获员工送激励 / 1-2 通关精灵再+2；
        /// 2：每员全采集+8%上限40% → 2-1 每员+10%上限50% / 2-2 指定岗每员+20%上限100%。
        /// </summary>
        public void SeedLampwickGrassAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("加料");
            path1.SetMaxWorkersBonus(pop);
            path1.SetFlatSoftBonus(10);
            path1.SetFlatToughBonus(5);
            path1.SetPermanentElfBonus(1);
            path1.SetEffectDescription("额外产出 10 份柔软食材和 5 份强韧食材，关卡结束后获得小精灵数量永久加 1");

            path2.SetDisplayName("全效");
            path2.SetMaxWorkersBonus(pop);
            path2.SetAllGatherEfficiency(0.08f, 0.4f);
            path2.SetEffectDescription("每个正在采集灯芯草的员工为所有采集岗位提供 8% 的采集效率加成，上限 40%");

            path1_1.SetDisplayName("激励");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetIncentivePerEmployeeGained(1);
            path1_1.SetEffectDescription("每获得 1 次员工，额外获得 1 个激励");

            path1_2.SetDisplayName("扩编");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetPermanentElfBonus(2);
            path1_2.SetEffectDescription("关卡结束后获得小精灵数量永久再加 2");

            path2_1.SetDisplayName("高效");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetAllGatherEfficiency(0.1f, 0.5f);
            path2_1.SetEffectDescription("每个正在采集灯芯草的员工为所有采集岗位提供 10% 的采集效率加成，上限 50%");

            path2_2.SetDisplayName("专援");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetDesignatedGatherEfficiency(0.2f, 1f);
            path2_2.SetEffectDescription("每个正在采集灯芯草的员工为一个指定采集岗位提供 20% 的采集效率加成，上限 100%");
        }

        /// <summary>
        /// 小白花岗设计进阶树。
        /// 基础产出 5 份；1：最多风味+4 → 1-1 风味+4且最多食材+20 / 1-2 风味+8；
        /// 2：摧毁另一采集岗且产出×3 → 2-1 再摧毁且×6 / 2-2 再摧毁且被毁岗每回合产 10 小白花。
        /// </summary>
        public void SeedLittleWhiteFlowerAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("风味潮");
            path1.SetMaxWorkersBonus(pop);
            path1.SetTopFlavorBonus(4);
            path1.SetEffectDescription("额外产出 4 份当前数量最多的风味（数量相同会先随机产出 4 份）");

            path2.SetDisplayName("献祭");
            path2.SetMaxWorkersBonus(pop);
            path2.SetDestroyOtherGatherOnTake(true);
            path2.SetGatherAmountMultiplier(3f);
            path2.SetEffectDescription("随机摧毁另一个采集岗位，小白花产出变为三倍（无其他采集岗位时无法选择）");

            path1_1.SetDisplayName("风味与料");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetTopFlavorBonus(4);
            path1_1.SetTopMaterialBonus(20);
            path1_1.SetEffectDescription(
                "额外产出 4 份当前数量最多的风味（数量相同会先随机产出 4 份）和 20 份当前数量最多的食材（数量相同会先随机产出 20 份）");

            path1_2.SetDisplayName("浓风味");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetTopFlavorBonus(8);
            path1_2.SetEffectDescription("额外产出 8 份当前数量最多的风味（数量相同会先随机产出 8 份）");

            path2_1.SetDisplayName("再献祭");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetDestroyOtherGatherOnTake(true);
            path2_1.SetGatherAmountMultiplier(6f);
            path2_1.SetEffectDescription("再随机摧毁另一个采集岗位，小白花产出变为六倍（无其他采集岗位时无法选择）");

            path2_2.SetDisplayName("白花坟场");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetDestroyOtherGatherOnTake(true);
            path2_2.SetDestroyedJobsOutputPerTurn(10);
            path2_2.SetEffectDescription("再随机摧毁另一个采集岗位，所有已被摧毁的采集岗位每回合产出 10 份小白花");
        }

        /// <summary>
        /// 甜团团岗设计进阶树。
        /// 基础产出 3 份；1：柔软/强韧/坚固各+10 → 1-1 最多未处理+90 / 1-2 产出5；
        /// 2：上限永久5且产1大团球 → 2-1 上限5且产2大团球 / 2-2 上限1且产1巨团山。
        /// </summary>
        public void SeedSweetBunAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;
            var bigBall = ResolveIngredient("big_ball", "大团球");
            var giantMountain = ResolveIngredient("giant_mountain", "巨团山");

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("三料");
            path1.SetMaxWorkersBonus(pop);
            path1.SetFlatSoftBonus(10);
            path1.SetFlatToughBonus(10);
            path1.SetFlatSolidBonus(10);
            path1.SetEffectDescription("额外产出柔软食材、强韧食材、坚固食材各 10 份");

            path2.SetDisplayName("大团球");
            path2.SetMaxWorkersBonus(0);
            path2.SetMaxWorkersOverride(5);
            path2.SetSuppressRawMaterialOutput(true);
            path2.SetBonusIngredient(bigBall, 1);
            path2.SetEffectDescription("岗位上限永久为 5，不再生产甜团团，改为生产 1 份大团球");

            path1_1.SetDisplayName("堆料");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetTopMaterialBonus(90);
            path1_1.SetEffectDescription("额外产出 90 份当前未处理食材中数量最多的一种");

            path1_2.SetDisplayName("增产");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetGatherAmountOverride(5);
            path1_2.SetEffectDescription("产出 5 份");

            path2_1.SetDisplayName("双团球");
            path2_1.SetMaxWorkersBonus(0);
            path2_1.SetMaxWorkersOverride(5);
            path2_1.SetSuppressRawMaterialOutput(true);
            path2_1.SetBonusIngredient(bigBall, 2);
            path2_1.SetEffectDescription("岗位上限永久为 5，不再生产甜团团，改为生产 2 份大团球");

            path2_2.SetDisplayName("巨团山");
            path2_2.SetMaxWorkersBonus(0);
            path2_2.SetMaxWorkersOverride(1);
            path2_2.SetSuppressRawMaterialOutput(true);
            path2_2.SetBonusIngredient(giantMountain, 1);
            path2_2.SetEffectDescription("岗位上限永久为 1，不再生产甜团团，改为生产 1 份巨团山");
        }

        /// <summary>
        /// 大角兽岗设计进阶树。
        /// 基础产出 1 份；1：+30柔软+100坚固 → 1-1 处理岗每员+10%上限50% / 1-2 烹饪岗每员+10%上限50%；
        /// 2：产出2 → 2-1 产出4且其他采集-25% / 2-2 产出4且烹饪后浪费25%未处理食材。
        /// </summary>
        public void SeedBigHornBeastAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("厚料");
            path1.SetMaxWorkersBonus(pop);
            path1.SetFlatSoftBonus(30);
            path1.SetFlatSolidBonus(100);
            path1.SetEffectDescription("额外产出 30 份柔软食材和 100 份坚固食材");

            path2.SetDisplayName("增产");
            path2.SetMaxWorkersBonus(pop);
            path2.SetGatherAmountOverride(2);
            path2.SetEffectDescription("产出 2 份");

            path1_1.SetDisplayName("援处理");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetAllProcessEfficiency(0.1f, 0.5f);
            path1_1.SetEffectDescription("每个采集大角兽的员工为所有处理岗位提供 10% 的效率加成，上限 50%");

            path1_2.SetDisplayName("援烹饪");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetAllCookEfficiency(0.1f, 0.5f);
            path1_2.SetEffectDescription("每个采集大角兽的员工为所有烹饪岗位提供 10% 的效率加成，上限 50%");

            path2_1.SetDisplayName("独采");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetGatherAmountOverride(4);
            path2_1.SetOtherGatherOutputPenalty(0.25f);
            path2_1.SetEffectDescription("产出 4 份，但其他采集岗位产量减少 25%");

            path2_2.SetDisplayName("暴食");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetGatherAmountOverride(4);
            path2_2.SetEndTurnRawWasteFraction(0.25f);
            path2_2.SetEffectDescription("产出 4 份，但回合结束烹饪完毕时会摧毁（浪费）仓库中 25% 的未处理食材");
        }

        /// <summary>
        /// 黏爬爬岗设计进阶树。
        /// 基础产出 4 份；1：最多风味+5（平手随机 4 份）→ 1-1 风味+4且最多食材+25 / 1-2 风味+10；
        /// 2：每份耗 20 柔软或坚固→+40 强韧 → 2-1 同耗再+8 最多风味 / 2-2 耗 30→+100 强韧。
        /// </summary>
        public void SeedNianPapaAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("风味潮");
            path1.SetMaxWorkersBonus(pop);
            path1.SetTopFlavorBonus(5, 4);
            path1.SetEffectDescription("额外产出 5 份当前数量最多的风味（数量相同会先随机产出 4 份）");

            path2.SetDisplayName("炼韧");
            path2.SetMaxWorkersBonus(pop);
            path2.SetConvertSoftOrSolidToTough(20, 40);
            path2.SetEffectDescription("产出每份黏爬爬时会消耗 20 份柔软或者坚固食材，额外生成 40 份强韧食材；若无法消耗则不会额外生成");

            path1_1.SetDisplayName("风味与料");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetTopFlavorBonus(4);
            path1_1.SetTopMaterialBonus(25);
            path1_1.SetEffectDescription(
                "额外产出 4 份当前数量最多的风味（数量相同会先随机产出 4 份）和 25 份当前数量最多的食材（数量相同会先随机产出 25 份）");

            path1_2.SetDisplayName("浓风味");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetTopFlavorBonus(10);
            path1_2.SetEffectDescription("额外产出 10 份当前数量最多的风味（数量相同会先随机产出 10 份）");

            path2_1.SetDisplayName("炼韧加味");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetConvertSoftOrSolidToTough(20, 40, 8);
            path2_1.SetEffectDescription(
                "产出每份黏爬爬时会消耗 20 份柔软或者坚固食材，额外生成 40 份强韧食材和 8 份数量最多的风味；若无法消耗则不会额外生成");

            path2_2.SetDisplayName("大炼韧");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetConvertSoftOrSolidToTough(30, 100);
            path2_2.SetEffectDescription(
                "产出每份黏爬爬时会消耗 30 份柔软或者坚固食材，额外生成 100 份强韧食材；若无法消耗则不会额外生成");
        }

        /// <summary>
        /// 小刺球岗设计进阶树。
        /// 基础产出 15 份；1：产出25且仅强韧 → 1-1 产出40且仅坚固 / 1-2 随机食材+8；
        /// 2：随机食材+10 → 2-1 随机+20且效率-100%由员工每员+20%最多+100% / 2-2 随机+20且下回合效率-100%。
        /// </summary>
        public void SeedLittleSpikyBallAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("纯韧");
            path1.SetMaxWorkersBonus(pop);
            path1.SetGatherAmountOverride(25);
            path1.SetForceOutputMaterial(IngredientMaterial.Tough);
            path1.SetEffectDescription("产出 25 份，只会生产强韧食材");

            path2.SetDisplayName("乱料");
            path2.SetMaxWorkersBonus(pop);
            path2.SetFlatRandomMaterialBonus(10);
            path2.SetEffectDescription("额外产出 10 份随机食材");

            path1_1.SetDisplayName("纯固");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGatherAmountOverride(40);
            path1_1.SetForceOutputMaterial(IngredientMaterial.Solid);
            path1_1.SetEffectDescription("产出 40 份，只会生产坚固食材");

            path1_2.SetDisplayName("少乱料");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetFlatRandomMaterialBonus(8);
            path1_2.SetEffectDescription("额外产出 8 份随机食材");

            path2_1.SetDisplayName("压榨");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetFlatRandomMaterialBonus(20);
            path2_1.SetGatherEfficiencyDebt(1f, 0.2f, 1f);
            path2_1.SetEffectDescription(
                "额外产出 20 份随机食材，但初始生产效率减 100，每个员工使效率加 20%，通过这种方式最多加 100%");

            path2_2.SetDisplayName("透支");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetFlatRandomMaterialBonus(20);
            path2_2.SetNextTurnGatherEfficiencyPenalty(1f);
            path2_2.SetEffectDescription("额外产出 20 份随机食材，但生产后下一回合初始生产效率减 100%");
        }

        /// <summary>
        /// 小银鱼岗设计进阶树（对称于冰晶果，风味为鲜美）。
        /// 基础产出 2 份；1：产出3 → 1-1 产出5 / 1-2 鲜美分+0.5倍；
        /// 2：每份+10鲜美 → 2-1 每份-20柔软+20鲜美 / 2-2 其他采集每份+2鲜美。
        /// </summary>
        public void SeedLittleSilverFishAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("增产");
            path1.SetMaxWorkersBonus(pop);
            path1.SetGatherAmountOverride(3);
            path1.SetEffectDescription("产出 3 份");

            path2.SetDisplayName("鲜美");
            path2.SetMaxWorkersBonus(pop);
            path2.SetMagicPerUnitBonus(10);
            path2.SetEffectDescription("额外产出 10 份鲜美");

            path1_1.SetDisplayName("高产");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGatherAmountOverride(5);
            path1_1.SetEffectDescription("产出 5 份");

            path1_2.SetDisplayName("鲜分");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetMagicScoreMultiplierBonus(0.5f);
            path1_2.SetEffectDescription("鲜美所提供的分数额外加 0.5 倍");

            path2_1.SetDisplayName("浓鲜");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetSoftPerUnitBonus(-20);
            path2_1.SetMagicPerUnitBonus(20);
            path2_1.SetEffectDescription("每份小银鱼少产出 20 份柔软食材，额外产出 20 份鲜美");

            path2_2.SetDisplayName("鲜息");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetOtherGatherMagicPerUnit(2);
            path2_2.SetEffectDescription("其他采集物每份额外产出 2 份鲜美");
        }

        /// <summary>
        /// 快乐坨坨岗设计进阶树。
        /// 基础产出 5 份；1：激励效果+5%/疲惫-10%每员 → 1-1 激励+10% / 1-2 同激励且回合结束每员10%产激励（每关≤3）；
        /// 2：全局效率+2.5%每员 → 2-1 +5% / 2-2 +7.5%上限60%。
        /// </summary>
        public void SeedHappyTuotuoAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("鼓舞");
            path1.SetMaxWorkersBonus(pop);
            path1.SetIncentiveFatigueAura(0.05f, 0.1f);
            path1.SetEffectDescription("每个采集快乐坨坨的员工都会使得激励的效果增加 5%，疲惫的效果减少 10%");

            path2.SetDisplayName("全效");
            path2.SetMaxWorkersBonus(pop);
            path2.SetAllGlobalLaborEfficiency(0.025f);
            path2.SetEffectDescription("每个采集快乐坨坨的员工都会使得全局的工作效率增加 2.5%");

            path1_1.SetDisplayName("大鼓舞");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetIncentiveFatigueAura(0.1f, 0.1f);
            path1_1.SetEffectDescription("每个采集快乐坨坨的员工都会使得激励的效果增加 10%，疲惫的效果减少 10%");

            path1_2.SetDisplayName("产激励");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetIncentiveFatigueAura(0.05f, 0.1f);
            path1_2.SetEndTurnIncentiveChance(0.1f, 3);
            path1_2.SetEffectDescription(
                "每个采集快乐坨坨的员工都会使得激励的效果增加 5%，疲惫的效果减少 10%。在回合结束时，每个采集快乐坨坨的员工有 10% 的概率生产一个激励，每关上限 3 个");

            path2_1.SetDisplayName("高效");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetAllGlobalLaborEfficiency(0.05f);
            path2_1.SetEffectDescription("每个采集快乐坨坨的员工都会使得全局的工作效率增加 5%");

            path2_2.SetDisplayName("顶效");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetAllGlobalLaborEfficiency(0.075f, 0.6f);
            path2_2.SetEffectDescription("每个采集快乐坨坨的员工都会使得全局的工作效率增加 7.5%，上限 60%");
        }

        /// <summary>
        /// 双尾蛇岗设计进阶树。
        /// 基础产出 2 份；1：产出3 → 1-1 产出5 / 1-2 不产食材改产20寒冷+20热辣；
        /// 2：指定岗与自己风味+50% → 2-1 指定岗与自己产量+50% / 2-2 指定岗与自己风味+100%。
        /// </summary>
        public void SeedDoubleTailSnakeAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("增产");
            path1.SetMaxWorkersBonus(pop);
            path1.SetGatherAmountOverride(3);
            path1.SetEffectDescription("产出 3 份");

            path2.SetDisplayName("双味");
            path2.SetMaxWorkersBonus(pop);
            path2.SetDesignatedPairFlavorYieldBonus(0.5f);
            path2.SetEffectDescription("指定一个采集岗位，使它和自己的风味产量额外加 50%");

            path1_1.SetDisplayName("高产");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetGatherAmountOverride(5);
            path1_1.SetEffectDescription("产出 5 份");

            path1_2.SetDisplayName("寒辣");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetSuppressRawMaterialOutput(true);
            path1_2.SetFlatColdBonus(20);
            path1_2.SetFlatSpicyBonus(20);
            path1_2.SetEffectDescription("不生产食材，改为生产 20 份寒冷和 20 份热辣");

            path2_1.SetDisplayName("双产");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetDesignatedPairAllYieldBonus(0.5f);
            path2_1.SetEffectDescription("指定一个采集岗位，使它和自己的产量额外加 50%");

            path2_2.SetDisplayName("浓双味");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetDesignatedPairFlavorYieldBonus(1f);
            path2_2.SetEffectDescription("指定一个采集岗位，使它和自己的风味产量额外加 100%");
        }

        /// <summary>
        /// 棍棍虫岗设计进阶树。
        /// 基础产出 12 份；1：每 300 未使用仓库 +1 坚固 → 1-1 每 300 空位 +2 坚固 / 1-2 每 300 容量 +1 坚固；
        /// 2：每 200 仓库坚固 +1 坚固 → 2-1 每 200 坚固 +2 / 2-2 每 200 坚固 +1 且每 200 处理食材采集 +1 份。
        /// </summary>
        public void SeedStickBugAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("空仓");
            path1.SetMaxWorkersBonus(pop);
            path1.SetSolidPerUnusedWarehouse(300, 1);
            path1.SetEffectDescription("每有 300 份未使用仓库，额外生产 1 份坚固食材");

            path2.SetDisplayName("囤固");
            path2.SetMaxWorkersBonus(pop);
            path2.SetSolidPerWarehouseSolid(200, 1);
            path2.SetEffectDescription("仓库中每有 200 份坚固食材，额外生产 1 份坚固食材");

            path1_1.SetDisplayName("宽仓");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetSolidPerUnusedWarehouse(300, 2);
            path1_1.SetEffectDescription("每有 300 份未使用仓库，额外生产 2 份坚固食材");

            path1_2.SetDisplayName("总容");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetSolidPerWarehouseCapacity(300, 1);
            path1_2.SetEffectDescription("每有 300 份仓库容量，额外生产 1 份坚固食材");

            path2_1.SetDisplayName("厚囤");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetSolidPerWarehouseSolid(200, 2);
            path2_1.SetEffectDescription("仓库中每有 200 份坚固食材，额外生产 2 份坚固食材");

            path2_2.SetDisplayName("固料双计");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetSolidPerWarehouseSolid(200, 1);
            path2_2.SetGatherUnitsPerProcessed(200, 1);
            path2_2.SetEffectDescription(
                "仓库中每有 200 份坚固食材，额外生产 1 份坚固食材；每有 200 份处理食材，采集时额外产出 1 份");
        }

        /// <summary>
        /// 刀切岗设计进阶树。
        /// 基础：优先 120 柔软，其他效率 50%；1：240 / 25% → 1-1 360 / 25% / 1-2 无；
        /// 2：180 / 50% → 2-1 240 / 50% 且每处理 10 份任意食材生成 1 处理 / 2-2 无。
        /// </summary>
        public void SeedKnifeCutAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("加量");
            path1.SetMaxWorkersBonus(pop);
            path1.SetProcessAmountOverride(240);
            path1.SetOtherMaterialEfficiencyOverride(0.25f);
            path1.SetEffectDescription("优先处理 240 份柔软食材，其他食材效率变为 25%");

            path2.SetDisplayName("稳量");
            path2.SetMaxWorkersBonus(pop);
            path2.SetProcessAmountOverride(180);
            path2.SetEffectDescription("优先处理 180 份柔软食材，其他食材效率减半");

            path1_1.SetDisplayName("超量");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetProcessAmountOverride(360);
            path1_1.SetOtherMaterialEfficiencyOverride(0.25f);
            path1_1.SetEffectDescription("优先处理 360 份柔软食材，其他食材效率变为 25%");

            path1_2.SetDisplayName("无");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetEffectDescription("无");

            path2_1.SetDisplayName("回软");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetProcessAmountOverride(240);
            path2_1.SetProcessedRefundPerProcessed(10, 1);
            path2_1.SetEffectDescription(
                "优先处理 240 份柔软食材，其他食材效率减半，每处理 10 份任意食材生成 1 份处理食材");

            path2_2.SetDisplayName("无");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetEffectDescription("无");
        }

        /// <summary>
        /// 电锯岗设计进阶树（结构同刀切，优先材质为强韧）。
        /// 基础：优先 120 强韧，其他效率 50%；1：240 / 25% → 1-1 360 / 25% / 1-2 无；
        /// 2：180 / 50% → 2-1 240 / 50% 且每处理 10 份任意食材生成 1 处理 / 2-2 无。
        /// </summary>
        public void SeedChainsawAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("加量");
            path1.SetMaxWorkersBonus(pop);
            path1.SetProcessAmountOverride(240);
            path1.SetOtherMaterialEfficiencyOverride(0.25f);
            path1.SetEffectDescription("优先处理 240 份强韧食材，其他食材效率变为 25%");

            path2.SetDisplayName("稳量");
            path2.SetMaxWorkersBonus(pop);
            path2.SetProcessAmountOverride(180);
            path2.SetEffectDescription("优先处理 180 份强韧食材，其他食材效率减半");

            path1_1.SetDisplayName("超量");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetProcessAmountOverride(360);
            path1_1.SetOtherMaterialEfficiencyOverride(0.25f);
            path1_1.SetEffectDescription("优先处理 360 份强韧食材，其他食材效率变为 25%");

            path1_2.SetDisplayName("无");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetEffectDescription("无");

            path2_1.SetDisplayName("回韧");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetProcessAmountOverride(240);
            path2_1.SetProcessedRefundPerProcessed(10, 1);
            path2_1.SetEffectDescription(
                "优先处理 240 份强韧食材，其他食材效率减半，每处理 10 份任意食材生成 1 份处理食材");

            path2_2.SetDisplayName("无");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetEffectDescription("无");
        }

        /// <summary>
        /// 钻头岗设计进阶树（结构同刀切/电锯，优先材质为坚固）。
        /// 基础：优先 120 坚固，其他效率 50%；1：240 / 25% → 1-1 360 / 25% / 1-2 无；
        /// 2：180 / 50% → 2-1 240 / 50% 且每处理 10 份任意食材生成 1 处理 / 2-2 无。
        /// </summary>
        public void SeedDrillAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("加量");
            path1.SetMaxWorkersBonus(pop);
            path1.SetProcessAmountOverride(240);
            path1.SetOtherMaterialEfficiencyOverride(0.25f);
            path1.SetEffectDescription("优先处理 240 份坚固食材，其他食材效率变为 25%");

            path2.SetDisplayName("稳量");
            path2.SetMaxWorkersBonus(pop);
            path2.SetProcessAmountOverride(180);
            path2.SetEffectDescription("优先处理 180 份坚固食材，其他食材效率减半");

            path1_1.SetDisplayName("超量");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetProcessAmountOverride(360);
            path1_1.SetOtherMaterialEfficiencyOverride(0.25f);
            path1_1.SetEffectDescription("优先处理 360 份坚固食材，其他食材效率变为 25%");

            path1_2.SetDisplayName("无");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetEffectDescription("无");

            path2_1.SetDisplayName("回固");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetProcessAmountOverride(240);
            path2_1.SetProcessedRefundPerProcessed(10, 1);
            path2_1.SetEffectDescription(
                "优先处理 240 份坚固食材，其他食材效率减半，每处理 10 份任意食材生成 1 份处理食材");

            path2_2.SetDisplayName("无");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetEffectDescription("无");
        }

        /// <summary>
        /// 爆炸岗设计进阶树。
        /// 基础：随机处理 100；1：200 → 1-1 240 / 1-2 500 且处理产出损耗 10%；
        /// 2 / 2-1 / 2-2：无。结算优先级最低，专精岗先吃料。
        /// </summary>
        public void SeedExplosionAdvanceTree()
        {
            int pop = JobProgressionRules.DefaultUpgradeWorkerBonus;

            path1 = new JobAdvanceNode();
            path2 = new JobAdvanceNode();
            path1_1 = new JobAdvanceNode();
            path1_2 = new JobAdvanceNode();
            path2_1 = new JobAdvanceNode();
            path2_2 = new JobAdvanceNode();

            path1.SetDisplayName("扩量");
            path1.SetMaxWorkersBonus(pop);
            path1.SetProcessAmountOverride(200);
            path1.SetEffectDescription("随机处理任意 200 份食材，优先处理其他岗位难以处理的食材");

            path2.SetDisplayName("无");
            path2.SetMaxWorkersBonus(pop);
            path2.SetEffectDescription("无");

            path1_1.SetDisplayName("巨量");
            path1_1.SetMaxWorkersBonus(pop);
            path1_1.SetProcessAmountOverride(240);
            path1_1.SetEffectDescription("随机处理任意 240 份食材，优先处理其他岗位难以处理的食材");

            path1_2.SetDisplayName("爆量");
            path1_2.SetMaxWorkersBonus(pop);
            path1_2.SetProcessAmountOverride(500);
            path1_2.SetProcessedOutputWasteFraction(0.1f);
            path1_2.SetEffectDescription(
                "随机处理任意 500 份食材，优先处理其他岗位难以处理的食材，会损耗 10% 的处理食材");

            path2_1.SetDisplayName("无");
            path2_1.SetMaxWorkersBonus(pop);
            path2_1.SetEffectDescription("无");

            path2_2.SetDisplayName("无");
            path2_2.SetMaxWorkersBonus(pop);
            path2_2.SetEffectDescription("无");
        }

        /// <summary>兼容旧 seeder / 编辑器按钮名称。</summary>
        public void SeedDefaultUpgradeTiers() => SeedDefaultAdvanceTree();

        public void SetGather(int amountPerWorker, IngredientItem ingredient)
        {
            jobType = JobType.Gather;
            gatherAmountPerWorker = Mathf.Max(0, amountPerWorker);
            outputIngredient = ingredient;
        }

        public void SetGatherConversion(
            IngredientMaterial material,
            int materialPerUnit,
            int spicy = 0,
            int sour = 0,
            int cold = 0,
            int magic = 0)
        {
            gatherMaterial = material == IngredientMaterial.Any ? IngredientMaterial.Soft : material;
            materialPerGatherUnit = Mathf.Max(0, materialPerUnit);
            spicyPerGatherUnit = Mathf.Max(0, spicy);
            sourPerGatherUnit = Mathf.Max(0, sour);
            coldPerGatherUnit = Mathf.Max(0, cold);
            magicPerGatherUnit = Mathf.Max(0, magic);
        }

        public void SetProcess(
            int amountPerWorker,
            IngredientMaterial preferred,
            float otherEfficiency = 0.5f,
            bool random = false,
            int priority = 100)
        {
            jobType = JobType.Process;
            processAmountPerWorker = Mathf.Max(0, amountPerWorker);
            preferredMaterial = preferred;
            otherMaterialEfficiency = Mathf.Clamp01(otherEfficiency);
            processRandom = random;
            processPriority = priority;
        }

        public void SetProcessPriority(int priority) => processPriority = priority;

        public void SetCook(int amountPerWorker, float multiplier)
        {
            jobType = JobType.Cook;
            cookAmountPerWorker = Mathf.Max(0, amountPerWorker);
            scoreMultiplier = Mathf.Max(0f, multiplier);
            maxWorkers = 0;
        }

        public string GetEffectSummary()
        {
            switch (jobType)
            {
                case JobType.Gather:
                {
                    var ingredientName = outputIngredient != null ? outputIngredient.DisplayName : "采集物";
                    if (outputIngredient != null)
                    {
                        var total = IngredientYieldResolver.FromIngredient(outputIngredient, gatherAmountPerWorker);
                        return $"每精灵产出 {gatherAmountPerWorker} 份{ingredientName} → {total.ToSummary()}";
                    }
                    return $"每精灵产出 {gatherAmountPerWorker} 份{ingredientName} → {materialPerGatherUnit}×{MaterialLabel(gatherMaterial)}";
                }
                case JobType.Process:
                    if (processRandom || preferredMaterial == IngredientMaterial.Any)
                        return $"处理任意 {processAmountPerWorker} 份食材（结算优先级最低 {processPriority}）";
                    return $"优先处理 {processAmountPerWorker} 份{MaterialLabel(preferredMaterial)}食材，其他材质效率 {otherMaterialEfficiency:0.##}（优先级 {processPriority}）";
                case JobType.Cook:
                    return $"烹饪 {cookAmountPerWorker} 份处理食材，分数倍率 {scoreMultiplier:0.##}";
                default:
                    return description;
            }
        }

        public static string MaterialLabel(IngredientMaterial material)
        {
            switch (material)
            {
                case IngredientMaterial.Soft: return "柔软";
                case IngredientMaterial.Tough: return "强韧";
                case IngredientMaterial.Solid: return "坚固";
                default: return "任意";
            }
        }

        public static string JobTypeLabel(JobType type)
        {
            switch (type)
            {
                case JobType.Gather: return "采集";
                case JobType.Process: return "处理";
                case JobType.Cook: return "烹饪";
                default: return type.ToString();
            }
        }

        public void EnsureDefaultIdFromName()
        {
            if (!string.IsNullOrWhiteSpace(id)) return;
            id = SanitizeId(displayName);
        }

        public static string SanitizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "job_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            var chars = source.Trim().ToLowerInvariant().ToCharArray();
            var builder = new System.Text.StringBuilder(chars.Length);
            bool lastWasSeparator = false;
            foreach (char c in chars)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            var result = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(result)
                ? "job_" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                : result;
        }

        private static void AppendNodeSummary(StringBuilder sb, JobAdvanceNodeId id, JobAdvanceNode node)
        {
            if (node == null) return;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(node.ToSummary(id));
        }

        private static string FormatBranchLine(
            JobAdvanceNodeId id,
            JobAdvanceNode node,
            JobAdvanceNodeId current,
            string prefix)
        {
            string mark = JobAdvancePath.HasTaken(current, id) ? "●" : "○";
            string label = node != null ? node.ToShortLabel(id) : JobAdvancePath.ToLabel(id);
            string effect = node != null && !string.IsNullOrWhiteSpace(node.EffectDescription)
                ? $" — {node.EffectDescription.Trim()}"
                : string.Empty;
            string currentTag = current == id ? "  ←当前" : string.Empty;
            return $"{prefix} {mark} {label}{effect}{currentTag}";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;

            EnsureDefaultIdFromName();
            maxWorkers = Mathf.Max(0, maxWorkers);
            gatherAmountPerWorker = Mathf.Max(0, gatherAmountPerWorker);
            materialPerGatherUnit = Mathf.Max(0, materialPerGatherUnit);
            spicyPerGatherUnit = Mathf.Max(0, spicyPerGatherUnit);
            sourPerGatherUnit = Mathf.Max(0, sourPerGatherUnit);
            coldPerGatherUnit = Mathf.Max(0, coldPerGatherUnit);
            magicPerGatherUnit = Mathf.Max(0, magicPerGatherUnit);
            processAmountPerWorker = Mathf.Max(0, processAmountPerWorker);
            otherMaterialEfficiency = Mathf.Clamp01(otherMaterialEfficiency);
            cookAmountPerWorker = Mathf.Max(0, cookAmountPerWorker);
            scoreMultiplier = Mathf.Max(0f, scoreMultiplier);
            EnsureAdvanceTreeDefaults();
        }
#endif
    }
}
