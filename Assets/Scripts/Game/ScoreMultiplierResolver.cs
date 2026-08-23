using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// 烹饪分数倍率：火力 × 热辣 × 遗物总倍率 × 快乐坨坨。
    /// 寒冷、酸涩、鲜美分数不受这些倍率影响。
    /// </summary>
    public static class ScoreMultiplierResolver
    {
        /// <summary>
        /// 热辣倍率：1 + 热辣÷2÷已烹饪食材；岗位/遗物加成（如「无辣不欢」×1.5）在其后相乘。
        /// <paramref name="cookedFoodBasis"/> 为结算时仓库已烹饪食材总量。
        /// </summary>
        public static float ComputeSpicyMultiplier(
            ResourceStore store,
            int cookedFoodBasis,
            float spicyMultiplierCap = 0f,
            bool spicyUncapped = false,
            float relicSpicyScoreMultiplierBonus = 0f)
        {
            if (store == null || cookedFoodBasis <= 0 || store.Spicy <= 0)
                return 1f;

            float mult = 1f + store.Spicy / (2f * cookedFoodBasis);
            if (!spicyUncapped && spicyMultiplierCap > 0f)
                mult = Mathf.Min(mult, spicyMultiplierCap);

            float jobBonus = JobAdvanceGatherMods.SumSpicyScoreMultiplierBonus();
            if (jobBonus > 0f)
                mult *= 1f + jobBonus;

            if (relicSpicyScoreMultiplierBonus > 0f)
                mult *= 1f + relicSpicyScoreMultiplierBonus;

            return Mathf.Max(0f, mult);
        }

        /// <summary>遗物总倍率（Additive FinalMultiplier × IndependentMultiplier）。</summary>
        public static float ResolveRelicTotalMultiplier(RelicContext relicCtx)
        {
            if (relicCtx == null) return 1f;
            float final = Mathf.Max(0f, relicCtx.FinalMultiplier);
            float independent = Mathf.Max(0f, relicCtx.IndependentMultiplier);
            return final * independent;
        }

        /// <summary>
        /// 烹饪区左侧倍率预览：遗物最终倍率 × 快乐坨坨（不含热辣；热辣在右侧单独展示）。
        /// </summary>
        public static float PreviewNonSpicyCookMultiplier(ResourceStore store = null)
        {
            store ??= ResourceStore.Instance;
            var ctx = new RelicContext(store, null)
            {
                LevelTurnNumber = RelicManager.GetLevelTurnNumber()
            };
            RelicEffectRunner.Run(RelicTrigger.AfterScore, ctx);
            return ResolveRelicTotalMultiplier(ctx) * WorkEfficiencyResolver.ResolveHappyTuotuoEfficiency();
        }

        /// <summary>
        /// 将遗物 / 快乐坨坨倍率应用于火力基础分（CookScoreBase 已含火力倍率）。
        /// 热辣在关卡最后一回合单独结算，不在此处理。
        /// 返回最终烹饪分数；寒冷/酸涩/鲜美不在此处理。
        /// </summary>
        public static int ApplyCookScoreMultipliers(
            TurnResult result,
            RelicContext relicCtx)
        {
            if (result == null || result.CookScoreBase <= 0)
                return 0;

            float relicMult = ResolveRelicTotalMultiplier(relicCtx);
            float happyMult = WorkEfficiencyResolver.ResolveHappyTuotuoEfficiency();
            float combined = relicMult * happyMult;

            int cookFinal = GameMath.CeilToInt(result.CookScoreBase * combined);
            result.SpicyMultiplier = 1f;
            result.FinalMultiplier = relicMult;
            result.IndependentMultiplier = relicCtx != null ? relicCtx.IndependentMultiplier : 1f;
            result.CookScore = cookFinal;
            return cookFinal;
        }
    }

}
