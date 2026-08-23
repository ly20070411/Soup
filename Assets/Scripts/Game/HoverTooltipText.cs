using Soup.Employees;
using Soup.Items;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>Shared hover-tooltip copy for stations, flavors, employees, relics.</summary>
    public static class HoverTooltipText
    {
        public const string SpicyTitle = "热辣";
        public const string ColdTitle = "寒冷";
        public const string SourTitle = "酸涩";
        public const string MagicTitle = "鲜美";

        public const string SpicyBody =
            "围绕关底集中爆发。\n" +
            "关卡最后一回合结束时，按当前热辣倍率乘以本关总分（含酸涩）。\n" +
            "倍率 = 1 + 热辣 ÷ 2 ÷ 已烹饪食材\n" +
            "遗物「无辣不欢」等可在此基础上再乘算；右侧红栏实时显示当前倍率。";

        public const string ColdBody =
            "绕过普通火力，在处理库存充足时提前兑现。\n" +
            "每 1 点寒冷消耗 2 份处理食材，直接产出 2 份已烹饪食材；每份按 2 分计，每点寒冷最高 +4 分。\n" +
            "发动后寒冷会消耗，该分数不受烹饪倍率影响。";

        public const string SourBody =
            "押注关底，在即时得分与延迟收益间取舍。\n" +
            "大关结算时按当前已烹饪食材总量分段换分：\n" +
            "· 酸涩 ≤ 已烹饪 10%：每点 3 分\n" +
            "· 10% < 酸涩 ≤ 50%：每点 2 分\n" +
            "· 50% < 酸涩 ≤ 100%：每点 1 分\n" +
            "· 超出已烹饪食材的部分不计分并保留\n" +
            "该分数不受其他分数倍率影响。";

        public const string MagicBody =
            "放大已经稳定的烹饪体系，不能凭空得分。\n" +
            "烹饪岗位有员工时，每回合消耗当前鲜美的 50%（向上取整）；\n" +
            "剩余鲜美每点换算 3 分，奖励不超过本回合新产出的已烹饪食材份数。\n" +
            "烹饪岗位无人时不消耗鲜美；该分数不受烹饪倍率影响。";

        public static void SourCookZone(out string title, out string body)
        {
            title = SourTitle;
            var store = ResourceStore.Instance;
            FlavorResolver.PreviewSourDetail(
                store,
                out int cooked,
                out int sour,
                out int usable,
                out int total,
                out int t1c,
                out int t1s,
                out int t2c,
                out int t2s,
                out int t3c,
                out int t3s);

            int topPercent = RelicEffectRunner.ResolveSourTopTierPercent(10);
            int secondPercent = RelicEffectRunner.ResolveSourSecondTierPercent(50);
            var lines = new System.Text.StringBuilder(SourBody);
            lines.Append("\n\n—— 当前 ——");
            lines.Append($"\n已烹饪 {cooked}，酸涩 {sour}");
            if (total > 0)
            {
                lines.Append($"\n大关结算可换 +{total} 分（{usable} 酸涩参与换算）");
                if (t1c > 0) lines.Append($"\n· ≤{topPercent}% 档：{t1c}×3 = +{t1s}");
                if (t2c > 0) lines.Append($"\n· {topPercent}%～{secondPercent}% 档：{t2c}×2 = +{t2s}");
                if (t3c > 0) lines.Append($"\n· {secondPercent}%～100% 档：{t3c}×1 = +{t3s}");
                if (sour > usable)
                    lines.Append($"\n· 超出已烹饪 {sour - usable} 酸涩不计分");
            }
            else
            {
                lines.Append("\n当前无法换算酸涩得分（需已烹饪食材 > 0 且酸涩 > 0）。");
            }

            body = lines.ToString();
        }

        public static void Flavor(FlavorType type, out string title, out string body)
        {
            switch (type)
            {
                case FlavorType.Spicy:
                    title = SpicyTitle;
                    body = SpicyBody;
                    break;
                case FlavorType.Cold:
                    title = ColdTitle;
                    body = ColdBody;
                    break;
                case FlavorType.Sour:
                    title = SourTitle;
                    body = SourBody;
                    break;
                case FlavorType.Magic:
                    title = MagicTitle;
                    body = MagicBody;
                    break;
                default:
                    title = type.ToString();
                    body = string.Empty;
                    break;
            }
        }

        /// <summary>AuthoredHud 顶部资源计数器名称（柔软 / 强韧 / 坚固 / 已处理 / 已烹饪）。</summary>
        public static string HudResourceTitle(string key)
        {
            switch (key)
            {
                case "Soft":
                    return "柔软";
                case "Tough":
                    return "强韧";
                case "Solid":
                    return "坚固";
                case "Processed":
                    return "已处理";
                case "Cooked":
                    return "已烹饪";
                default:
                    return key ?? string.Empty;
            }
        }

        /// <summary>食材每份产物摘要，括号形式；无产出时返回空字符串。</summary>
        public static string IngredientYieldParenthesis(IngredientItem ingredient, int units = 1)
        {
            if (ingredient == null || units <= 0)
                return string.Empty;

            var yield = IngredientYieldResolver.FromIngredient(ingredient, units);
            string summary = yield.ToSummary();
            if (string.IsNullOrWhiteSpace(summary) || summary == "无产出")
                return string.Empty;
            return $"（{summary}）";
        }

        /// <summary>进阶节点采集物变更时，在名称后标注对应产物。</summary>
        public static string AnnotateAdvanceNodeIngredientLabel(JobAdvanceNode node, string displayLabel)
        {
            if (node == null || string.IsNullOrWhiteSpace(displayLabel))
                return displayLabel ?? string.Empty;

            displayLabel = displayLabel.Trim();
            if (displayLabel.IndexOf('（') >= 0)
                return displayLabel;

            if (node.BonusIngredient != null && node.BonusIngredientAmount > 0)
            {
                string para = IngredientYieldParenthesis(node.BonusIngredient, node.BonusIngredientAmount);
                if (!string.IsNullOrEmpty(para))
                    return displayLabel + para;
            }

            if (node.VariantIngredient != null && node.VariantChance > 0f)
            {
                string variantName = node.VariantIngredient.DisplayName;
                string para = IngredientYieldParenthesis(node.VariantIngredient, 1);
                if (string.IsNullOrEmpty(para))
                    return displayLabel;

                if (!displayLabel.Contains(variantName))
                    return $"{displayLabel}：{variantName}{para}";
                return displayLabel + para;
            }

            return displayLabel;
        }

        /// <summary>在效果文案中给已出现的食材名补上产物括号。</summary>
        public static string AnnotateEffectIngredientRefs(JobAdvanceNode node, string effect)
        {
            if (node == null || string.IsNullOrWhiteSpace(effect))
                return effect ?? string.Empty;

            effect = effect.Trim();
            if (effect.IndexOf('（') >= 0)
                return effect;

            if (node.BonusIngredient != null && node.BonusIngredientAmount > 0)
            {
                string name = node.BonusIngredient.DisplayName;
                string para = IngredientYieldParenthesis(node.BonusIngredient, node.BonusIngredientAmount);
                if (!string.IsNullOrEmpty(para) && effect.Contains(name))
                    effect = effect.Replace(name, name + para);
            }

            if (node.VariantIngredient != null && node.VariantChance > 0f)
            {
                string name = node.VariantIngredient.DisplayName;
                string para = IngredientYieldParenthesis(node.VariantIngredient, 1);
                if (!string.IsNullOrEmpty(para) && effect.Contains(name))
                    effect = effect.Replace(name, name + para);
            }

            return effect;
        }

        public static void Relic(RelicItem relic, out string title, out string body)
        {
            if (relic == null)
            {
                title = string.Empty;
                body = string.Empty;
                return;
            }

            title = relic.DisplayName;
            int stacks = RelicManager.Instance != null ? RelicManager.Instance.CountOwned(relic) : 1;
            if (stacks > 1)
                title = $"{relic.DisplayName} ×{stacks}";
            body = relic.GetEffectDisplayText(stacks);
            if (string.IsNullOrWhiteSpace(body))
                body = title;
        }

        public static void Employee(EmployeeItem employee, out string title, out string body)
        {
            if (employee == null)
            {
                title = string.Empty;
                body = string.Empty;
                return;
            }

            title = employee.DisplayName;
            // 「劳动力」= 工作效率；其余规则/描述作为特殊效果。
            var summary = employee.GetEffectSummary();
            if (!string.IsNullOrWhiteSpace(summary) && summary.StartsWith("效率"))
                body = "劳动力：" + summary;
            else
                body = string.IsNullOrWhiteSpace(summary) ? "无特殊效果" : summary;
        }

        public static void JobStation(JobItem job, out string title, out string body)
        {
            if (job == null)
            {
                title = string.Empty;
                body = string.Empty;
                return;
            }

            title = job.DisplayName;
            switch (job.JobType)
            {
                case JobType.Gather:
                    body = FormatGatherBody(job);
                    break;
                case JobType.Process:
                case JobType.Cook:
                    body = FormatBaseAndAdvanceBody(job);
                    break;
                default:
                    body = job.GetEffectSummary();
                    break;
            }
        }

        private static string FormatGatherBody(JobItem job)
        {
            string baseLine = FormatGatherCropLine(job);
            string efficiencyLine = FormatGatherEfficiencyLine(job);
            string outputLine = FormatGatherCurrentOutputLine(job);
            string advanceLine = FormatAdvanceLine(job);
            return JoinEffectBlocks(
                JoinEffectBlocks(baseLine, efficiencyLine),
                JoinEffectBlocks(outputLine, advanceLine));
        }

        private static string FormatBaseAndAdvanceBody(JobItem job)
        {
            string baseLine = FormatBaseEffectLine(job);
            string advanceLine = FormatAdvanceLine(job);
            return JoinEffectBlocks(baseLine, advanceLine);
        }

        private static string JoinEffectBlocks(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
                return second ?? string.Empty;
            if (string.IsNullOrWhiteSpace(second))
                return first;
            return first + "\n" + second;
        }

        private static string FormatBaseEffectLine(JobItem job)
        {
            string summary = job != null ? job.GetEffectSummary() : string.Empty;
            if (string.IsNullOrWhiteSpace(summary))
                summary = "无";
            return $"基础：{summary.Trim()}";
        }

        private static string FormatGatherCropLine(JobItem job)
        {
            ResolveGatherPreview(
                job, out _, out var def, out var mods, out _, out _, out int baseAmount);

            var ingredient = def != null ? def.OutputIngredient : null;
            string name = ingredient != null ? ingredient.DisplayName : "采集物";
            if (ingredient != null)
            {
                var perUnit = IngredientYieldResolver.FromIngredient(ingredient);
                string yieldText = perUnit.ToSummary();
                if (string.IsNullOrWhiteSpace(yieldText) || yieldText == "无产出")
                    return $"基础：每员工 {baseAmount} 份{name}";
                return $"基础：每员工 {baseAmount} 份{name}（每份{yieldText}）";
            }

            return $"基础：每员工 {baseAmount} 份{name}";
        }

        private static string FormatGatherEfficiencyLine(JobItem job)
        {
            ResolveGatherPreview(job, out _, out _, out _, out _, out float efficiency, out _);
            return $"当前效率：{FormatEfficiencyPercent(efficiency)}";
        }

        private static string FormatGatherCurrentOutputLine(JobItem job)
        {
            ResolveGatherPreview(
                job, out var runtimeJob, out var def, out var mods, out int amount, out float efficiency, out _);

            if (mods.IsReplacementGatherOutput)
            {
                var replacement = mods.BonusIngredient;
                string replacementName = replacement != null ? replacement.DisplayName : "采集物";
                var em = EmployeeManager.Instance;
                int workers = em != null ? em.GetAssignedCountOnJob(job) : 0;
                int relicBonus = RelicEffectRunner.SumGatherAmountPerWorkerBonus();
                int scaledUnits = workers > 0 && amount > 0
                    ? GameMath.CeilToInt(workers * (double)amount)
                    : 0;
                int outputUnits = mods.ResolveReplacementOutputUnits(workers, scaledUnits, relicBonus);
                if (replacement == null || outputUnits <= 0)
                    return $"当前产出：每员工 {mods.BonusIngredientAmount} 份{replacementName}";

                var replacementYield = BuildGatherPreviewYield(
                    runtimeJob, def, mods, replacement, outputUnits, efficiency);
                string replacementYieldText = replacementYield.ToSummary();
                if (string.IsNullOrWhiteSpace(replacementYieldText) || replacementYieldText == "无产出")
                    return $"当前产出：每回合 {outputUnits} 份{replacementName}";
                return $"当前产出：每回合 {outputUnits} 份{replacementName}（{replacementYieldText}）";
            }

            var ingredient = def != null ? def.OutputIngredient : null;
            string name = ingredient != null ? ingredient.DisplayName : "采集物";
            if (ingredient == null || amount <= 0)
                return $"当前产出：每员工 {amount} 份{name}";

            var yield = BuildGatherPreviewYield(runtimeJob, def, mods, ingredient, amount, efficiency);
            string yieldText = yield.ToSummary();
            string mainLine;
            if (string.IsNullOrWhiteSpace(yieldText) || yieldText == "无产出")
                mainLine = $"当前产出：每员工 {amount} 份{name}";
            else
                mainLine = $"当前产出：每员工 {amount} 份{name}（{yieldText}）";

            var store = ResourceStore.Instance;
            if (store != null)
            {
                int unused = store.WarehouseSpace;
                if (unused == int.MaxValue)
                    unused = 0;
                int warehouseSolid = mods.ComputeWarehouseScaledSolidBonus(
                    unused, store.WarehouseCapacity, store.Solid);
                if (warehouseSolid > 0)
                    mainLine += $"；仓库加成 +{warehouseSolid}坚固";
            }

            if (mods.HasBonusIngredient)
            {
                string bonusName = mods.BonusIngredient.DisplayName;
                string bonusPara = IngredientYieldParenthesis(
                    mods.BonusIngredient, mods.BonusIngredientAmount);
                mainLine += $"；+{mods.BonusIngredientAmount}份{bonusName}{bonusPara}";
            }

            return mainLine;
        }

        private static void ResolveGatherPreview(
            JobItem job,
            out JobItem runtimeJob,
            out JobItem def,
            out JobAdvanceGatherMods mods,
            out int currentAmountPerWorker,
            out float efficiency,
            out int baseAmountPerWorker)
        {
            runtimeJob = job;
            var progression = JobProgressionManager.Instance;
            var path = progression != null ? progression.GetAdvancePath(job) : JobAdvanceNodeId.None;
            def = progression != null ? progression.ResolveGatherDefinition(job) : job;
            mods = JobAdvanceGatherMods.From(job, path);
            baseAmountPerWorker = Mathf.Max(0, mods.ResolveAmountPerWorker(def));
            currentAmountPerWorker = baseAmountPerWorker + RelicEffectRunner.SumGatherAmountPerWorkerBonus();
            if (currentAmountPerWorker < 0)
                currentAmountPerWorker = 0;

            float eventYield = progression != null ? progression.GetEventYieldMultiplier(job) : 1f;
            if (eventYield > 0f && !Mathf.Approximately(eventYield, 1f))
                currentAmountPerWorker = GameMath.CeilToInt(currentAmountPerWorker * eventYield);

            var em = EmployeeManager.Instance;
            int workers = em != null ? em.GetAssignedCountOnJob(job) : 0;
            float labor = em != null ? em.GetLaborOnJob(job) : 0f;
            efficiency = WorkEfficiencyResolver.PreviewGatherConversionEfficiency(
                job, mods, labor, workers);
        }

        private static IngredientYield BuildGatherPreviewYield(
            JobItem job,
            JobItem def,
            JobAdvanceGatherMods mods,
            IngredientItem ingredient,
            int units,
            float efficiency)
        {
            var yield = IngredientYieldResolver.FromIngredient(ingredient, units);

            int softBonus = mods.SoftPerUnitBonus;
            int maxWorkers = JobProgressionManager.Instance != null
                ? JobProgressionManager.Instance.GetEffectiveMaxWorkers(job)
                : (def != null
                    ? def.GetEffectiveMaxWorkers(
                        JobProgressionManager.Instance != null
                            ? JobProgressionManager.Instance.GetAdvancePath(job)
                            : JobAdvanceNodeId.None)
                    : 0);
            var em = EmployeeManager.Instance;
            int workers = em != null ? em.GetAssignedCountOnJob(job) : 0;
            if (mods.SoftPerUnitWhenFull > 0 && maxWorkers > 0 && workers >= maxWorkers)
                softBonus += mods.SoftPerUnitWhenFull;

            int coldBonus = mods.ColdPerUnitBonus + JobAdvanceGatherMods.SumOtherGatherColdAura(job);
            int spicyBonus = mods.SpicyPerUnitBonus + JobAdvanceGatherMods.SumOtherGatherSpicyAura(job);
            int sourBonus = mods.SourPerUnitBonus + JobAdvanceGatherMods.SumOtherGatherSourAura(job);
            int magicBonus = mods.MagicPerUnitBonus + JobAdvanceGatherMods.SumOtherGatherMagicAura(job);
            int randomFlavorBonus = mods.RandomFlavorPerUnitBonus
                                    + JobAdvanceGatherMods.SumOtherGatherRandomFlavorAura(job);
            int solidBonus = mods.SolidPerUnitBonus;
            int toughBonus = mods.ToughPerUnitBonus;

            var eventMods = JobProgressionManager.Instance?.GetEventMods(job);
            if (eventMods != null)
            {
                coldBonus += eventMods.ColdPerUnitDelta;
                spicyBonus += eventMods.SpicyPerUnitDelta;
                sourBonus += eventMods.SourPerUnitDelta;
                magicBonus += eventMods.MagicPerUnitDelta;
            }

            if (softBonus != 0)
                yield.Soft = Mathf.Max(0, yield.Soft + softBonus * units);
            if (solidBonus != 0)
                yield.Solid = Mathf.Max(0, yield.Solid + solidBonus * units);
            if (toughBonus != 0)
                yield.Tough = Mathf.Max(0, yield.Tough + toughBonus * units);
            if (coldBonus != 0)
                yield.Cold = Mathf.Max(0, yield.Cold + coldBonus * units);
            if (spicyBonus != 0)
                yield.Spicy = Mathf.Max(0, yield.Spicy + spicyBonus * units);
            if (sourBonus != 0)
                yield.Sour = Mathf.Max(0, yield.Sour + sourBonus * units);
            if (magicBonus != 0)
                yield.Magic = Mathf.Max(0, yield.Magic + magicBonus * units);
            if (randomFlavorBonus != 0)
                yield.RandomFlavor = Mathf.Max(0, yield.RandomFlavor + randomFlavorBonus * units);

            if (mods.ShouldSuppressYieldFor(ingredient))
            {
                yield.Soft = 0;
                yield.Tough = 0;
                yield.Solid = 0;
                yield.RandomMaterial = 0;
            }

            yield = yield.ScaledByEfficiency(efficiency);

            float flavorBonus = JobAdvanceGatherMods.SumIncomingDesignatedPairFlavorYieldBonus(job);
            if (flavorBonus > 0f)
                yield = yield.ScaledFlavorsBy(1f + flavorBonus);

            return yield;
        }

        private static string FormatEfficiencyPercent(float efficiency)
        {
            float pct = efficiency * 100f;
            if (Mathf.Abs(pct - Mathf.Round(pct)) < 0.05f)
                return $"{Mathf.RoundToInt(pct)}%";
            return $"{pct:0.#}%";
        }

        private static string FormatAdvanceLine(JobItem job)
        {
            var progression = JobProgressionManager.Instance;
            if (progression == null)
                return "进阶：未进阶";

            var path = progression.GetAdvancePath(job);
            if (path == JobAdvanceNodeId.None)
                return "进阶：未进阶";

            var def = progression.ResolveGatherDefinition(job);
            def.EnsureAdvanceTreeDefaults();
            var node = def.GetAdvanceNode(path);
            if (node == null || node.IsNoneAdvanceNode())
                return "进阶：未进阶";

            string effect = node.EffectDescription != null ? node.EffectDescription.Trim() : string.Empty;
            string pathLabel = !string.IsNullOrWhiteSpace(node.DisplayName)
                ? node.DisplayName.Trim()
                : JobAdvancePath.ToLabel(path);
            pathLabel = AnnotateAdvanceNodeIngredientLabel(node, pathLabel);
            effect = AnnotateEffectIngredientRefs(node, effect);

            if (string.IsNullOrWhiteSpace(effect) || effect == "无")
            {
                string fallback = progression.DescribeCurrentPath(job);
                if (!string.IsNullOrWhiteSpace(pathLabel) && pathLabel != "无")
                    return $"进阶：{pathLabel}";
                return string.IsNullOrWhiteSpace(fallback) ? "进阶：未进阶" : $"进阶：{fallback}";
            }

            // Keep label + effect on separate lines when the effect is long, so wraps stay aligned.
            if (effect.Length > 18 || effect.IndexOf('\n') >= 0)
                return $"进阶：{pathLabel}\n　　{effect.Replace("\n", "\n　　")}";

            return $"进阶：{pathLabel} — {effect}";
        }
    }
}
