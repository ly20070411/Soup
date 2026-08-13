using Soup.Employees;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Flavor settlement helpers (cold / spicy / sour / magic).
    /// All fractional math rounds up.
    /// </summary>
    public static class FlavorResolver
    {
        public const int ColdProcessedPerFlavor = 2;
        public const int ColdCookedPerFlavor = 2;
        public const int ColdScorePerCooked = 2;

        /// <summary>
        /// Each cold converts 2 processed → 2 cooked for 2 score each (no cook multiplier).
        /// Activated cold is consumed.
        /// </summary>
        public static void ResolveCold(ResourceStore store, TurnResult result)
        {
            if (store == null || result == null) return;

            int cold = store.Cold;
            if (cold <= 0) return;

            int maxByProcessed = store.Processed / ColdProcessedPerFlavor;
            int usable = Mathf.Min(cold, maxByProcessed);
            if (usable <= 0) return;

            int processedNeeded = usable * ColdProcessedPerFlavor;
            int cookedGain = usable * ColdCookedPerFlavor;
            int scoreGain = cookedGain * ColdScorePerCooked;

            store.ConsumeProcessedUpTo(processedNeeded);
            store.ConsumeFlavorUpTo(FlavorType.Cold, usable);
            store.AddCooked(cookedGain);

            result.ProcessedConsumed += processedNeeded;
            result.CookedGained += cookedGain;
            result.ColdScore += scoreGain;
            result.ColdUsed += usable;
            result.ScoreGained += scoreGain;
        }

        /// <summary>
        /// Independent multiplier on cook-station score:
        /// 1 + spicy * 2 / cookedThisTurn.
        /// Optionally capped unless relics disable the cap.
        /// </summary>
        public static void ApplySpicyToCookScore(
            ResourceStore store,
            TurnResult result,
            float spicyMultiplierCap = 0f,
            bool spicyUncapped = false)
        {
            if (store == null || result == null) return;
            if (result.CookScoreBase <= 0) return;

            int cooked = result.CookedGained;
            int spicy = store.Spicy;
            float mult = 1f;
            if (cooked > 0 && spicy > 0)
                mult = 1f + spicy * 2f / cooked;

            if (!spicyUncapped && spicyMultiplierCap > 0f)
                mult = Mathf.Min(mult, spicyMultiplierCap);

            int boosted = GameMath.CeilToInt(result.CookScoreBase * mult);
            int delta = boosted - result.CookScoreBase;
            result.SpicyMultiplier = mult;
            result.CookScore = boosted;
            result.ScoreGained += delta;
        }

        /// <summary>
        /// Progressive sour→score conversion vs cooked food.
        /// Tiers: &lt;=10% →5, &lt;=50% →3, &lt;=100% →1, beyond cooked →0.
        /// Only used at stage (大关) settlement — not each cook turn.
        /// Scored sour is consumed; excess remains.
        /// </summary>
        public static void ResolveSourForSettlement(
            ResourceStore store,
            int cookedInStage,
            out int sourUsed,
            out int sourScore)
        {
            sourUsed = 0;
            sourScore = 0;
            if (store == null || cookedInStage <= 0) return;

            int sour = store.Sour;
            if (sour <= 0) return;

            sourUsed = Mathf.Min(sour, cookedInStage);
            sourScore = ScoreSour(sourUsed, cookedInStage);
            if (sourUsed > 0)
                store.ConsumeFlavorUpTo(FlavorType.Sour, sourUsed);
        }

        [System.Obsolete("Sour settles at stage end. Use ResolveSourForSettlement.")]
        public static void ResolveSour(ResourceStore store, TurnResult result)
        {
            if (store == null || result == null) return;
            ResolveSourForSettlement(store, result.CookedGained, out int used, out int score);
            result.SourUsed += used;
            result.SourScore += score;
            result.ScoreGained += score;
        }

        public static int ScoreSour(int sourAmount, int cookedAmount)
        {
            if (sourAmount <= 0 || cookedAmount <= 0) return 0;

            int tier1End = GameMath.CeilDiv(cookedAmount * 10, 100); // <=10%
            int tier2End = GameMath.CeilDiv(cookedAmount * 50, 100); // <=50%
            int tier3End = cookedAmount;                             // <=100%

            int remaining = Mathf.Min(sourAmount, cookedAmount);
            int score = 0;

            int take1 = Mathf.Min(remaining, tier1End);
            score += take1 * 5;
            remaining -= take1;

            int take2 = Mathf.Min(remaining, Mathf.Max(0, tier2End - tier1End));
            score += take2 * 3;
            remaining -= take2;

            int take3 = Mathf.Min(remaining, Mathf.Max(0, tier3End - tier2End));
            score += take3 * 1;

            return score;
        }

        /// <summary>
        /// If any cook workers are assigned: consume 30% of magic (ceil),
        /// bonus = remaining * 3, capped by this turn's cooked-food score
        /// (cold + spicy-boosted cook).
        /// </summary>
        public static void ResolveMagic(ElfManager elves, ResourceStore store, TurnResult result)
        {
            if (elves == null || store == null || result == null) return;
            if (!HasCookWorkers(elves)) return;

            int magic = store.Magic;
            if (magic <= 0) return;

            int consumed = GameMath.CeilDiv(magic * 30, 100);
            consumed = Mathf.Min(consumed, magic);
            int remaining = magic - consumed;
            store.ConsumeFlavorUpTo(FlavorType.Magic, consumed);

            int rawBonus = remaining * 3;
            int cookedFoodScore = result.ColdScore + result.CookScore;
            int bonus = Mathf.Min(rawBonus, cookedFoodScore);

            result.MagicConsumed += consumed;
            result.MagicScore += bonus;
            result.ScoreGained += bonus;
        }

        public static bool HasCookWorkers(ElfManager elves)
        {
            if (EmployeeManager.Instance != null)
            {
                foreach (var pair in EmployeeManager.Instance.GetLaborByJob())
                {
                    if (pair.Key != null && pair.Key.JobType == JobType.Cook && pair.Value > 0f)
                        return true;
                }

                return false;
            }

            if (elves == null) return false;
            foreach (var pair in elves.GetAssignments())
            {
                if (pair.Key != null && pair.Key.JobType == JobType.Cook && pair.Value > 0)
                    return true;
            }
            return false;
        }
    }
}
