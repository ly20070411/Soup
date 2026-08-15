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
        private int _stageIndex = 1;
        private int _stageCooked;
        private GameSaveData _undoSnapshot;

        public static TurnManager Instance { get; private set; }

        public int TurnIndex => _turnIndex;
        public int Score => _score;
        public int LastTurnCooked => _lastTurnCooked;
        public int LastTurnScore => _lastTurnScore;
        public int StageIndex => _stageIndex;
        public int StageCooked => _stageCooked;
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

        public void ResetRun(bool restartLevel = true)
        {
            ClearUndoSnapshot();
            _turnIndex = 0;
            _score = 0;
            _lastTurnCooked = 0;
            _lastTurnScore = 0;
            _stageIndex = 1;
            _stageCooked = 0;
            ResourceStore.Instance?.Clear();
            EmployeeManager.Instance?.ResetRun();
            JobModifierManager.Instance?.ResetRun();
            ElfManager.Instance?.ResetFromConfig();
            RelicManager.Instance?.ResetRun();
            JobProgressionManager.Instance?.ResetRun();
            EventManager.Instance?.ResetRun();
            LevelManager.Instance?.ResetRun(restartLevel);
            ElfManager.Instance?.ClearAssignments();
        }

        public void ApplyState(
            int turnIndex,
            int score,
            int lastTurnCooked,
            int lastTurnScore,
            int stageIndex = 1,
            int stageCooked = 0)
        {
            _turnIndex = Mathf.Max(0, turnIndex);
            _score = Mathf.Max(0, score);
            _lastTurnCooked = Mathf.Max(0, lastTurnCooked);
            _lastTurnScore = Mathf.Max(0, lastTurnScore);
            _stageIndex = Mathf.Max(1, stageIndex);
            _stageCooked = Mathf.Max(0, stageCooked);
        }

        public void ClearUndoSnapshot() => _undoSnapshot = null;

        /// <summary>每关开始时清空总分（本关得分从 0 重新累计）。</summary>
        public void ResetLevelScore()
        {
            ClearUndoSnapshot();
            _score = 0;
            _lastTurnCooked = 0;
            _lastTurnScore = 0;
            _stageCooked = 0;
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

            RelicEffectRunner.Run(RelicTrigger.BeforeSpicy, relicCtx);
            FlavorResolver.ApplySpicyToCookScore(
                store,
                result,
                relicCtx.SpicyMultiplierCap,
                relicCtx.SpicyUncapped);

            // Sour is settled only at stage (大关) end — see SettleStage().
            FlavorResolver.ResolveMagic(elves, store, result);

            RelicEffectRunner.Run(RelicTrigger.AfterScore, relicCtx);
            ApplyFinalMultiplier(result, relicCtx);

            _turnIndex = result.TurnIndex;
            _lastTurnCooked = result.CookedGained;
            _lastTurnScore = result.ScoreGained;
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
        /// 不改变状态的基础产能预览。随机食材只统计总量；遗物的最终乘区在实际结算中另行显示。
        /// </summary>
        public TurnCapacityPreview PreviewNextTurnCapacity()
        {
            var preview = new TurnCapacityPreview();
            var store = ResourceStore.Instance;
            var modifiers = JobModifierManager.Instance;

            foreach (var pair in GetJobLaborMap())
            {
                var job = pair.Key;
                float labor = pair.Value;
                if (job == null || labor <= 0f) continue;
                if (modifiers != null && modifiers.IsDisabled(job)) continue;

                switch (job.JobType)
                {
                    case JobType.Gather:
                    {
                        float yieldMult = modifiers != null ? modifiers.GetYieldMultiplier(job) : 1f;
                        int units = GameMath.CeilToInt(labor * job.GatherAmountPerWorker * yieldMult);
                        if (job.OutputIngredient != null)
                        {
                            var yield = IngredientYieldResolver.FromIngredient(job.OutputIngredient, units);
                            preview.GatherRaw += yield.TotalFixedRaw + yield.RandomMaterial;
                            preview.GatherFlavor += yield.TotalFlavor;
                        }
                        else
                        {
                            preview.GatherRaw += units * job.MaterialPerGatherUnit;
                            preview.GatherFlavor += units * (
                                job.SpicyPerGatherUnit
                                + job.SourPerGatherUnit
                                + job.ColdPerGatherUnit
                                + job.MagicPerGatherUnit);
                        }

                        if (modifiers != null
                            && modifiers.TryGetBonusFlavor(job, out _, out int fixedFlavor))
                            preview.GatherFlavor += fixedFlavor;
                        break;
                    }
                    case JobType.Process:
                        preview.ProcessCapacity += GameMath.CeilToInt(labor * job.ProcessAmountPerWorker);
                        break;
                    case JobType.Cook:
                    {
                        int demand = GameMath.CeilToInt(labor * job.CookAmountPerWorker);
                        preview.CookCapacity += demand;
                        preview.CookScoreAtCapacity += GameMath.CeilMul(demand, job.ScoreMultiplier);
                        break;
                    }
                }
            }

            if (store != null && store.WarehouseCapacity > 0)
            {
                int rawAfterBestCaseProcess = Mathf.Max(
                    0,
                    store.TotalRaw + preview.GatherRaw - preview.ProcessCapacity);
                preview.OverflowRisk = Mathf.Max(0, rawAfterBestCaseProcess - store.WarehouseCapacity);
            }

            return preview;
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

        private static void ApplyFinalMultiplier(TurnResult result, RelicContext relicCtx)
        {
            if (result == null || relicCtx == null) return;

            float mult = Mathf.Max(0f, relicCtx.FinalMultiplier);
            float independent = Mathf.Max(0f, relicCtx.IndependentMultiplier);
            result.FinalMultiplier = mult;
            result.IndependentMultiplier = independent;

            if (result.ScoreGained == 0) return;
            if (Mathf.Approximately(mult, 1f) && Mathf.Approximately(independent, 1f))
                return;

            int before = result.ScoreGained;
            result.ScoreGained = GameMath.CeilToInt(before * mult * independent);
        }

        private static void ResolveGather(
            ElfManager elves,
            ResourceStore store,
            TurnResult result,
            RelicContext relicCtx,
            List<GatherTurnOutput> gatherOutputs)
        {
            var modifiers = JobModifierManager.Instance;
            foreach (var pair in GetJobLaborMap())
            {
                var job = pair.Key;
                float labor = pair.Value;
                if (job == null || labor <= 0f || job.JobType != JobType.Gather)
                    continue;

                if (modifiers != null && modifiers.IsDisabled(job))
                    continue;

                // 进阶专属事件可调整采集产量（如 孢子感染：蘑菇产量增加 30%）。
                float yieldMult = modifiers != null ? modifiers.GetYieldMultiplier(job) : 1f;
                int units = GameMath.CeilToInt(labor * job.GatherAmountPerWorker * yieldMult);
                if (units <= 0) continue;

                // 进阶专属事件的额外风味（如 冷笑话：风味产量加 10 点）。
                if (modifiers != null
                    && modifiers.TryGetBonusFlavor(job, out var bonusFlavor, out int flavorPerUnit))
                {
                    // 事件数值按“该岗位本回合有产出时固定增加”结算，避免随高产岗位失控。
                    int bonusFlavorAmount = flavorPerUnit;
                    store.AddFlavor(bonusFlavor, bonusFlavorAmount);
                    result.FlavorGained += bonusFlavorAmount;
                }

                var bucket = GetOrCreateOutput(gatherOutputs, job, GetGatherJobNumber(job));

                if (job.OutputIngredient != null)
                {
                    relicCtx?.RecordGather(job.OutputIngredient, units);
                    ApplyIngredientYield(
                        store,
                        result,
                        IngredientYieldResolver.FromIngredient(job.OutputIngredient, units),
                        bucket);
                    continue;
                }

                relicCtx?.RecordGatherUnitsOnly(units);
                ApplyLegacyGatherConversion(store, result, job, units, bucket);
            }
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
            GatherTurnOutput bucket)
        {
            int rawWanted = units * job.MaterialPerGatherUnit;
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

            if (gatherOutputs.Count > 1)
                gatherOutputs.Sort((a, b) => b.Number.CompareTo(a.Number));

            for (int i = 0; i < gatherOutputs.Count && overflow > 0; i++)
            {
                var output = gatherOutputs[i];
                // Only numbered gather stations that produced this turn.
                if (output == null || output.Job == null || output.TotalRaw <= 0)
                    continue;

                overflow -= DiscardFromOutput(store, result, output, overflow);
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
                int capacity = GameMath.CeilToInt(labor * job.ProcessAmountPerWorker);
                if (capacity <= 0) continue;

                int produced = IsExplosionProcessJob(job)
                    ? ProcessExplosion(store, capacity)
                    : ProcessPreferredThenOther(store, job, capacity);

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

                int demand = GameMath.CeilToInt(labor * job.CookAmountPerWorker);
                if (demand <= 0) continue;

                int consumed = store.ConsumeProcessedUpTo(demand);
                if (consumed <= 0) continue;

                store.AddCooked(consumed);
                int scoreGain = GameMath.CeilMul(consumed, job.ScoreMultiplier);
                result.CookedGained += consumed;
                result.ProcessedConsumed += consumed;
                cookScoreBase += scoreGain;
            }

            result.CookScoreBase = cookScoreBase;
            result.CookScore = cookScoreBase;
            result.ScoreGained += cookScoreBase;
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
    public class TurnCapacityPreview
    {
        public int GatherRaw;
        public int GatherFlavor;
        public int ProcessCapacity;
        public int CookCapacity;
        public int CookScoreAtCapacity;
        public int OverflowRisk;

        public override string ToString()
        {
            string overflow = OverflowRisk > 0 ? $"；溢出风险至少 {OverflowRisk}" : string.Empty;
            return $"产能预览：采集原料 +{GatherRaw} / 风味 +{GatherFlavor}；" +
                   $"处理最多 {ProcessCapacity}；烹饪最多 {CookCapacity}（基础分上限 {CookScoreAtCapacity}）{overflow}";
        }
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
        public int SpicyUsed;
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
                $"spicy used {SpicyUsed}, cold {ColdScore}, sour {SourScore}, magic {MagicScore})";
        }
    }
}
