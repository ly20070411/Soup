using System;
using System.Collections.Generic;
using Soup.Items;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Advances one production turn: gather → process → cook.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class TurnManager : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        private int _turnIndex;
        private int _score;
        private int _lastTurnCooked;
        private int _lastTurnScore;

        public static TurnManager Instance { get; private set; }

        public int TurnIndex => _turnIndex;
        public int Score => _score;
        public int LastTurnCooked => _lastTurnCooked;
        public int LastTurnScore => _lastTurnScore;

        public event Action<TurnResult> TurnResolved;

        public static void Initialize()
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(TurnManager));
                Instance = go.AddComponent<TurnManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ResetRun()
        {
            _turnIndex = 0;
            _score = 0;
            _lastTurnCooked = 0;
            _lastTurnScore = 0;
            ResourceStore.Instance?.Clear();
            ElfManager.Instance?.ResetFromConfig();
            RelicManager.Instance?.ResetRun();
            JobProgressionManager.Instance?.ResetRun();
            ElfManager.Instance?.ClearAssignments();
        }

        /// <summary>点击「下一回合」时调用。</summary>
        public TurnResult NextTurn()
        {
            var elves = ElfManager.Instance;
            var store = ResourceStore.Instance;
            if (elves == null || store == null)
            {
                Debug.LogError("[TurnManager] ElfManager / ResourceStore missing.");
                return TurnResult.Empty;
            }

            var result = new TurnResult { TurnIndex = _turnIndex + 1 };
            var relicCtx = BuildRelicContext(store, result);

            ResolveGather(elves, store, result, relicCtx);
            RelicEffectRunner.Run(RelicTrigger.AfterGather, relicCtx);

            ResolveProcess(elves, store, result);
            FlavorResolver.ResolveCold(store, result);
            ResolveCook(elves, store, result);

            RelicEffectRunner.Run(RelicTrigger.BeforeSpicy, relicCtx);
            FlavorResolver.ApplySpicyToCookScore(
                store,
                result,
                relicCtx.SpicyMultiplierCap,
                relicCtx.SpicyUncapped);

            FlavorResolver.ResolveSour(store, result);
            FlavorResolver.ResolveMagic(elves, store, result);

            RelicEffectRunner.Run(RelicTrigger.AfterScore, relicCtx);
            ApplyFinalMultiplier(result, relicCtx);

            _turnIndex = result.TurnIndex;
            _lastTurnCooked = result.CookedGained;
            _lastTurnScore = result.ScoreGained;
            _score += result.ScoreGained;

            TurnResolved?.Invoke(result);
            return result;
        }

        private static RelicContext BuildRelicContext(ResourceStore store, TurnResult result)
        {
            var ctx = new RelicContext(store, result)
            {
                ApplyYield = yield => ApplyIngredientYield(store, result, yield)
            };

            float cap = 3f;
            var config = store.Config;
            if (config != null)
                cap = config.SpicyMultiplierCap;
            else
            {
                var loaded = Resources.Load<GameConfig>(ResourceStore.ResourcesConfigPath);
                if (loaded != null)
                    cap = loaded.SpicyMultiplierCap;
            }

            ctx.SpicyMultiplierCap = cap;
            return ctx;
        }

        private static void ApplyFinalMultiplier(TurnResult result, RelicContext relicCtx)
        {
            if (result == null || relicCtx == null) return;

            float mult = Mathf.Max(0f, relicCtx.FinalMultiplier);
            result.FinalMultiplier = mult;
            if (Mathf.Approximately(mult, 1f) || result.ScoreGained == 0)
                return;

            int before = result.ScoreGained;
            result.ScoreGained = GameMath.CeilToInt(before * mult);
        }

        private static void ResolveGather(
            ElfManager elves,
            ResourceStore store,
            TurnResult result,
            RelicContext relicCtx)
        {
            foreach (var pair in elves.GetAssignments())
            {
                var job = pair.Key;
                int workers = pair.Value;
                if (job == null || workers <= 0 || job.JobType != JobType.Gather)
                    continue;

                int units = workers * job.GatherAmountPerWorker;
                if (units <= 0) continue;

                if (job.OutputIngredient != null)
                {
                    relicCtx?.RecordGather(job.OutputIngredient, units);
                    ApplyIngredientYield(
                        store,
                        result,
                        IngredientYieldResolver.FromIngredient(job.OutputIngredient, units));
                    continue;
                }

                relicCtx?.RecordGatherUnitsOnly(units);
                ApplyLegacyGatherConversion(store, result, job, units);
            }
        }

        public static void ApplyIngredientYield(
            ResourceStore store,
            TurnResult result,
            IngredientYield yield)
        {
            int soft = store.TryAddRaw(IngredientMaterial.Soft, yield.Soft);
            int tough = store.TryAddRaw(IngredientMaterial.Tough, yield.Tough);
            int solid = store.TryAddRaw(IngredientMaterial.Solid, yield.Solid);

            int randomStored = 0;
            int randomWanted = Mathf.Max(0, yield.RandomMaterial);
            for (int i = 0; i < randomWanted; i++)
            {
                var mat = (IngredientMaterial)UnityEngine.Random.Range(0, 3); // Soft / Tough / Solid
                randomStored += store.TryAddRaw(mat, 1);
            }

            int rawWanted = yield.TotalFixedRaw + randomWanted;
            int rawStored = soft + tough + solid + randomStored;
            result.RawGained += rawStored;
            result.RawDiscarded += Mathf.Max(0, rawWanted - rawStored);

            if (yield.Spicy > 0) store.AddFlavor(FlavorType.Spicy, yield.Spicy);
            if (yield.Sour > 0) store.AddFlavor(FlavorType.Sour, yield.Sour);
            if (yield.Cold > 0) store.AddFlavor(FlavorType.Cold, yield.Cold);
            if (yield.Magic > 0) store.AddFlavor(FlavorType.Magic, yield.Magic);

            int randomFlavor = Mathf.Max(0, yield.RandomFlavor);
            for (int i = 0; i < randomFlavor; i++)
            {
                var flavor = (FlavorType)UnityEngine.Random.Range(0, 4); // Spicy / Sour / Cold / Magic
                store.AddFlavor(flavor, 1);
            }

            result.FlavorGained += yield.TotalFixedFlavor + randomFlavor;
        }

        private static void ApplyLegacyGatherConversion(
            ResourceStore store,
            TurnResult result,
            JobItem job,
            int units)
        {
            int rawWanted = units * job.MaterialPerGatherUnit;
            int rawStored = store.TryAddRaw(job.GatherMaterial, rawWanted);
            result.RawGained += rawStored;
            result.RawDiscarded += Mathf.Max(0, rawWanted - rawStored);

            int spicy = units * job.SpicyPerGatherUnit;
            int sour = units * job.SourPerGatherUnit;
            int cold = units * job.ColdPerGatherUnit;
            int magic = units * job.MagicPerGatherUnit;
            if (spicy > 0) store.AddFlavor(FlavorType.Spicy, spicy);
            if (sour > 0) store.AddFlavor(FlavorType.Sour, sour);
            if (cold > 0) store.AddFlavor(FlavorType.Cold, cold);
            if (magic > 0) store.AddFlavor(FlavorType.Magic, magic);
            result.FlavorGained += spicy + sour + cold + magic;
        }

        private static void ResolveProcess(ElfManager elves, ResourceStore store, TurnResult result)
        {
            foreach (var pair in elves.GetAssignments())
            {
                var job = pair.Key;
                int workers = pair.Value;
                if (!IsStandardProcessJob(job, workers))
                    continue;

                int capacity = workers * job.ProcessAmountPerWorker;
                if (capacity <= 0) continue;

                int produced = ProcessPreferredThenOther(store, job, capacity);
                if (produced > 0)
                {
                    store.AddProcessed(produced);
                    result.ProcessedGained += produced;
                }
            }

            foreach (var pair in elves.GetAssignments())
            {
                var job = pair.Key;
                int workers = pair.Value;
                if (!IsExplosionProcessJob(job, workers))
                    continue;

                int capacity = workers * job.ProcessAmountPerWorker;
                if (capacity <= 0) continue;

                int produced = ProcessExplosion(elves, store, capacity);
                if (produced > 0)
                {
                    store.AddProcessed(produced);
                    result.ProcessedGained += produced;
                }
            }
        }

        private static bool IsStandardProcessJob(JobItem job, int workers)
        {
            return job != null
                   && workers > 0
                   && job.JobType == JobType.Process
                   && !job.ProcessRandom
                   && job.PreferredMaterial != IngredientMaterial.Any;
        }

        private static bool IsExplosionProcessJob(JobItem job, int workers)
        {
            return job != null
                   && workers > 0
                   && job.JobType == JobType.Process
                   && (job.ProcessRandom || job.PreferredMaterial == IngredientMaterial.Any);
        }

        private static int ProcessPreferredThenOther(ResourceStore store, JobItem job, int capacity)
        {
            int produced = 0;
            var preferred = job.PreferredMaterial;

            int takePref = store.ConsumeRawUpTo(preferred, capacity);
            produced += takePref;
            int remaining = capacity - takePref;
            if (remaining <= 0)
                return produced;

            float efficiency = Mathf.Clamp01(job.OtherMaterialEfficiency);
            if (efficiency <= 0f)
                return produced;

            int otherBudget = GameMath.CeilToInt(remaining * efficiency);
            otherBudget = ConsumeMaterialsInOrder(store, BuildMaterialOrder(preferred, preferExcludeFirst: true), otherBudget);
            produced += otherBudget;
            return produced;
        }

        private static int ProcessExplosion(ElfManager elves, ResourceStore store, int capacity)
        {
            bool softEasy = false, toughEasy = false, solidEasy = false;
            foreach (var pair in elves.GetAssignments())
            {
                if (!IsStandardProcessJob(pair.Key, pair.Value))
                    continue;

                switch (pair.Key.PreferredMaterial)
                {
                    case IngredientMaterial.Soft: softEasy = true; break;
                    case IngredientMaterial.Tough: toughEasy = true; break;
                    case IngredientMaterial.Solid: solidEasy = true; break;
                }
            }

            var order = new List<IngredientMaterial>(3);
            if (!softEasy) order.Add(IngredientMaterial.Soft);
            if (!toughEasy) order.Add(IngredientMaterial.Tough);
            if (!solidEasy) order.Add(IngredientMaterial.Solid);
            if (softEasy) order.Add(IngredientMaterial.Soft);
            if (toughEasy) order.Add(IngredientMaterial.Tough);
            if (solidEasy) order.Add(IngredientMaterial.Solid);

            return ConsumeMaterialsInOrder(store, order, capacity);
        }

        private static List<IngredientMaterial> BuildMaterialOrder(
            IngredientMaterial exclude,
            bool preferExcludeFirst)
        {
            var order = new List<IngredientMaterial>(3);
            var all = new[]
            {
                IngredientMaterial.Soft,
                IngredientMaterial.Tough,
                IngredientMaterial.Solid
            };

            if (preferExcludeFirst && exclude != IngredientMaterial.Any)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != exclude)
                        order.Add(all[i]);
                }
            }
            else
            {
                order.AddRange(all);
            }

            return order;
        }

        private static int ConsumeMaterialsInOrder(
            ResourceStore store,
            IList<IngredientMaterial> order,
            int budget)
        {
            if (budget <= 0 || order == null) return 0;

            int taken = 0;
            for (int i = 0; i < order.Count && taken < budget; i++)
            {
                int got = store.ConsumeRawUpTo(order[i], budget - taken);
                taken += got;
            }

            return taken;
        }

        private static void ResolveCook(ElfManager elves, ResourceStore store, TurnResult result)
        {
            int cookScoreBase = 0;

            foreach (var pair in elves.GetAssignments())
            {
                var job = pair.Key;
                int workers = pair.Value;
                if (job == null || workers <= 0 || job.JobType != JobType.Cook)
                    continue;

                int demand = workers * job.CookAmountPerWorker;
                if (demand <= 0) continue;

                int consumed = store.ConsumeProcessedUpTo(demand);
                if (consumed <= 0) continue;

                store.AddCooked(consumed);
                int scoreGain = GameMath.CeilToInt(consumed * job.ScoreMultiplier);
                result.CookedGained += consumed;
                result.ProcessedConsumed += consumed;
                cookScoreBase += scoreGain;
            }

            result.CookScoreBase = cookScoreBase;
            result.CookScore = cookScoreBase;
            result.ScoreGained += cookScoreBase;
        }
    }

    [Serializable]
    public class TurnResult
    {
        public static TurnResult Empty => new TurnResult();

        public int TurnIndex;
        public int RawGained;
        public int RawDiscarded;
        public int FlavorGained;
        public int ProcessedGained;
        public int ProcessedConsumed;
        public int CookedGained;
        public int ScoreGained;

        public int CookScoreBase;
        public int CookScore;
        public float SpicyMultiplier = 1f;
        public float FinalMultiplier = 1f;
        public int ColdUsed;
        public int ColdScore;
        public int SourUsed;
        public int SourScore;
        public int MagicConsumed;
        public int MagicScore;

        public override string ToString()
        {
            return
                $"Turn {TurnIndex}: raw+{RawGained} (lost {RawDiscarded}), " +
                $"flavor+{FlavorGained}, processed+{ProcessedGained}, " +
                $"cook {ProcessedConsumed}→{CookedGained}, " +
                $"score+{ScoreGained} (cook {CookScoreBase}→{CookScore}×{SpicyMultiplier:0.##}, " +
                $"final×{FinalMultiplier:0.##}, " +
                $"cold {ColdScore}, sour {SourScore}, magic {MagicScore})";
        }
    }
}
