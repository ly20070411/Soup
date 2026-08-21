namespace Soup.Relics
{
    /// <summary>
    /// How a relic enters the run.
    /// </summary>
    public enum RelicAcquireStage
    {
        /// <summary>Legacy start-of-run pick.</summary>
        Starting = 0,
        /// <summary>Granted by events (or similar rewards).</summary>
        Event = 1,
        /// <summary>Purchased from the shop between stages.</summary>
        Shop = 2,
        /// <summary>Legacy start-or-shop tag; still eligible for the shop pool.</summary>
        StartingAndShop = 3
    }

    public static class RelicAcquireStageUtil
    {
        public static bool IsStartingChoice(RelicAcquireStage stage) =>
            stage == RelicAcquireStage.Starting || stage == RelicAcquireStage.StartingAndShop;

        /// <summary>商店货架：仅「商店获取」及遗留的「开局/商店」标签。</summary>
        public static bool IsShopEligible(RelicAcquireStage stage) =>
            stage == RelicAcquireStage.Shop
            || stage == RelicAcquireStage.StartingAndShop;

        public static bool MatchesStageFilter(RelicAcquireStage relicStage, RelicAcquireStage filter)
        {
            if (relicStage == filter) return true;
            if (filter == RelicAcquireStage.Starting && RelicAcquireStageUtil.IsStartingChoice(relicStage))
                return true;
            if (filter == RelicAcquireStage.Shop && RelicAcquireStageUtil.IsShopEligible(relicStage))
                return true;
            return false;
        }
    }
}
