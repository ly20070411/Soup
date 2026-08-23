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
        /// <summary>有烹饪员工时，每回合消耗当前鲜美的基础比例（遗物可在此基础上减少）。</summary>
        public const float MagicConsumeBaseRate = 0.50f;
        /// <summary>消耗鲜美后，每份剩余鲜美换算的分数。</summary>
        public const int MagicScorePerRemainingFlavor = 3;

        /// <summary>
        /// 读取热辣倍率上限 / 遗物加成（预览与 HUD 共用）。
        /// </summary>
        public static void ResolveSpicyPreviewSettings(
            ResourceStore store,
            out float spicyMultiplierCap,
            out bool spicyUncapped,
            out float relicSpicyScoreMultiplierBonus)
        {
            spicyMultiplierCap = 0f;
            spicyUncapped = false;
            relicSpicyScoreMultiplierBonus = 0f;
            if (store != null)
            {
                var config = store.Config;
                if (config != null)
                    spicyMultiplierCap = config.SpicyMultiplierCap;
                else
                {
                    var loaded = Resources.Load<GameConfig>(ResourceStore.ResourcesConfigPath);
                    if (loaded != null)
                        spicyMultiplierCap = loaded.SpicyMultiplierCap;
                }
            }

            var relics = RelicManager.Instance;
            if (relics == null) return;

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
                        relicSpicyScoreMultiplierBonus += rule.FloatValue;
                }
            }
        }

        /// <summary>
        /// 烹饪区热辣倍率展示：与顶栏当前「已烹饪食材」「热辣」一致，便于心算核对。
        /// </summary>
        public static float PreviewSpicyMultiplierForDisplay(ResourceStore store = null)
        {
            store ??= ResourceStore.Instance;
            if (store == null) return 1f;

            ResolveSpicyPreviewSettings(
                store,
                out float spicyCap,
                out bool spicyUncapped,
                out float spicyScoreBonus);
            return ScoreMultiplierResolver.ComputeSpicyMultiplier(
                store,
                store.Cooked,
                spicyCap,
                spicyUncapped,
                spicyScoreBonus);
        }

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

            ResolveSpicyPreviewSettings(
                store,
                out float spicyCap,
                out bool spicyUncapped,
                out float spicyScoreBonus);

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
        /// sour uses warehouse cooked at turn end (回合结束结算口径).
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
                int cookedNet = RelicEffectRunner.ApplyCookOutputWaste(consumed);
                cookedFromCook += cookedNet;
                cookScoreBase += GameMath.CeilMul(cookedNet, scoreMultiplier);
            }

            preview.CookScoreBase = cookScoreBase;
            cookedThisTurn += cookedFromCook;
            preview.CookedThisTurn = cookedThisTurn;

            // 热辣仅在关卡最后一回合乘总分；预览只展示当前倍率。
            preview.SpicyMultiplier = PreviewSpicyMultiplierForDisplay(store);
            preview.SpicyBonusScore = 0;

            // Sour — 大关结算按当前已烹饪食材总量换算
            int cookedBasis = store.Cooked;
            preview.SourCookedBasis = cookedBasis;
            preview.SourUsable = Mathf.Min(store.Sour, cookedBasis);
            preview.SourScore = ScoreSour(preview.SourUsable, cookedBasis);

            // Magic
            preview.MagicHasCookWorkers = HasCookWorkers(elves);
            if (preview.MagicHasCookWorkers && store.Magic > 0)
            {
                int magic = store.Magic;
                int consumed = ComputeMagicConsumed(magic);
                int remaining = magic - consumed;
                int rawBonus = remaining * MagicScorePerRemainingFlavor;
                int bonus = ComputeMagicBonusScore(remaining, cookedThisTurn);

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
        /// 热辣在关卡最后一回合乘总分结算；回合内不再作用于烹饪分。
        /// </summary>
        [System.Obsolete("Spicy settles at level end; use TurnManager.TrySettleSpicyAtLevelEnd.")]
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
                store.Cooked,
                spicyMultiplierCap,
                spicyUncapped,
                relicSpicyScoreMultiplierBonus);
            int cookAfterSpicy = GameMath.CeilToInt(result.CookScoreBase * spicyMult);
            int delta = cookAfterSpicy - result.CookScoreBase;

            result.SpicyMultiplier = spicyMult;
            result.CookScore = cookAfterSpicy;
            result.ScoreGained += delta;
        }

        /// <summary>
        /// 大关 / 关底结算：按当前已烹饪食材总量换算酸涩分并消耗对应酸涩。
        /// </summary>
        public static void ResolveSourForSettlement(ResourceStore store, out int sourUsed, out int sourScore)
        {
            sourUsed = 0;
            sourScore = 0;
            if (store == null) return;

            int cookedBasis = store.Cooked;
            if (cookedBasis <= 0) return;

            int sour = store.Sour;
            if (sour <= 0) return;

            sourUsed = Mathf.Min(sour, cookedBasis);
            sourScore = ScoreSour(sourUsed, cookedBasis);
            if (sourUsed <= 0 || sourScore <= 0) return;

            store.ConsumeFlavorUpTo(FlavorType.Sour, sourUsed);
        }

        /// <summary>预览大关结算酸涩换分（只读，不消耗）。</summary>
        public static int PreviewSourScore(ResourceStore store)
        {
            if (store == null) return 0;

            int cookedBasis = store.Cooked;
            if (cookedBasis <= 0) return 0;

            int sour = store.Sour;
            if (sour <= 0) return 0;

            int sourUsed = Mathf.Min(sour, cookedBasis);
            return ScoreSour(sourUsed, cookedBasis);
        }

        /// <summary>预览酸涩分档明细，供烹饪区悬停提示。</summary>
        public static void PreviewSourDetail(
            ResourceStore store,
            out int cookedBasis,
            out int sourAmount,
            out int sourUsable,
            out int totalScore,
            out int tier1Count,
            out int tier1Score,
            out int tier2Count,
            out int tier2Score,
            out int tier3Count,
            out int tier3Score)
        {
            cookedBasis = store != null ? store.Cooked : 0;
            sourAmount = store != null ? store.Sour : 0;
            sourUsable = cookedBasis > 0 ? Mathf.Min(sourAmount, cookedBasis) : 0;
            ScoreSour(
                sourUsable,
                cookedBasis,
                out totalScore,
                out tier1Count,
                out tier1Score,
                out tier2Count,
                out tier2Score,
                out tier3Count,
                out tier3Score);
        }

        [System.Obsolete("Sour settles at stage end; use ResolveSourForSettlement.")]
        public static void ResolveSourForTurn(ResourceStore store, TurnResult result)
        {
            if (store == null || result == null) return;

            ResolveSourForSettlement(store, out int sourUsed, out int sourScore);
            if (sourUsed <= 0 || sourScore <= 0) return;

            result.SourUsed += sourUsed;
            result.SourScore += sourScore;
            result.ScoreGained += sourScore;
        }

        /// <summary>
        /// 兼容旧调用：按给定烹饪量结算酸涩（大关手动结算等）。
        /// </summary>
        public static void ResolveSourForSettlement(
            ResourceStore store,
            int cookedAmount,
            out int sourUsed,
            out int sourScore)
        {
            sourUsed = 0;
            sourScore = 0;
            if (store == null || cookedAmount <= 0) return;

            int sour = store.Sour;
            if (sour <= 0) return;

            sourUsed = Mathf.Min(sour, cookedAmount);
            sourScore = ScoreSour(sourUsed, cookedAmount);
            if (sourUsed > 0)
                store.ConsumeFlavorUpTo(FlavorType.Sour, sourUsed);
        }

        [System.Obsolete("Use ResolveSourForSettlement.")]
        public static void ResolveSour(ResourceStore store, TurnResult result)
        {
            ResolveSourForTurn(store, result);
        }

        /// <summary>
        /// Progressive sour→score conversion vs cooked food.
        /// Default tiers: &lt;=10% →3, &lt;=50% →2, &lt;=100% →1.
        /// Top-tier percent can be raised by relics (酸酸糖 → 30%).
        /// Second-tier ceiling can be raised (酸酸糖 → 70%).
        /// </summary>
        public static int ScoreSour(int sourAmount, int cookedAmount) =>
            ScoreSour(sourAmount, cookedAmount, out _, out _, out _, out _, out _, out _, out _);

        public static int ScoreSour(
            int sourAmount,
            int cookedAmount,
            out int totalScore,
            out int tier1Count,
            out int tier1Score,
            out int tier2Count,
            out int tier2Score,
            out int tier3Count,
            out int tier3Score)
        {
            totalScore = 0;
            tier1Count = tier1Score = tier2Count = tier2Score = tier3Count = tier3Score = 0;

            if (sourAmount <= 0 || cookedAmount <= 0) return 0;

            int topTierPercent = RelicEffectRunner.ResolveSourTopTierPercent(10);
            topTierPercent = Mathf.Clamp(topTierPercent, 1, 50);
            int secondTierPercent = RelicEffectRunner.ResolveSourSecondTierPercent(50);
            secondTierPercent = Mathf.Clamp(secondTierPercent, topTierPercent + 1, 99);

            int tier1End = GameMath.CeilDiv(cookedAmount * topTierPercent, 100);
            int tier2End = GameMath.CeilDiv(cookedAmount * secondTierPercent, 100);
            int tier3End = cookedAmount;                             // <=100%

            int remaining = Mathf.Min(sourAmount, cookedAmount);

            tier1Count = Mathf.Min(remaining, tier1End);
            tier1Score = tier1Count * 3;
            totalScore += tier1Score;
            remaining -= tier1Count;

            tier2Count = Mathf.Min(remaining, Mathf.Max(0, tier2End - tier1End));
            tier2Score = tier2Count * 2;
            totalScore += tier2Score;
            remaining -= tier2Count;

            tier3Count = Mathf.Min(remaining, Mathf.Max(0, tier3End - tier2End));
            tier3Score = tier3Count * 1;
            totalScore += tier3Score;

            return totalScore;
        }

        /// <summary>
        /// If any cook workers are assigned: consume 50% of magic (ceil),
        /// reduced by relics (科技与狠活), bonus = remaining ×3,
        /// capped by this turn's cooked ingredient count (not cook score).
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

            int bonus = ComputeMagicBonusScore(remaining, result.CookedGained);
            result.MagicConsumed += consumed;
            result.MagicScore += bonus;
            result.ScoreGained += bonus;
        }

        /// <summary>
        /// 鲜美得分 = min(剩余鲜美 × MagicScorePerRemainingFlavor, 本回合已烹饪食材份数)。
        /// </summary>
        public static int ComputeMagicBonusScore(int magicRemaining, int cookedGainedThisTurn)
        {
            if (magicRemaining <= 0 || cookedGainedThisTurn <= 0)
                return 0;
            int rawBonus = magicRemaining * MagicScorePerRemainingFlavor;
            return Mathf.Min(rawBonus, cookedGainedThisTurn);
        }

        /// <summary>
        /// Base magic consume rate is 50%; relics subtract from that rate in absolute terms
        /// (科技与狠活 0.2 → consume 30%).
        /// </summary>
        public static int ComputeMagicConsumed(int magic)
        {
            if (magic <= 0) return 0;
            float rate = MagicConsumeBaseRate - RelicEffectRunner.SumMagicConsumeReduction();
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
