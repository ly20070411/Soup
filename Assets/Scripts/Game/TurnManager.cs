using System;
using System.Collections.Generic;
using Soup.Employees;
using Soup.Events;
using Soup.Items;
using Soup.Jobs;
using Soup.Levels;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Advances one production turn: gather (uncapped) → process → warehouse clamp → cook.
    /// </summary>
    [DefaultExecutionOrder(-70)]
    public class TurnManager : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        private int _turnIndex;
        private int _score;
        private int _lastTurnCooked;
        private int _lastTurnScore;
        private int _lastWarehouseDelta;
        private bool _lastWarehouseOverflowed;
        private int _stageIndex = 1;
        private int _stageCooked;
        private int _scoreFromCook;
        private int _scoreFromSpicy;
        private int _scoreFromCold;
        private int _scoreFromSour;
        private int _scoreFromMagic;
        private GameSaveData _undoSnapshot;

        public static TurnManager Instance { get; private set; }

        public int TurnIndex => _turnIndex;
        public int Score => _score;
        public int LastTurnCooked => _lastTurnCooked;
        public int LastTurnScore => _lastTurnScore;
        public int LastWarehouseDelta => _lastWarehouseDelta;
        public bool LastWarehouseOverflowed => _lastWarehouseOverflowed;
        public int StageIndex => _stageIndex;
        public int StageCooked => _stageCooked;
        public int ScoreFromCook => _scoreFromCook;
        public int ScoreFromSpicy => _scoreFromSpicy;
        public int ScoreFromCold => _scoreFromCold;
        public int ScoreFromSour => _scoreFromSour;
        public int ScoreFromMagic => _scoreFromMagic;
        public bool CanUndo => _undoSnapshot != null;

        public event Action<TurnResult> TurnResolved;
        public event Action UndoApplied;
        public event Action<StageSettlementResult> StageSettled;

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
            ClearUndoSnapshot();
            _turnIndex = 0;
            _score = 0;
            _lastTurnCooked = 0;
            _lastTurnScore = 0;
            _stageIndex = 1;
            _stageCooked = 0;
            ClearScoreComposition();
            ResourceStore.Instance?.Clear();
            EmployeeManager.Instance?.ResetRun();
            ElfManager.Instance?.ResetFromConfig();
            RelicManager.Instance?.ResetRun();
            JobProgressionManager.Instance?.ResetRun();
            EventManager.Instance?.ResetRun();
            LevelManager.Instance?.ResetRun();
            ElfManager.Instance?.ClearAssignments();
        }

        public void ApplyState(
            int turnIndex,
            int score,
            int lastTurnCooked,
            int lastTurnScore,
            int stageIndex = 1,
            int stageCooked = 0,
            int scoreFromCook = 0,
            int scoreFromSpicy = 0,
            int scoreFromCold = 0,
            int scoreFromSour = 0,
            int scoreFromMagic = 0)
        {
            _turnIndex = Mathf.Max(0, turnIndex);
            _score = Mathf.Max(0, score);
            _lastTurnCooked = Mathf.Max(0, lastTurnCooked);
            _lastTurnScore = Mathf.Max(0, lastTurnScore);
            _stageIndex = Mathf.Max(1, stageIndex);
            _stageCooked = Mathf.Max(0, stageCooked);
            _scoreFromCook = Mathf.Max(0, scoreFromCook);
            _scoreFromSpicy = Mathf.Max(0, scoreFromSpicy);
            _scoreFromCold = Mathf.Max(0, scoreFromCold);
            _scoreFromSour = Mathf.Max(0, scoreFromSour);
            _scoreFromMagic = Mathf.Max(0, scoreFromMagic);
        }

        public void ClearUndoSnapshot() => _undoSnapshot = null;

        /// <summary>每关结束 / 进入新关：清空分数、食材与风味库存。</summary>
        public void ResetLevelScore()
        {
            ClearUndoSnapshot();
            _turnIndex = 0;
            _score = 0;
            _lastTurnCooked = 0;
            _lastTurnScore = 0;
            _lastWarehouseDelta = 0;
            _lastWarehouseOverflowed = false;
            _stageIndex = 1;
            _stageCooked = 0;
            ClearScoreComposition();
            ResourceStore.Instance?.ClearLevelStocks();
        }

        private void ClearScoreComposition()
        {
            _scoreFromCook = 0;
            _scoreFromSpicy = 0;
            _scoreFromCold = 0;
            _scoreFromSour = 0;
            _scoreFromMagic = 0;
        }

        /// <summary>Compact HUD line covering every score bucket (sums to total).</summary>
        public string FormatFlavorScoreComposition()
        {
            return
                $"烹饪+{_scoreFromCook}  热辣+{_scoreFromSpicy}  酸涩+{_scoreFromSour}  " +
                $"寒冷+{_scoreFromCold}  鲜美+{_scoreFromMagic}";
        }

        /// <summary>撤回上一回合（恢复「下一回合」点击前的完整局内状态）。</summary>
        public bool TryUndoPreviousTurn()
        {
            if (_undoSnapshot == null)
                return false;

            var snapshot = _undoSnapshot;
            _undoSnapshot = null;
            GameSaveService.Apply(snapshot);
            UndoApplied?.Invoke();
            return true;
        }

        /// <summary>
        /// 大关结算：酸涩按本关已烹饪食物占比换算分数后消耗。
        /// </summary>
        public StageSettlementResult SettleStage()
        {
            var store = ResourceStore.Instance;
            var result = new StageSettlementResult
            {
                StageIndex = _stageIndex,
                CookedInStage = _stageCooked
            };

            if (store != null)
            {
                FlavorResolver.ResolveSourForSettlement(
                    store,
                    _stageCooked,
                    out int sourUsed,
                    out int sourScore);
                result.SourUsed = sourUsed;
                result.SourScore = sourScore;
                result.ScoreGained = sourScore;
                _score += sourScore;
                _scoreFromSour += sourScore;
            }

            result.TotalScoreAfter = _score;
            _stageCooked = 0;
            _stageIndex++;
            ClearUndoSnapshot();
            StageSettled?.Invoke(result);
            return result;
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

            _undoSnapshot = GameSaveService.Capture();

            var result = new TurnResult { TurnIndex = _turnIndex + 1 };
            int rawBefore = store.TotalRaw;
            var gatherOutputs = new List<GatherTurnOutput>();
            var relicCtx = BuildRelicContext(store, result, gatherOutputs);

            RelicEffectRunner.Run(RelicTrigger.TurnStart, relicCtx);

            int solidBeforeGather = store.Solid;
            ResolveGather(elves, store, result, relicCtx, gatherOutputs);
            // Solid produced this gather batch (for 凑企鹅的祝福).
            relicCtx.SolidProducedThisBatch = Mathf.Max(0, store.Solid - solidBeforeGather);
            RelicEffectRunner.Run(RelicTrigger.AfterGather, relicCtx);

            ResolveProcess(elves, store, result);
            // Process may have consumed this-turn gather outputs; shrink tracked
            // remaining before warehouse overflow discard.
            ShrinkGatherOutputsToStore(gatherOutputs, store);
            EnforceWarehouseCapacity(store, result, gatherOutputs);
            FlavorResolver.ResolveCold(store, result);
            ResolveCook(elves, store, result);
            ApplyEndTurnRawWaste(store, result);

            RelicEffectRunner.Run(RelicTrigger.BeforeSpicy, relicCtx);
            float spicyMult = ScoreMultiplierResolver.ComputeSpicyMultiplier(
                store,
                result.CookedGained,
                relicCtx.SpicyMultiplierCap,
                relicCtx.SpicyUncapped,
                relicCtx.SpicyScoreMultiplierBonus);
            // 鲜美上限按热辣后、遗物/快乐坨坨前的烹饪分计算。
            result.CookScore = result.CookScoreBase > 0
                ? GameMath.CeilToInt(result.CookScoreBase * spicyMult)
                : 0;

            FlavorResolver.ResolveMagic(elves, store, result);

            RelicEffectRunner.Run(RelicTrigger.AfterScore, relicCtx);
            int cookFinal = ScoreMultiplierResolver.ApplyCookScoreMultipliers(result, relicCtx, spicyMult);
            result.ScoreGained += cookFinal;

            int cookPart = Mathf.Max(0, result.CookScoreBase);
            int spicyPart = Mathf.Max(0, cookFinal - result.CookScoreBase);
            int coldPart = Mathf.Max(0, result.ColdScore);
            int magicPart = Mathf.Max(0, result.MagicScore);
            int sourPart = Mathf.Max(0, result.SourScore);

            AccrueTurnScoreComposition(
                result.ScoreGained, cookPart, spicyPart, coldPart, magicPart, sourPart);

            JobProgressionManager.Instance?.TryGrantEndTurnIncentives();

            _turnIndex = result.TurnIndex;
            _lastTurnCooked = result.CookedGained;
            _lastTurnScore = result.ScoreGained;
            _lastWarehouseDelta = store.TotalRaw - rawBefore;
            _lastWarehouseOverflowed = result.RawDiscarded > 0
                || (store.WarehouseCapacity > 0 && store.TotalRaw >= store.WarehouseCapacity);
            _stageCooked += result.CookedGained;
            _score += result.ScoreGained;

            // Track unused warehouse for 苔藓 next turn.
            int unused = store.WarehouseSpace;
            if (unused == int.MaxValue)
                unused = 0;
            RelicManager.Instance?.RememberUnusedWarehouse(unused);

            TurnResolved?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Simulates gather → process → warehouse clamp → end-turn waste without mutating game state.
        /// Used by the gather warehouse preview plaque.
        /// </summary>
        public int PreviewWarehouseDelta(out bool wouldOverflow)
        {
            wouldOverflow = false;
            var store = ResourceStore.Instance;
            var elves = ElfManager.Instance;
            if (store == null || elves == null)
                return 0;

            var snapshot = store.CaptureSnapshot();
            store.PushSuppressChanged();
            try
            {
                int rawBefore = store.TotalRaw;
                var result = new TurnResult();
                var gatherOutputs = new List<GatherTurnOutput>();
                var relicCtx = BuildRelicContext(store, result, gatherOutputs);

                RelicEffectRunner.Run(RelicTrigger.TurnStart, relicCtx);
                ResolveGather(elves, store, result, relicCtx, gatherOutputs);
                RelicEffectRunner.Run(RelicTrigger.AfterGather, relicCtx);
                ResolveProcess(elves, store, result);
                ShrinkGatherOutputsToStore(gatherOutputs, store);
                EnforceWarehouseCapacity(store, result, gatherOutputs);
                ApplyEndTurnRawWaste(store, result);

                wouldOverflow = result.RawDiscarded > 0
                    || (store.WarehouseCapacity > 0 && store.TotalRaw >= store.WarehouseCapacity);
                return store.TotalRaw - rawBefore;
            }
            finally
            {
                store.RestoreSnapshot(snapshot);
                store.PopSuppressChanged();
            }
        }

        private void AccrueTurnScoreComposition(
            int gained,
            int cookPart,
            int spicyPart,
            int coldPart,
            int magicPart,
            int sourPart)
        {
            if (gained <= 0) return;

            int pre = cookPart + spicyPart + coldPart + magicPart + sourPart;
            if (pre <= 0)
            {
                // Relic-only or unexpected gain — fold into cook bucket for visibility.
                _scoreFromCook += gained;
                return;
            }

            if (gained == pre)
            {
                _scoreFromCook += cookPart;
                _scoreFromSpicy += spicyPart;
                _scoreFromCold += coldPart;
                _scoreFromMagic += magicPart;
                _scoreFromSour += sourPart;
                return;
            }

            // Final / independent multipliers scale the whole turn gain — keep ratios.
            int cook = (int)((long)cookPart * gained / pre);
            int spicy = (int)((long)spicyPart * gained / pre);
            int cold = (int)((long)coldPart * gained / pre);
            int magic = (int)((long)magicPart * gained / pre);
            int sour = (int)((long)sourPart * gained / pre);
            int remainder = gained - (cook + spicy + cold + magic + sour);

            // Put rounding remainder on the largest pre-mult part.
            int maxPart = cookPart;
            int maxBucket = 0; // 0 cook, 1 spicy, 2 cold, 3 magic, 4 sour
            if (spicyPart > maxPart) { maxPart = spicyPart; maxBucket = 1; }
            if (coldPart > maxPart) { maxPart = coldPart; maxBucket = 2; }
            if (magicPart > maxPart) { maxPart = magicPart; maxBucket = 3; }
            if (sourPart > maxPart) { maxBucket = 4; }

            switch (maxBucket)
            {
                case 1: spicy += remainder; break;
                case 2: cold += remainder; break;
                case 3: magic += remainder; break;
                case 4: sour += remainder; break;
                default: cook += remainder; break;
            }

            _scoreFromCook += cook;
            _scoreFromSpicy += spicy;
            _scoreFromCold += cold;
            _scoreFromMagic += magic;
            _scoreFromSour += sour;
        }

        private static RelicContext BuildRelicContext(
            ResourceStore store,
            TurnResult result,
            List<GatherTurnOutput> gatherOutputs)
        {
            var relics = RelicManager.Instance;
            var ctx = new RelicContext(store, result)
            {
                LevelTurnNumber = RelicManager.GetLevelTurnNumber(),
                PreviousUnusedWarehouse = relics != null ? relics.PreviousUnusedWarehouse : 0,
                ApplyYield = yield =>
                {
                    // Relic grants are not tied to a numbered gather station.
                    var bucket = GetOrCreateOutput(gatherOutputs, null, 0);
                    ApplyIngredientYield(store, result, yield, bucket);
                }
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

        private static void ResolveGather(
            ElfManager elves,
            ResourceStore store,
            TurnResult result,
            RelicContext relicCtx,
            List<GatherTurnOutput> gatherOutputs)
        {
            var em = EmployeeManager.Instance;
            var gatheredJobs = new List<JobItem>();
            foreach (var pair in GetJobLaborMap())
            {
                var job = pair.Key;
                float labor = pair.Value;
                if (job == null || labor <= 0f || job.JobType != JobType.Gather)
                    continue;

                // Gather count uses headcount (not efficiency). Efficiency scales final yield only.
                // Formula: 激励/疲惫 × 遗物 × (岗位自身 + 灯芯草/双尾蛇) × 快乐坨坨 × 劳动力/workers.
                // Spore relics key off gather units, so they must ignore efficiency.
                int workers = em != null
                    ? em.GetAssignedCountOnJob(job)
                    : GameMath.CeilToInt(labor);
                if (workers <= 0) continue;

                gatheredJobs.Add(job);

                var advancePath = JobProgressionManager.Instance != null
                    ? JobProgressionManager.Instance.GetAdvancePath(job)
                    : JobAdvanceNodeId.None;
                var advanceMods = JobAdvanceGatherMods.From(job, advancePath);
                int amountPerWorker = advanceMods.ResolveAmountPerWorker(job)
                    + RelicEffectRunner.SumGatherAmountPerWorkerBonus();

                int units = GameMath.CeilToInt(workers * (double)amountPerWorker);
                float eventYield = JobProgressionManager.Instance != null
                    ? JobProgressionManager.Instance.GetEventYieldMultiplier(job)
                    : 1f;
                if (eventYield > 0f && !Mathf.Approximately(eventYield, 1f))
                    units = GameMath.CeilToInt(units * eventYield);
                int snapUnusedSpace = store != null ? store.WarehouseSpace : 0;
                int snapWarehouseCapacity = store != null ? store.WarehouseCapacity : 0;
                int snapSolidStock = store != null ? store.Solid : 0;
                if (advanceMods.GatherUnitsPerProcessedThreshold > 0
                    && advanceMods.GatherUnitsPerProcessedAmount > 0
                    && store != null)
                {
                    int processed = Mathf.Max(0, store.Processed);
                    int threshold = advanceMods.GatherUnitsPerProcessedThreshold;
                    units += (processed / threshold) * advanceMods.GatherUnitsPerProcessedAmount;
                }

                if (units <= 0) continue;

                float efficiency = WorkEfficiencyResolver.ResolveGatherConversionEfficiency(
                    job, advanceMods, labor, workers);

                var bucket = GetOrCreateOutput(gatherOutputs, job, GetGatherJobNumber(job));

                if (job.OutputIngredient != null)
                {
                    int softBonus = advanceMods.SoftPerUnitBonus;
                    int maxWorkers = JobProgressionManager.Instance != null
                        ? JobProgressionManager.Instance.GetEffectiveMaxWorkers(job)
                        : job.GetEffectiveMaxWorkers(advancePath);
                    if (advanceMods.SoftPerUnitWhenFull > 0 && maxWorkers > 0 && workers >= maxWorkers)
                        softBonus += advanceMods.SoftPerUnitWhenFull;

                    var eventMods = JobProgressionManager.Instance?.GetEventMods(job);
                    int coldBonus = advanceMods.ColdPerUnitBonus
                                    + JobAdvanceGatherMods.SumOtherGatherColdAura(job);
                    int spicyBonus = advanceMods.SpicyPerUnitBonus
                                     + JobAdvanceGatherMods.SumOtherGatherSpicyAura(job);
                    int sourBonus = advanceMods.SourPerUnitBonus
                                    + JobAdvanceGatherMods.SumOtherGatherSourAura(job);
                    int magicBonus = advanceMods.MagicPerUnitBonus
                                     + JobAdvanceGatherMods.SumOtherGatherMagicAura(job);
                    int randomFlavorBonus = advanceMods.RandomFlavorPerUnitBonus
                                            + JobAdvanceGatherMods.SumOtherGatherRandomFlavorAura(job);
                    if (eventMods != null)
                    {
                        coldBonus += eventMods.ColdPerUnitDelta;
                        spicyBonus += eventMods.SpicyPerUnitDelta;
                        sourBonus += eventMods.SourPerUnitDelta;
                        magicBonus += eventMods.MagicPerUnitDelta;
                    }

                    if (advanceMods.HasVariant)
                    {
                        int baseUnits = 0;
                        int variantUnits = 0;
                        for (int i = 0; i < units; i++)
                        {
                            if (UnityEngine.Random.value < advanceMods.VariantChance)
                                variantUnits++;
                            else
                                baseUnits++;
                        }

                        ApplyGatherIngredientBatch(
                            store, result, relicCtx, bucket,
                            job.OutputIngredient, baseUnits,
                            softBonus, advanceMods.SolidPerUnitBonus, advanceMods.ToughPerUnitBonus,
                            coldBonus, spicyBonus, sourBonus, magicBonus, randomFlavorBonus, efficiency,
                            advanceMods);
                        ApplyGatherIngredientBatch(
                            store, result, relicCtx, bucket,
                            advanceMods.VariantIngredient, variantUnits,
                            softBonus, advanceMods.SolidPerUnitBonus, advanceMods.ToughPerUnitBonus,
                            coldBonus, spicyBonus, sourBonus, magicBonus, randomFlavorBonus, efficiency,
                            advanceMods);
                    }
                    else
                    {
                        ApplyGatherIngredientBatch(
                            store, result, relicCtx, bucket,
                            job.OutputIngredient, units,
                            softBonus, advanceMods.SolidPerUnitBonus, advanceMods.ToughPerUnitBonus,
                            coldBonus, spicyBonus, sourBonus, magicBonus, randomFlavorBonus, efficiency,
                            advanceMods);
                    }

                    ApplyFlatGatherBonuses(store, result, bucket, advanceMods);
                    ApplyWarehouseScaledSolidBonuses(
                        store, result, bucket, advanceMods,
                        snapUnusedSpace, snapWarehouseCapacity, snapSolidStock);
                    ApplySoftOrSolidConvert(store, result, bucket, advanceMods, units);
                    ApplyFlatRandomMaterialBonus(store, result, bucket, advanceMods);
                    ApplyBonusIngredient(store, result, relicCtx, bucket, advanceMods, efficiency);
                    ApplyTopStockBonuses(store, result, bucket, advanceMods);
                    if (advanceMods.NextTurnGatherEfficiencyPenalty > 0f)
                    {
                        var progression = JobProgressionManager.Instance;
                        if (progression != null)
                            progression.SetPendingGatherEfficiencyPenalty(
                                job, advanceMods.NextTurnGatherEfficiencyPenalty);
                    }
                    continue;
                }

                relicCtx?.RecordGatherUnitsOnly(units);
                ApplyLegacyGatherConversion(store, result, job, units, efficiency, bucket);
                ApplyFlatGatherBonuses(store, result, bucket, advanceMods);
                ApplyWarehouseScaledSolidBonuses(
                    store, result, bucket, advanceMods,
                    snapUnusedSpace, snapWarehouseCapacity, snapSolidStock);
                ApplySoftOrSolidConvert(store, result, bucket, advanceMods, units);
                ApplyFlatRandomMaterialBonus(store, result, bucket, advanceMods);
                ApplyBonusIngredient(store, result, relicCtx, bucket, advanceMods, efficiency);
                ApplyTopStockBonuses(store, result, bucket, advanceMods);
                if (advanceMods.NextTurnGatherEfficiencyPenalty > 0f && JobProgressionManager.Instance != null)
                    JobProgressionManager.Instance.SetPendingGatherEfficiencyPenalty(
                        job, advanceMods.NextTurnGatherEfficiencyPenalty);
            }

            ApplyDestroyedGatherGhostOutput(store, result, relicCtx, gatherOutputs);
            JobProgressionManager.Instance?.RecoverPendingGatherEfficiencyPenaltyForJobsNotGathered(
                gatheredJobs);
        }

        private static void ApplyTopStockBonuses(
            ResourceStore store,
            TurnResult result,
            GatherTurnOutput bucket,
            JobAdvanceGatherMods mods)
        {
            if (store == null) return;

            if (mods.TopFlavorBonus > 0)
            {
                var flavor = PickTopFlavor(store, out bool tied);
                int amount = tied && mods.TopFlavorTieBonus > 0
                    ? mods.TopFlavorTieBonus
                    : mods.TopFlavorBonus;
                store.AddFlavor(flavor, amount);
                result.FlavorGained += amount;
            }

            if (mods.TopMaterialBonus > 0)
            {
                var material = PickTopMaterial(store);
                var flat = new IngredientYield();
                switch (material)
                {
                    case IngredientMaterial.Tough:
                        flat.Tough = mods.TopMaterialBonus;
                        break;
                    case IngredientMaterial.Solid:
                        flat.Solid = mods.TopMaterialBonus;
                        break;
                    default:
                        flat.Soft = mods.TopMaterialBonus;
                        break;
                }

                ApplyIngredientYield(store, result, flat, bucket);
            }
        }

        private static FlavorType PickTopFlavor(ResourceStore store) =>
            PickTopFlavor(store, out _);

        private static FlavorType PickTopFlavor(ResourceStore store, out bool tied)
        {
            int spicy = store.Spicy;
            int sour = store.Sour;
            int cold = store.Cold;
            int magic = store.Magic;
            int max = Mathf.Max(Mathf.Max(spicy, sour), Mathf.Max(cold, magic));

            var candidates = new List<FlavorType>(4);
            if (spicy == max) candidates.Add(FlavorType.Spicy);
            if (sour == max) candidates.Add(FlavorType.Sour);
            if (cold == max) candidates.Add(FlavorType.Cold);
            if (magic == max) candidates.Add(FlavorType.Magic);
            if (candidates.Count == 0)
            {
                tied = false;
                return FlavorType.Spicy;
            }

            tied = candidates.Count > 1;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private static IngredientMaterial PickTopMaterial(ResourceStore store)
        {
            int soft = store.Soft;
            int tough = store.Tough;
            int solid = store.Solid;
            int max = Mathf.Max(soft, Mathf.Max(tough, solid));

            var tied = new List<IngredientMaterial>(3);
            if (soft == max) tied.Add(IngredientMaterial.Soft);
            if (tough == max) tied.Add(IngredientMaterial.Tough);
            if (solid == max) tied.Add(IngredientMaterial.Solid);
            if (tied.Count == 0) return IngredientMaterial.Soft;
            return tied[UnityEngine.Random.Range(0, tied.Count)];
        }

        private static void ApplyDestroyedGatherGhostOutput(
            ResourceStore store,
            TurnResult result,
            RelicContext relicCtx,
            List<GatherTurnOutput> gatherOutputs)
        {
            int perJob = JobAdvanceGatherMods.SumDestroyedJobsOutputPerTurn();
            if (perJob <= 0) return;

            var progression = JobProgressionManager.Instance;
            if (progression == null || progression.DestroyedGatherJobs.Count == 0) return;

            var sourceJob = JobAdvanceGatherMods.FindDestroyedOutputSourceJob();
            var ingredient = sourceJob != null ? sourceJob.OutputIngredient : null;
            if (ingredient == null) return;

            int destroyedCount = progression.DestroyedGatherJobs.Count;
            int units = perJob * destroyedCount;
            if (units <= 0) return;

            var bucket = sourceJob != null
                ? GetOrCreateOutput(gatherOutputs, sourceJob, GetGatherJobNumber(sourceJob))
                : null;

            ApplyGatherIngredientBatch(
                store, result, relicCtx, bucket,
                ingredient, units,
                0, 0, 0, 0, 0, 0, 0, 0, 1f);
        }

        private static void ApplyFlatGatherBonuses(
            ResourceStore store,
            TurnResult result,
            GatherTurnOutput bucket,
            JobAdvanceGatherMods mods)
        {
            if (mods.FlatSoftBonus == 0 && mods.FlatToughBonus == 0 && mods.FlatSolidBonus == 0
                && mods.FlatColdBonus == 0 && mods.FlatSpicyBonus == 0)
                return;
            var flat = new IngredientYield
            {
                Soft = Mathf.Max(0, mods.FlatSoftBonus),
                Tough = Mathf.Max(0, mods.FlatToughBonus),
                Solid = Mathf.Max(0, mods.FlatSolidBonus),
                Cold = Mathf.Max(0, mods.FlatColdBonus),
                Spicy = Mathf.Max(0, mods.FlatSpicyBonus)
            };
            if (flat.Soft <= 0 && flat.Tough <= 0 && flat.Solid <= 0
                && flat.Cold <= 0 && flat.Spicy <= 0)
                return;
            ApplyIngredientYield(store, result, flat, bucket);
        }

        /// <summary>
        /// 棍棍虫等：按采集前仓库空位 / 容量 / 坚固存量换算额外坚固。
        /// </summary>
        private static void ApplyWarehouseScaledSolidBonuses(
            ResourceStore store,
            TurnResult result,
            GatherTurnOutput bucket,
            JobAdvanceGatherMods mods,
            int unusedSpace,
            int warehouseCapacity,
            int solidStock)
        {
            if (store == null) return;

            int solid = 0;
            if (mods.SolidPerUnusedWarehouseThreshold > 0 && mods.SolidPerUnusedWarehouseAmount > 0)
            {
                if (unusedSpace > 0 && unusedSpace < int.MaxValue)
                    solid += (unusedSpace / mods.SolidPerUnusedWarehouseThreshold)
                             * mods.SolidPerUnusedWarehouseAmount;
            }

            if (mods.SolidPerWarehouseCapacityThreshold > 0 && mods.SolidPerWarehouseCapacityAmount > 0)
            {
                if (warehouseCapacity > 0)
                    solid += (warehouseCapacity / mods.SolidPerWarehouseCapacityThreshold)
                             * mods.SolidPerWarehouseCapacityAmount;
            }

            if (mods.SolidPerWarehouseSolidThreshold > 0 && mods.SolidPerWarehouseSolidAmount > 0)
            {
                int stock = Mathf.Max(0, solidStock);
                solid += (stock / mods.SolidPerWarehouseSolidThreshold)
                         * mods.SolidPerWarehouseSolidAmount;
            }

            if (solid <= 0) return;
            ApplyIngredientYield(store, result, new IngredientYield { Solid = solid }, bucket);
        }

        private static void ApplyBonusIngredient(
            ResourceStore store,
            TurnResult result,
            RelicContext relicCtx,
            GatherTurnOutput bucket,
            JobAdvanceGatherMods mods,
            float _)
        {
            if (!mods.HasBonusIngredient) return;
            ApplyGatherIngredientBatch(
                store, result, relicCtx, bucket,
                mods.BonusIngredient, mods.BonusIngredientAmount,
                0, 0, 0, 0, 0, 0, 0, 0, 1f);
        }

        /// <summary>
        /// 每份采集物消耗柔软或坚固，成功则额外获得强韧（及可选最多风味）。
        /// 单种材质必须独自凑够消耗量；优先消耗存量更多的一方。
        /// </summary>
        private static void ApplySoftOrSolidConvert(
            ResourceStore store,
            TurnResult result,
            GatherTurnOutput bucket,
            JobAdvanceGatherMods mods,
            int units)
        {
            if (store == null || !mods.HasSoftOrSolidConvert || units <= 0) return;

            int cost = mods.ConvertConsumeSoftOrSolidPerUnit;
            int gainTough = 0;
            int gainFlavor = 0;

            for (int i = 0; i < units; i++)
            {
                var prefer = store.Soft >= store.Solid
                    ? IngredientMaterial.Soft
                    : IngredientMaterial.Solid;
                var fallback = prefer == IngredientMaterial.Soft
                    ? IngredientMaterial.Solid
                    : IngredientMaterial.Soft;

                IngredientMaterial spent;
                if (store.GetRaw(prefer) >= cost)
                    spent = prefer;
                else if (store.GetRaw(fallback) >= cost)
                    spent = fallback;
                else
                    continue;

                if (!store.TryConsumeRaw(spent, cost))
                    continue;

                gainTough += mods.ConvertGainToughPerUnit;
                gainFlavor += mods.ConvertGainTopFlavorPerUnit;
            }

            if (gainTough > 0)
            {
                var flat = new IngredientYield { Tough = gainTough };
                ApplyIngredientYield(store, result, flat, bucket);
            }

            if (gainFlavor > 0)
            {
                var flavor = PickTopFlavor(store);
                store.AddFlavor(flavor, gainFlavor);
                result.FlavorGained += gainFlavor;
            }
        }

        private static void ApplyGatherIngredientBatch(
            ResourceStore store,
            TurnResult result,
            RelicContext relicCtx,
            GatherTurnOutput bucket,
            IngredientItem ingredient,
            int units,
            int softPerUnitBonus,
            int solidPerUnitBonus,
            int toughPerUnitBonus,
            int coldPerUnitBonus,
            int spicyPerUnitBonus,
            int sourPerUnitBonus,
            int magicPerUnitBonus,
            int randomFlavorPerUnitBonus,
            float efficiency,
            JobAdvanceGatherMods forceMods = default)
        {
            if (ingredient == null || units <= 0) return;

            relicCtx?.RecordGather(ingredient, units);
            var yield = IngredientYieldResolver.FromIngredient(ingredient, units);
            if (softPerUnitBonus != 0)
                yield.Soft = Mathf.Max(0, yield.Soft + softPerUnitBonus * units);
            if (solidPerUnitBonus != 0)
                yield.Solid = Mathf.Max(0, yield.Solid + solidPerUnitBonus * units);
            if (toughPerUnitBonus != 0)
                yield.Tough = Mathf.Max(0, yield.Tough + toughPerUnitBonus * units);
            if (coldPerUnitBonus != 0)
                yield.Cold = Mathf.Max(0, yield.Cold + coldPerUnitBonus * units);
            if (spicyPerUnitBonus != 0)
                yield.Spicy = Mathf.Max(0, yield.Spicy + spicyPerUnitBonus * units);
            if (sourPerUnitBonus != 0)
                yield.Sour = Mathf.Max(0, yield.Sour + sourPerUnitBonus * units);
            if (magicPerUnitBonus != 0)
                yield.Magic = Mathf.Max(0, yield.Magic + magicPerUnitBonus * units);
            if (randomFlavorPerUnitBonus != 0)
                yield.RandomFlavor = Mathf.Max(0, yield.RandomFlavor + randomFlavorPerUnitBonus * units);

            ApplyEventYieldTweaks(bucket != null ? bucket.Job : null, units, ref yield);

            if (forceMods.SuppressRawMaterialOutput)
            {
                yield.Soft = 0;
                yield.Tough = 0;
                yield.Solid = 0;
                yield.RandomMaterial = 0;
            }

            if (forceMods.HasForcedOutputMaterial)
                yield = CollapseYieldToSingleMaterial(yield, forceMods.ForcedOutputMaterial);

            yield = yield.ScaledByEfficiency(efficiency);

            float flavorBonus = JobAdvanceGatherMods.SumIncomingDesignatedPairFlavorYieldBonus(
                bucket != null ? bucket.Job : null);
            if (flavorBonus > 0f)
                yield = yield.ScaledFlavorsBy(1f + flavorBonus);

            ApplyIngredientYield(store, result, yield, bucket);
        }

        private static void ApplyEventYieldTweaks(JobItem job, int units, ref IngredientYield yield)
        {
            if (job == null || units <= 0) return;
            var progression = JobProgressionManager.Instance;
            if (progression == null) return;
            var mods = progression.GetEventMods(job);
            if (mods == null) return;

            if (mods.RawPerUnitDelta != 0)
            {
                int delta = mods.RawPerUnitDelta * units;
                if (yield.Soft > 0)
                    yield.Soft = Mathf.Max(0, yield.Soft + delta);
                if (yield.Tough > 0)
                    yield.Tough = Mathf.Max(0, yield.Tough + delta);
                if (yield.Solid > 0)
                    yield.Solid = Mathf.Max(0, yield.Solid + delta);
            }

            if (mods.ProduceAllFourFlavors && yield.RandomFlavor > 0)
            {
                int all = yield.RandomFlavor;
                yield.RandomFlavor = 0;
                yield.Spicy += all;
                yield.Sour += all;
                yield.Cold += all;
                yield.Magic += all;
            }
        }

        private static IngredientYield CollapseYieldToSingleMaterial(
            IngredientYield yield,
            IngredientMaterial material)
        {
            int total = yield.Soft + yield.Tough + yield.Solid + Mathf.Max(0, yield.RandomMaterial);
            yield.Soft = 0;
            yield.Tough = 0;
            yield.Solid = 0;
            yield.RandomMaterial = 0;
            switch (material)
            {
                case IngredientMaterial.Tough:
                    yield.Tough = total;
                    break;
                case IngredientMaterial.Solid:
                    yield.Solid = total;
                    break;
                default:
                    yield.Soft = total;
                    break;
            }

            return yield;
        }

        private static void ApplyFlatRandomMaterialBonus(
            ResourceStore store,
            TurnResult result,
            GatherTurnOutput bucket,
            JobAdvanceGatherMods mods)
        {
            if (store == null || mods.FlatRandomMaterialBonus <= 0) return;
            var flat = new IngredientYield { RandomMaterial = mods.FlatRandomMaterialBonus };
            ApplyIngredientYield(store, result, flat, bucket);
        }

        private static Dictionary<JobItem, float> GetJobLaborMap()
        {
            if (EmployeeManager.Instance != null)
                return EmployeeManager.Instance.GetLaborByJob();

            var fallback = new Dictionary<JobItem, float>();
            var elves = ElfManager.Instance;
            if (elves == null) return fallback;
            foreach (var pair in elves.GetAssignments())
            {
                if (pair.Key != null && pair.Value > 0)
                    fallback[pair.Key] = pair.Value;
            }

            return fallback;
        }

        public static void ApplyIngredientYield(
            ResourceStore store,
            TurnResult result,
            IngredientYield yield)
        {
            ApplyIngredientYield(store, result, yield, null);
        }

        private static void ApplyIngredientYield(
            ResourceStore store,
            TurnResult result,
            IngredientYield yield,
            GatherTurnOutput bucket)
        {
            int soft = store.AddRaw(IngredientMaterial.Soft, yield.Soft);
            int tough = store.AddRaw(IngredientMaterial.Tough, yield.Tough);
            int solid = store.AddRaw(IngredientMaterial.Solid, yield.Solid);

            int randomStored = 0;
            int randomSoft = 0, randomTough = 0, randomSolid = 0;
            int randomWanted = Mathf.Max(0, yield.RandomMaterial);
            for (int i = 0; i < randomWanted; i++)
            {
                var mat = (IngredientMaterial)UnityEngine.Random.Range(0, 3); // Soft / Tough / Solid
                int added = store.AddRaw(mat, 1);
                randomStored += added;
                switch (mat)
                {
                    case IngredientMaterial.Soft: randomSoft += added; break;
                    case IngredientMaterial.Tough: randomTough += added; break;
                    case IngredientMaterial.Solid: randomSolid += added; break;
                }
            }

            int rawStored = soft + tough + solid + randomStored;
            result.RawGained += rawStored;

            if (bucket != null)
            {
                bucket.Soft += soft + randomSoft;
                bucket.Tough += tough + randomTough;
                bucket.Solid += solid + randomSolid;
            }

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
            int units,
            float efficiency,
            GatherTurnOutput bucket)
        {
            int rawBase = units * job.MaterialPerGatherUnit;
            int rawWanted = GameMath.CeilMul(rawBase, efficiency);
            int rawStored = store.AddRaw(job.GatherMaterial, rawWanted);
            result.RawGained += rawStored;

            if (bucket != null && rawStored > 0)
            {
                switch (job.GatherMaterial)
                {
                    case IngredientMaterial.Soft: bucket.Soft += rawStored; break;
                    case IngredientMaterial.Tough: bucket.Tough += rawStored; break;
                    case IngredientMaterial.Solid: bucket.Solid += rawStored; break;
                }
            }

            int spicy = GameMath.CeilMul(units * job.SpicyPerGatherUnit, efficiency);
            int sour = GameMath.CeilMul(units * job.SourPerGatherUnit, efficiency);
            int cold = GameMath.CeilMul(units * job.ColdPerGatherUnit, efficiency);
            int magic = GameMath.CeilMul(units * job.MagicPerGatherUnit, efficiency);
            if (spicy > 0) store.AddFlavor(FlavorType.Spicy, spicy);
            if (sour > 0) store.AddFlavor(FlavorType.Sour, sour);
            if (cold > 0) store.AddFlavor(FlavorType.Cold, cold);
            if (magic > 0) store.AddFlavor(FlavorType.Magic, magic);
            result.FlavorGained += spicy + sour + cold + magic;
        }

        /// <summary>
        /// After process consumed from the shared pool, reduce tracked this-turn
        /// gather remaining so it cannot exceed what is still in the store.
        /// Prefer attributing consumption to lower-numbered stations first, so
        /// higher-numbered stations keep discard priority.
        /// </summary>
        private static void ShrinkGatherOutputsToStore(
            List<GatherTurnOutput> gatherOutputs,
            ResourceStore store)
        {
            if (gatherOutputs == null || gatherOutputs.Count == 0 || store == null)
                return;

            if (gatherOutputs.Count > 1)
                gatherOutputs.Sort((a, b) => a.Number.CompareTo(b.Number));

            ShrinkMaterialToStore(gatherOutputs, IngredientMaterial.Soft, store.Soft);
            ShrinkMaterialToStore(gatherOutputs, IngredientMaterial.Tough, store.Tough);
            ShrinkMaterialToStore(gatherOutputs, IngredientMaterial.Solid, store.Solid);
        }

        private static void ShrinkMaterialToStore(
            List<GatherTurnOutput> gatherOutputs,
            IngredientMaterial material,
            int availableInStore)
        {
            int attributed = 0;
            for (int i = 0; i < gatherOutputs.Count; i++)
            {
                var output = gatherOutputs[i];
                if (output == null || output.Job == null) continue;
                attributed += GetOutputMaterial(output, material);
            }

            int excess = attributed - Mathf.Max(0, availableInStore);
            if (excess <= 0) return;

            // Lowest number first: treat process as consuming earlier stations first.
            for (int i = 0; i < gatherOutputs.Count && excess > 0; i++)
            {
                var output = gatherOutputs[i];
                if (output == null || output.Job == null) continue;

                int have = GetOutputMaterial(output, material);
                if (have <= 0) continue;

                int take = Mathf.Min(have, excess);
                SetOutputMaterial(output, material, have - take);
                excess -= take;
            }
        }

        private static int GetOutputMaterial(GatherTurnOutput output, IngredientMaterial material)
        {
            switch (material)
            {
                case IngredientMaterial.Soft: return output.Soft;
                case IngredientMaterial.Tough: return output.Tough;
                case IngredientMaterial.Solid: return output.Solid;
                default: return 0;
            }
        }

        private static void SetOutputMaterial(GatherTurnOutput output, IngredientMaterial material, int value)
        {
            value = Mathf.Max(0, value);
            switch (material)
            {
                case IngredientMaterial.Soft: output.Soft = value; break;
                case IngredientMaterial.Tough: output.Tough = value; break;
                case IngredientMaterial.Solid: output.Solid = value; break;
            }
        }

        /// <summary>
        /// After process: if raw exceeds warehouse, discard this-turn gather outputs
        /// from highest-numbered station first, materials Solid → Tough → Soft.
        /// Flavors are never discarded. Stations with zero this-turn output are skipped.
        /// Unattributed (relic) grants are not treated as numbered gather stations.
        /// </summary>
        private static void EnforceWarehouseCapacity(
            ResourceStore store,
            TurnResult result,
            List<GatherTurnOutput> gatherOutputs)
        {
            if (store == null) return;

            int cap = store.WarehouseCapacity;
            if (cap <= 0) return;

            int overflow = store.TotalRaw - cap;
            if (overflow <= 0 || gatherOutputs == null || gatherOutputs.Count == 0)
                return;

            bool convertWaste = RelicEffectRunner.WasteConvertMultiplier() > 0f;
            float wasteMult = RelicEffectRunner.WasteConvertMultiplier();
            if (!convertWaste)
            {
                // 回收器等：浪费减少 X% → 只丢弃 overflow 的 (1 - reduction)。
                float wasteKept = RelicEffectRunner.SumWarehouseWasteReduction();
                if (wasteKept > 0f)
                    overflow = GameMath.CeilToInt(overflow * (1.0 - wasteKept));
                if (overflow <= 0) return;
            }

            if (gatherOutputs.Count > 1)
                gatherOutputs.Sort((a, b) => b.Number.CompareTo(a.Number));

            int discardedBefore = result != null ? result.RawDiscarded : 0;
            for (int i = 0; i < gatherOutputs.Count && overflow > 0; i++)
            {
                var output = gatherOutputs[i];
                // Only numbered gather stations that produced this turn.
                if (output == null || output.Job == null || output.TotalRaw <= 0)
                    continue;

                overflow -= DiscardFromOutput(store, result, output, overflow);
            }

            if (convertWaste && result != null)
            {
                int converted = result.RawDiscarded - discardedBefore;
                if (converted > 0)
                {
                    int gained = GameMath.CeilToInt(converted * wasteMult);
                    store.AddProcessed(gained);
                    result.ProcessedGained += gained;
                }
            }
        }

        private static int DiscardFromOutput(
            ResourceStore store,
            TurnResult result,
            GatherTurnOutput output,
            int overflow)
        {
            if (overflow <= 0 || output == null) return 0;

            int discarded = 0;
            discarded += DiscardTracked(store, result, ref output.Solid, IngredientMaterial.Solid, overflow - discarded);
            discarded += DiscardTracked(store, result, ref output.Tough, IngredientMaterial.Tough, overflow - discarded);
            discarded += DiscardTracked(store, result, ref output.Soft, IngredientMaterial.Soft, overflow - discarded);
            return discarded;
        }

        private static int DiscardTracked(
            ResourceStore store,
            TurnResult result,
            ref int remainingInOutput,
            IngredientMaterial material,
            int need)
        {
            if (need <= 0 || remainingInOutput <= 0) return 0;
            int take = Mathf.Min(need, remainingInOutput, store.GetRaw(material));
            if (take <= 0) return 0;
            store.TryConsumeRaw(material, take);
            remainingInOutput -= take;
            result.RawDiscarded += take;
            return take;
        }

        private static GatherTurnOutput GetOrCreateOutput(
            List<GatherTurnOutput> outputs,
            JobItem job,
            int number)
        {
            if (outputs == null) return null;

            for (int i = 0; i < outputs.Count; i++)
            {
                var existing = outputs[i];
                if (existing == null) continue;
                if (job == null)
                {
                    if (existing.Job == null && existing.Number == number)
                        return existing;
                }
                else if (ReferenceEquals(existing.Job, job))
                {
                    return existing;
                }
            }

            var created = new GatherTurnOutput { Job = job, Number = number };
            outputs.Add(created);
            return created;
        }

        /// <summary>
        /// 1-based gather-job index in map order (DisplayName sort), matching JobWorldMap grid.
        /// </summary>
        public static int GetGatherJobNumber(JobItem job)
        {
            if (job == null) return 0;

            var jobs = JobManager.Instance != null
                ? JobManager.Instance.FindByType(JobType.Gather)
                : null;
            if (jobs == null || jobs.Count == 0) return 0;

            jobs.Sort((a, b) =>
            {
                string na = a != null ? a.DisplayName : string.Empty;
                string nb = b != null ? b.DisplayName : string.Empty;
                return string.CompareOrdinal(na, nb);
            });

            for (int i = 0; i < jobs.Count; i++)
            {
                if (ReferenceEquals(jobs[i], job))
                    return i + 1;
            }

            return 0;
        }

        private static void ResolveProcess(ElfManager elves, ResourceStore store, TurnResult result)
        {
            var processJobs = new List<KeyValuePair<JobItem, float>>();
            foreach (var pair in GetJobLaborMap())
            {
                var job = pair.Key;
                float labor = pair.Value;
                if (job == null || labor <= 0f || job.JobType != JobType.Process)
                    continue;
                processJobs.Add(pair);
            }

            // Higher processPriority settles first. Random/explosion jobs always last
            // among equal priorities so specialized stations consume materials first.
            processJobs.Sort((a, b) =>
            {
                int cmp = b.Key.ProcessPriority.CompareTo(a.Key.ProcessPriority);
                if (cmp != 0) return cmp;

                bool aRandom = IsExplosionProcessJob(a.Key);
                bool bRandom = IsExplosionProcessJob(b.Key);
                if (aRandom != bRandom)
                    return aRandom ? 1 : -1;

                return string.CompareOrdinal(a.Key.Id, b.Key.Id);
            });

            for (int i = 0; i < processJobs.Count; i++)
            {
                var job = processJobs[i].Key;
                float labor = processJobs[i].Value;
                var advancePath = JobProgressionManager.Instance != null
                    ? JobProgressionManager.Instance.GetAdvancePath(job)
                    : JobAdvanceNodeId.None;
                var advanceMods = JobAdvanceProcessMods.From(job, advancePath);

                float capacityMult = WorkEfficiencyResolver.ResolveWorkCapacityMultiplier(
                    job, WorkEfficiencyScope.Process);
                int amountPerWorker = advanceMods.ResolveAmountPerWorker(job);
                int capacity = GameMath.CeilToInt(labor * amountPerWorker * capacityMult);
                if (capacity <= 0) continue;

                int produced = IsExplosionProcessJob(job)
                    ? ProcessExplosion(store, capacity)
                    : ProcessPreferredThenOther(store, job, capacity, advanceMods);

                if (produced > 0
                    && advanceMods.MaterialRefundPerProcessedThreshold > 0
                    && advanceMods.MaterialRefundPerProcessedAmount > 0)
                {
                    int refund = (produced / advanceMods.MaterialRefundPerProcessedThreshold)
                                 * advanceMods.MaterialRefundPerProcessedAmount;
                    if (refund > 0)
                        store.AddRaw(advanceMods.MaterialRefundMaterial, refund);
                }

                if (produced > 0
                    && advanceMods.ProcessedRefundPerProcessedThreshold > 0
                    && advanceMods.ProcessedRefundPerProcessedAmount > 0)
                {
                    int refund = (produced / advanceMods.ProcessedRefundPerProcessedThreshold)
                                 * advanceMods.ProcessedRefundPerProcessedAmount;
                    if (refund > 0)
                    {
                        store.AddProcessed(refund);
                        result.ProcessedGained += refund;
                    }
                }

                if (produced > 0 && advanceMods.ProcessedOutputWasteFraction > 0f)
                {
                    int wasted = Mathf.FloorToInt(produced * advanceMods.ProcessedOutputWasteFraction);
                    if (wasted > 0)
                    {
                        float wasteMult = RelicEffectRunner.WasteConvertMultiplier();
                        if (wasteMult > 0f)
                            produced += GameMath.CeilToInt(wasted * wasteMult);
                        else
                            produced = Mathf.Max(0, produced - wasted);
                    }
                }

                if (produced > 0)
                {
                    var employees = EmployeeManager.Instance;
                    if (employees != null)
                    {
                        int eaten = employees.ComputeOwnProcessedConsumed(job, produced);
                        if (eaten > 0)
                        {
                            produced -= eaten;
                            result.ProcessedEatenByWorkers += eaten;
                        }
                    }
                }

                if (produced > 0)
                {
                    store.AddProcessed(produced);
                    result.ProcessedGained += produced;
                }
            }
        }

        private static bool IsExplosionProcessJob(JobItem job)
        {
            return job != null
                   && job.JobType == JobType.Process
                   && (job.ProcessRandom || job.PreferredMaterial == IngredientMaterial.Any);
        }

        private static int ProcessPreferredThenOther(
            ResourceStore store,
            JobItem job,
            int capacity,
            JobAdvanceProcessMods mods)
        {
            int produced = 0;
            var preferred = job.PreferredMaterial;

            int takePref = store.ConsumeRawUpTo(preferred, capacity);
            produced += takePref;
            int remaining = capacity - takePref;
            if (remaining <= 0)
                return produced;

            float efficiency = mods.ResolveOtherMaterialEfficiency(job);
            if (efficiency <= 0f)
                return produced;

            int otherBudget = GameMath.CeilToInt(remaining * efficiency);
            otherBudget = ConsumeMaterialsInOrder(store, BuildMaterialOrder(preferred, preferExcludeFirst: true), otherBudget);
            produced += otherBudget;
            return produced;
        }

        /// <summary>
        /// Explosion: each processed unit randomly picks among Soft / Tough / Solid
        /// that still have stock (no Soft→Tough→Solid fixed order).
        /// </summary>
        private static int ProcessExplosion(ResourceStore store, int capacity)
        {
            if (store == null || capacity <= 0) return 0;

            int produced = 0;
            var available = new List<IngredientMaterial>(3);
            for (int i = 0; i < capacity; i++)
            {
                available.Clear();
                if (store.Soft > 0) available.Add(IngredientMaterial.Soft);
                if (store.Tough > 0) available.Add(IngredientMaterial.Tough);
                if (store.Solid > 0) available.Add(IngredientMaterial.Solid);
                if (available.Count == 0)
                    break;

                var pick = available[UnityEngine.Random.Range(0, available.Count)];
                if (store.TryConsumeRaw(pick, 1))
                    produced++;
            }

            return produced;
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

            foreach (var pair in GetJobLaborMap())
            {
                var job = pair.Key;
                float labor = pair.Value;
                if (job == null || labor <= 0f || job.JobType != JobType.Cook)
                    continue;

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

                int consumed = store.ConsumeProcessedUpTo(demand);
                if (consumed <= 0) continue;

                store.AddCooked(consumed);
                int scoreGain = GameMath.CeilMul(consumed, scoreMultiplier);
                result.CookedGained += consumed;
                result.ProcessedConsumed += consumed;
                cookScoreBase += scoreGain;
            }

            result.CookScoreBase = cookScoreBase;
            result.CookScore = cookScoreBase;
        }

        /// <summary>
        /// 烹饪结束后按进阶效果浪费仓库未处理食材（如大角兽 2-2 的 25%）。
        /// </summary>
        private static void ApplyEndTurnRawWaste(ResourceStore store, TurnResult result)
        {
            if (store == null) return;

            float fraction = JobAdvanceGatherMods.MaxEndTurnRawWasteFraction();
            if (fraction <= 0f) return;

            int total = store.TotalRaw;
            if (total <= 0) return;

            int toWaste = GameMath.CeilToInt(total * fraction);
            if (toWaste <= 0) return;

            int discarded = ConsumeMaterialsInOrder(
                store,
                new[]
                {
                    IngredientMaterial.Soft,
                    IngredientMaterial.Tough,
                    IngredientMaterial.Solid
                },
                toWaste);
            if (discarded > 0)
            {
                result.RawDiscarded += discarded;
                float wasteMult = RelicEffectRunner.WasteConvertMultiplier();
                if (wasteMult > 0f)
                {
                    int gained = GameMath.CeilToInt(discarded * wasteMult);
                    store.AddProcessed(gained);
                    result.ProcessedGained += gained;
                }
            }
        }
    }

    /// <summary>
    /// Per gather station (or unattributed relic) raw materials produced this turn.
    /// Used for warehouse overflow discard priority.
    /// </summary>
    internal sealed class GatherTurnOutput
    {
        public JobItem Job;
        public int Number;
        public int Soft;
        public int Tough;
        public int Solid;

        public int TotalRaw => Soft + Tough + Solid;
    }

    [Serializable]
    public class StageSettlementResult
    {
        public int StageIndex;
        public int CookedInStage;
        public int SourUsed;
        public int SourScore;
        public int ScoreGained;
        public int TotalScoreAfter;

        public override string ToString()
        {
            return
                $"大关 {StageIndex} 结算: 本关烹饪 {CookedInStage}, " +
                $"酸涩 {SourUsed}→+{SourScore} 分, 总分 {TotalScoreAfter}";
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
        public int ProcessedEatenByWorkers;
        public int ProcessedConsumed;
        public int CookedGained;
        public int ScoreGained;

        public int CookScoreBase;
        public int CookScore;
        public float SpicyMultiplier = 1f;
        public float FinalMultiplier = 1f;
        public float IndependentMultiplier = 1f;
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
                $"flavor+{FlavorGained}, processed+{ProcessedGained}" +
                (ProcessedEatenByWorkers > 0 ? $"(被吃{ProcessedEatenByWorkers})" : string.Empty) + ", " +
                $"cook {ProcessedConsumed}→{CookedGained}, " +
                $"score+{ScoreGained} (cook {CookScoreBase}→{CookScore}×{SpicyMultiplier:0.##}, " +
                $"final×{FinalMultiplier:0.##}×{IndependentMultiplier:0.##}, " +
                $"cold {ColdScore}, sour {SourScore}, magic {MagicScore})";
        }
    }
}
