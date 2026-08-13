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

        [Header("Advancement")]
        [Tooltip("Per-level upgrade definitions. Gather/Process: up to 2; Cook: up to 1.")]
        [SerializeField] private List<JobUpgradeTier> upgradeTiers = new List<JobUpgradeTier>();

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
        public IReadOnlyList<JobUpgradeTier> UpgradeTiers => upgradeTiers;
        public int DesignedMaxUpgradeLevel => JobProgressionRules.MaxUpgradesPerJob(jobType);

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

        public JobUpgradeTier GetUpgradeTier(int zeroBasedLevel)
        {
            if (upgradeTiers == null || zeroBasedLevel < 0 || zeroBasedLevel >= upgradeTiers.Count)
                return null;
            return upgradeTiers[zeroBasedLevel];
        }

        /// <summary>Population bonus for the first <paramref name="upgradeLevel"/> upgrades (1..N).</summary>
        public int GetWorkersBonusForLevel(int upgradeLevel)
        {
            if (upgradeLevel <= 0 || upgradeTiers == null) return 0;

            int bonus = 0;
            int count = Mathf.Min(upgradeLevel, upgradeTiers.Count);
            for (int i = 0; i < count; i++)
            {
                if (upgradeTiers[i] != null)
                    bonus += upgradeTiers[i].MaxWorkersBonus;
            }

            return bonus;
        }

        public int GetEffectiveMaxWorkers(int upgradeLevel)
        {
            if (!HasWorkerLimit) return 0;
            return maxWorkers + GetWorkersBonusForLevel(upgradeLevel);
        }

        public string GetUpgradeSummary()
        {
            EnsureUpgradeTierSize();
            if (upgradeTiers == null || upgradeTiers.Count == 0)
                return "无进阶";

            var sb = new StringBuilder();
            int show = Mathf.Min(DesignedMaxUpgradeLevel, upgradeTiers.Count);
            for (int i = 0; i < show; i++)
            {
                if (upgradeTiers[i] == null) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(upgradeTiers[i].ToSummary(i));
            }

            return sb.Length > 0 ? sb.ToString() : "无进阶";
        }

        public void EnsureUpgradeTierSize()
        {
            if (upgradeTiers == null)
                upgradeTiers = new List<JobUpgradeTier>();

            int target = DesignedMaxUpgradeLevel;
            while (upgradeTiers.Count < target)
            {
                var tier = new JobUpgradeTier();
                if (JobProgressionRules.UsesPopulationCap(jobType))
                    tier.SetMaxWorkersBonus(JobProgressionRules.DefaultUpgradeWorkerBonus);
                else
                    tier.SetMaxWorkersBonus(0);
                tier.SetEffectDescription(string.Empty);
                upgradeTiers.Add(tier);
            }

            if (upgradeTiers.Count > target)
                upgradeTiers.RemoveRange(target, upgradeTiers.Count - target);
        }

        public void SeedDefaultUpgradeTiers()
        {
            upgradeTiers = new List<JobUpgradeTier>();
            EnsureUpgradeTierSize();
        }

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
            EnsureUpgradeTierSize();
        }
#endif
    }
}
