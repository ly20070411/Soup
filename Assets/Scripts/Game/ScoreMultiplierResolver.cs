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
        /// <summary>热辣倍率（含岗位/遗物对热辣的额外加成）。</summary>
        public static float ComputeSpicyMultiplier(
            ResourceStore store,
            int cookedThisTurn,
            float spicyMultiplierCap = 0f,
            bool spicyUncapped = false,
            float relicSpicyScoreMultiplierBonus = 0f)
        {
            if (store == null || cookedThisTurn <= 0 || store.Spicy <= 0)
                return 1f;

            float mult = 1f + store.Spicy * 2f / cookedThisTurn;
            if (!spicyUncapped && spicyMultiplierCap > 0f)
                mult = Mathf.Min(mult, spicyMultiplierCap);

            float bonus = JobAdvanceGatherMods.SumSpicyScoreMultiplierBonus()
                          + Mathf.Max(0f, relicSpicyScoreMultiplierBonus);
            if (bonus > 0f)
                mult *= 1f + bonus;

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
        /// 将倍率应用于火力基础分（CookScoreBase 已含火力倍率）。
        /// 返回最终烹饪分数；寒冷/酸涩/鲜美不在此处理。
        /// </summary>
        public static int ApplyCookScoreMultipliers(
            TurnResult result,
            RelicContext relicCtx,
            float spicyMultiplier)
        {
            if (result == null || result.CookScoreBase <= 0)
                return 0;

            float relicMult = ResolveRelicTotalMultiplier(relicCtx);
            float happyMult = WorkEfficiencyResolver.ResolveHappyTuotuoEfficiency();
            float combined = spicyMultiplier * relicMult * happyMult;

            int cookFinal = GameMath.CeilToInt(result.CookScoreBase * combined);
            result.SpicyMultiplier = spicyMultiplier;
            result.FinalMultiplier = relicMult;
            result.IndependentMultiplier = relicCtx != null ? relicCtx.IndependentMultiplier : 1f;
            result.CookScore = cookFinal;
            return cookFinal;
        }
    }

}
