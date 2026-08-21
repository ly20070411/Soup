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
            "提高本回合烹饪站得分的倍率。\n" +
            "倍率 ≈ 1 + 热辣×2 / 本回合烹饪产出量（默认上限 ×3，遗物可取消上限或额外加成）。\n" +
            "只作用于烹饪站得分；寒冷 / 酸涩 / 鲜美得分不受此倍率影响。";

        public const string ColdBody =
            "每 1 寒冷消耗 2 份处理食材，产出 2 份已烹饪并计分（默认每份已烹饪 +2，遗物可提高）。\n" +
            "可用数量受当前处理食材存量限制（处理食材÷2）。\n" +
            "寒冷分在结算时计入，不受热辣倍率影响。";

        public const string SourBody =
            "大关结算时按本关已烹饪总量占比换分：\n" +
            "前 10% 每份 5 分，至 50% 每份 3 分，其余每份 1 分（遗物可提高前档比例）。\n" +
            "不在每回合结算；超额未用完的酸涩会保留。";

        public const string MagicBody =
            "有烹饪员工时，约消耗 30% 鲜美（向上取整，遗物可降低消耗）。\n" +
            "剩余鲜美 ×3 转化为加分，且不超过本回合「寒冷分 + 热辣加成后的烹饪分」。";

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

        public static void Relic(RelicItem relic, out string title, out string body)
        {
            if (relic == null)
            {
                title = string.Empty;
                body = string.Empty;
                return;
            }

            title = relic.DisplayName;
            string desc = relic.Description != null ? relic.Description.Trim() : string.Empty;
            string rules = relic.GetRulesSummary();
            if (!string.IsNullOrWhiteSpace(desc))
                body = desc;
            else if (!string.IsNullOrWhiteSpace(rules) && rules != "无规则")
                body = rules;
            else
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
                    body = FormatAdvanceOnlyBody(job);
                    break;
                default:
                    body = job.GetEffectSummary();
                    break;
            }
        }

        private static string FormatGatherBody(JobItem job)
        {
            string line1 = FormatGatherCropLine(job);
            string line2 = FormatAdvanceOnlyBody(job);
            return line1 + "\n" + line2;
        }

        private static string FormatGatherCropLine(JobItem job)
        {
            var progression = JobProgressionManager.Instance;
            var path = progression != null ? progression.GetAdvancePath(job) : JobAdvanceNodeId.None;
            var mods = JobAdvanceGatherMods.From(job, path);
            int amount = mods.ResolveAmountPerWorker(job);

            var ingredient = job.OutputIngredient;
            string name = ingredient != null ? ingredient.DisplayName : "采集物";
            if (ingredient != null)
            {
                var yield = IngredientYieldResolver.FromIngredient(ingredient, amount);
                string yieldText = yield.ToSummary();
                if (string.IsNullOrWhiteSpace(yieldText))
                    return $"采集：每员工 {amount} 份{name}";
                return $"采集：每员工 {amount} 份{name}（{yieldText}）";
            }

            return $"采集：每员工 {amount} 份{name}";
        }

        private static string FormatAdvanceOnlyBody(JobItem job)
        {
            var progression = JobProgressionManager.Instance;
            if (progression == null)
                return "进阶：未进阶";

            var path = progression.GetAdvancePath(job);
            if (path == JobAdvanceNodeId.None)
                return "进阶：未进阶";

            job.EnsureAdvanceTreeDefaults();
            var node = job.GetAdvanceNode(path);
            if (node == null || node.IsNoneAdvanceNode())
                return "进阶：未进阶";

            string effect = node.EffectDescription != null ? node.EffectDescription.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(effect) || effect == "无")
            {
                string pathLabel = progression.DescribeCurrentPath(job);
                return string.IsNullOrWhiteSpace(pathLabel) ? "进阶：未进阶" : $"进阶：{pathLabel}";
            }

            string title = !string.IsNullOrWhiteSpace(node.DisplayName) ? node.DisplayName.Trim() : JobAdvancePath.ToLabel(path);
            return $"进阶：{title} — {effect}";
        }
    }
}
