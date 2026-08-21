using System.Collections.Generic;
using Soup.Employees;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Non-destructive preview of how much score each flavor would contribute
    /// with the current resource / assignment state.
    /// </summary>
    public struct FlavorScoreBreakdown
    {
        public int ColdUsable;
        public int ColdScore;
        public int ColdBlockedByProcessed;

        public int CookScoreBase;
        public int CookedThisTurn;
        public float SpicyMultiplier;
        public int SpicyBonusScore;

        public int SourUsable;
        public int SourScore;
        public int SourCookedBasis;

        public int MagicConsumed;
        public int MagicRemaining;
        public int MagicRawBonus;
        public int MagicScore;
        public int MagicCappedAway;
        public bool MagicHasCookWorkers;
    }

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
        /// Preview using live store / elves / stage cooked, resolving spicy cap from config & relics.
        /// </summary>
        public static FlavorScoreBreakdown PreviewScoresFromState(
            ResourceStore store = null,
            ElfManager elves = null,
            TurnManager turns = null)
        {
            store ??= ResourceStore.Instance;
            elves ??= ElfManager.Instance;
            turns ??= TurnManager.Instance;

            float spicyCap = 3f;
            bool spicyUncapped = false;
            float spicyScoreBonus = 0f;
            if (store != null)
            {
                var config = store.Config;
                if (config != null)
                    spicyCap = config.SpicyMultiplierCap;
                else
                {
                    var loaded = Resources.Load<GameConfig>(ResourceStore.ResourcesConfigPath);
                    if (loaded != null)
                        spicyCap = loaded.SpicyMultiplierCap;
                }
            }

            var relics = RelicManager.Instance;
            if (relics != null)
            {
                for (int i = 0; i < relics.Owned.Count; i++)
                {
                    var relic = relics.Owned[i];
                    if (relic?.Rules == null) continue;
                    for (int r = 0; r < relic.Rules.Count; r++)
                    {
                        var rule = relic.Rules[r];
                        if (rule == null) continue;
                        if (rule.Effect == RelicEffectType.DisableSpicyCap)
                            spicyUncapped = true;
                        if (rule.Effect == RelicEffectType.AddSpicyScoreMultiplier)
                            spicyScoreBonus += rule.FloatValue;
                    }
                }
            }

            int stageCooked = turns != null ? turns.StageCooked : 0;
            return PreviewScores(store, elves, stageCooked, spicyCap, spicyUncapped, spicyScoreBonus);
        }

        /// <summary>
        /// Compact one-line summary for HUD: 热辣+N 酸涩+N 寒冷+N 鲜美+N
        /// </summary>
        public static string FormatScoreSummary(in FlavorScoreBreakdown preview)
        {
            return
                $"热辣+{preview.SpicyBonusScore}  酸涩+{preview.SourScore}  " +
                $"寒冷+{preview.ColdScore}  鲜美+{preview.MagicScore}";
        }

        /// <summary>
        /// Preview score each flavor would provide without mutating game state.
        /// Cold / spicy / magic follow next-turn resolution order;
        /// sour uses current stage cooked (大关结算口径).
        /// </summary>
        public static FlavorScoreBreakdown PreviewScores(
            ResourceStore store,
            ElfManager elves,
            int stageCooked,
            float spicyMultiplierCap = 0f,
            bool spicyUncapped = false,
            float relicSpicyScoreMultiplierBonus = 0f)
        {
            var preview = new FlavorScoreBreakdown();
            if (store == null)
                return preview;

            int processed = store.Processed;

            // Cold
            int cold = store.Cold;
            int maxByProcessed = processed / ColdProcessedPerFlavor;
            preview.ColdUsable = Mathf.Min(cold, maxByProcessed);
            preview.ColdBlockedByProcessed = Mathf.Max(0, cold - preview.ColdUsable);
            // 寒冷分数不受分数倍率影响；冰点等遗物为每份固定加分。
            int scorePerCooked = ColdScorePerCooked + RelicEffectRunner.SumColdScorePerCookedUnit();
            int cookedFromCold = preview.ColdUsable * ColdCookedPerFlavor;
            preview.ColdScore = cookedFromCold * scorePerCooked;
            processed -= preview.ColdUsable * ColdProcessedPerFlavor;
            int cookedThisTurn = cookedFromCold;

            // Cook stations (same rules as TurnManager.ResolveCook)
            int cookScoreBase = 0;
            int cookedFromCook = 0;
            foreach (var pair in EnumerateCookLabor(elves))
            {
                var job = pair.job;
                float labor = pair.labor;
                if (job == null || labor <= 0f) continue;

                var advancePath = JobProgressionManager.Instance != null
                    ? JobProgressionManager.Instance.GetAdvancePath(job)
                    : JobAdvanceNodeId.None;
                var cookMods = JobAdvanceCookMods.From(job, advancePath);
                int amountPerWorker = cookMods.ResolveAmountPerWorker(job);
                float scoreMultiplier = cookMods.ResolveScoreMultiplier(job);

                float capacityMult = WorkEfficiencyResolver.ResolveWorkCapacityMultiplier(
                    job, WorkEfficiencyScope.Cook);
                int demand = GameMath.CeilToInt(labor * amountPerWorker * capacityMult);
                if (demand <= 0) continue;

                int consumed = Mathf.Min(demand, processed);
                if (consumed <= 0) continue;

                processed -= consumed;
                cookedFromCook += consumed;
                cookScoreBase += GameMath.CeilMul(consumed, scoreMultiplier);
            }

            preview.CookScoreBase = cookScoreBase;
            cookedThisTurn += cookedFromCook;
            preview.CookedThisTurn = cookedThisTurn;

            float spicyMult = ScoreMultiplierResolver.ComputeSpicyMultiplier(
                store,
                cookedThisTurn,
                spicyMultiplierCap,
                spicyUncapped,
                relicSpicyScoreMultiplierBonus);
            int cookAfterSpicy = cookScoreBase > 0
                ? GameMath.CeilToInt(cookScoreBase * spicyMult)
                : 0;
            preview.SpicyMultiplier = cookScoreBase > 0 ? spicyMult : 1f;
            preview.SpicyBonusScore = Mathf.Max(0, cookAfterSpicy - cookScoreBase);

            // Sour — stage settlement basis
            preview.SourCookedBasis = Mathf.Max(0, stageCooked);
            preview.SourUsable = Mathf.Min(store.Sour, preview.SourCookedBasis);
            preview.SourScore = ScoreSour(preview.SourUsable, preview.SourCookedBasis);

            // Magic
            preview.MagicHasCookWorkers = HasCookWorkers(elves);
            if (preview.MagicHasCookWorkers && store.Magic > 0)
            {
                int magic = store.Magic;
                int consumed = ComputeMagicConsumed(magic);
                int remaining = magic - consumed;
                int rawBonus = remaining * 3;
                int cookedFoodScore = preview.ColdScore + cookAfterSpicy;
                int bonus = Mathf.Min(rawBonus, cookedFoodScore);

                preview.MagicConsumed = consumed;
                preview.MagicRemaining = remaining;
                preview.MagicRawBonus = rawBonus;
                preview.MagicScore = bonus;
                preview.MagicCappedAway = Mathf.Max(0, rawBonus - bonus);
            }

            return preview;
        }

        private static IEnumerable<(JobItem job, float labor)> EnumerateCookLabor(ElfManager elves)
        {
            if (EmployeeManager.Instance != null)
            {
                foreach (var pair in EmployeeManager.Instance.GetLaborByJob())
                {
                    if (pair.Key != null && pair.Key.JobType == JobType.Cook && pair.Value > 0f)
                        yield return (pair.Key, pair.Value);
                }

                yield break;
            }

            if (elves == null) yield break;
            foreach (var pair in elves.GetAssignments())
            {
                if (pair.Key != null && pair.Key.JobType == JobType.Cook && pair.Value > 0)
                    yield return (pair.Key, pair.Value);
            }
        }

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
            // 寒冷分数不受分数倍率影响；冰点等遗物为每份固定加分。
            int scorePerCooked = ColdScorePerCooked + RelicEffectRunner.SumColdScorePerCookedUnit();
            int scoreGain = cookedGain * scorePerCooked;

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
        /// 热辣倍率仅作用于烹饪站分数（CookScoreBase 已含火力倍率）。
        /// 完整结算见 <see cref="ScoreMultiplierResolver.ApplyCookScoreMultipliers"/>。
        /// </summary>
        public static void ApplySpicyToCookScore(
            ResourceStore store,
            TurnResult result,
            float spicyMultiplierCap = 0f,
            bool spicyUncapped = false,
            float relicSpicyScoreMultiplierBonus = 0f)
        {
            if (store == null || result == null) return;
            if (result.CookScoreBase <= 0) return;

            float spicyMult = ScoreMultiplierResolver.ComputeSpicyMultiplier(
                store,
                result.CookedGained,
                spicyMultiplierCap,
                spicyUncapped,
                relicSpicyScoreMultiplierBonus);
            int cookAfterSpicy = GameMath.CeilToInt(result.CookScoreBase * spicyMult);
            int delta = cookAfterSpicy - result.CookScoreBase;

            result.SpicyMultiplier = spicyMult;
            result.CookScore = cookAfterSpicy;
            result.ScoreGained += delta;
            if (spicyUsed > 0)
                store.ConsumeFlavorUpTo(FlavorType.Spicy, spicyUsed);
        }

        public static int CalculateSpicyUsage(
            int availableSpicy,
            int cookedThisTurn,
            float spicyMultiplierCap,
            bool spicyUncapped)
        {
            availableSpicy = Mathf.Max(0, availableSpicy);
            cookedThisTurn = Mathf.Max(0, cookedThisTurn);
            if (availableSpicy == 0 || cookedThisTurn == 0) return 0;
            if (spicyUncapped || spicyMultiplierCap <= 0f)
                return availableSpicy;
            if (spicyMultiplierCap <= 1f)
                return 0;

            int requiredForCap = GameMath.CeilToInt(
                (spicyMultiplierCap - 1f) * cookedThisTurn * 0.5f);
            return Mathf.Min(availableSpicy, requiredForCap);
        }

        /// <summary>
        /// Progressive sour→score conversion vs cooked food.
        /// Default tiers: &lt;=10% →5, &lt;=50% →3, &lt;=100% →1 (酸酸糖 raises top tier to 20%).
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

        /// <summary>
        /// Progressive sour→score conversion vs cooked food.
        /// Default tiers: &lt;=10% →5, &lt;=50% →3, &lt;=100% →1.
        /// Top-tier percent can be raised by relics (酸酸糖 → 20%).
        /// </summary>
        public static int ScoreSour(int sourAmount, int cookedAmount)
        {
            if (sourAmount <= 0 || cookedAmount <= 0) return 0;

            int topTierPercent = RelicEffectRunner.ResolveSourTopTierPercent(10);
            topTierPercent = Mathf.Clamp(topTierPercent, 1, 50);

            int tier1End = GameMath.CeilDiv(cookedAmount * topTierPercent, 100);
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
        /// reduced by relics (科技与狠活), bonus = remaining * 3,
        /// capped by this turn's cooked-food score (cold + spicy-boosted cook).
        /// </summary>
        public static void ResolveMagic(ElfManager elves, ResourceStore store, TurnResult result)
        {
            if (elves == null || store == null || result == null) return;
            if (!HasCookWorkers(elves)) return;

            int magic = store.Magic;
            if (magic <= 0) return;

            int consumed = ComputeMagicConsumed(magic);
            int remaining = magic - consumed;
            store.ConsumeFlavorUpTo(FlavorType.Magic, consumed);

            int rawBonus = remaining * 3;
            int cookedFoodScore = result.ColdScore + result.CookScore;
            int bonus = Mathf.Min(rawBonus, cookedFoodScore);
            result.MagicConsumed += consumed;
            result.MagicScore += bonus;
            result.ScoreGained += bonus;
        }

        /// <summary>
        /// Base magic consume rate is 30%; relics can remove a fraction of that rate
        /// (0.1 → consume 27%).
        /// </summary>
        public static int ComputeMagicConsumed(int magic)
        {
            if (magic <= 0) return 0;
            float rate = 0.30f * (1f - RelicEffectRunner.SumMagicConsumeReduction());
            rate = Mathf.Clamp01(rate);
            int percent = Mathf.Max(0, Mathf.RoundToInt(rate * 100f));
            return Mathf.Min(magic, GameMath.CeilDiv(magic * percent, 100));
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
