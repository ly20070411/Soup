using System;
using System.Collections.Generic;
using Soup.Employees;
using Soup.Game;
using Soup.Items;
using Soup.Jobs;
using Soup.Levels;
using Soup.Relics;
using UnityEngine;

namespace Soup.Events
{
    /// <summary>
    /// Presents random events (族长的激励): turn-end random draws with cooldown,
    /// stage settlement, level-clear exclusive events, and option effects.
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public class EventManager : MonoBehaviour
    {
        public const string ResourcesDatabasePath = "EventDatabase";
        public const string ResourcesConfigPath = "GameConfig";

        [SerializeField] private EventDatabase database;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private EventItem _pending;
        private int _cooldownTurnsLeft;
        private bool _subscribedTurns;
        private readonly List<string> _queuedEventIds = new List<string>();
        private readonly HashSet<string> _seenEventIds = new HashSet<string>();

        public static EventManager Instance { get; private set; }

        public EventDatabase Database => database;

        public IReadOnlyList<EventItem> All =>
            database != null ? database.Events : System.Array.Empty<EventItem>();

        /// <summary>族长的激励累计值（事件选项增减）。</summary>
        public int ChiefIncentive { get; private set; }

        public bool HasPendingEvent => _pending != null;

        public EventItem PendingEvent => _pending;

        public bool HasPendingEventSequence => _pending != null || _queuedEventIds.Count > 0;

        public bool EnableTurnEndEvents => ReadConfig(cfg => cfg.EnableTurnEndEvents, false);

        public int EventCooldownTurns => ReadConfig(cfg => cfg.EventCooldownTurns, 1);

        public float TurnEndEventChance => ReadConfig(cfg => cfg.TurnEndEventChance, 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            Initialize();
        }

        public static void Initialize(EventDatabase db = null)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(EventManager));
                Instance = go.AddComponent<EventManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            if (db != null)
                Instance.database = db;
            if (Instance.database != null)
                Instance.database.RebuildIndex();
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

            if (database == null)
                database = Resources.Load<EventDatabase>(ResourcesDatabasePath);

            database?.RebuildIndex();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            // TurnManager may be created after us; retry subscription until it exists.
            if (!_subscribedTurns)
                TrySubscribeTurns();
        }

        private void TrySubscribeTurns()
        {
            var turns = TurnManager.Instance;
            if (turns == null) return;

            turns.TurnResolved -= OnTurnResolved;
            turns.TurnResolved += OnTurnResolved;
            turns.StageSettled -= OnStageSettled;
            turns.StageSettled += OnStageSettled;
            _subscribedTurns = true;
        }

        private void OnEnable() => TrySubscribeTurns();

        private void OnDisable()
        {
            var turns = TurnManager.Instance;
            if (turns != null)
            {
                turns.TurnResolved -= OnTurnResolved;
                turns.StageSettled -= OnStageSettled;
            }

            _subscribedTurns = false;
        }

        // ------------------------------------------------------------- presenting

        public bool Present(EventItem item)
        {
            if (item == null || database == null || !database.Contains(item)) return false;
            _pending = item;
            return true;
        }

        public bool PresentById(string id)
        {
            if (HasPendingEvent) return false;
            return Present(database != null ? database.GetById(id) : null);
        }

        /// <summary>兼容旧入口：按正式顺序排入固定事件和普通事件。</summary>
        public bool NotifyLevelCleared(int oneBasedLevelIndex)
        {
            return QueueLevelClearEvents(oneBasedLevelIndex);
        }

        /// <summary>
        /// 奖励选择后调用：固定关卡事件优先，再补足本关普通事件；同一事件本局不重复。
        /// </summary>
        public bool QueueLevelClearEvents(int oneBasedLevelIndex)
        {
            if (database == null) return false;

            int before = _queuedEventIds.Count + (HasPendingEvent ? 1 : 0);
            var fixedEvents = CollectEligible(item => item.TriggerLevelIndex == oneBasedLevelIndex);
            for (int i = 0; i < fixedEvents.Count; i++)
                Enqueue(fixedEvents[i]);

            int configuredCount = ReadConfig(cfg => cfg.StageEndEventCount, 0);
            int desiredTotal = Mathf.Max(configuredCount, fixedEvents.Count);
            int queuedForThisClear = fixedEvents.Count;
            var general = CollectEligible(item => item.TriggerLevelIndex == 0);
            var rng = new System.Random(911 + oneBasedLevelIndex * 131 + _seenEventIds.Count * 17);
            while (queuedForThisClear < desiredTotal && general.Count > 0)
            {
                int index = rng.Next(general.Count);
                var item = general[index];
                general.RemoveAt(index);
                if (Enqueue(item))
                    queuedForThisClear++;
            }

            PresentNextQueued();
            return _queuedEventIds.Count + (HasPendingEvent ? 1 : 0) > before;
        }

        /// <summary>Resolve the pending event by picking an option; applies its effects.</summary>
        public bool ResolvePendingOption(int optionIndex)
        {
            if (_pending == null || _pending.Options == null) return false;
            var options = _pending.Options;
            if (optionIndex < 0 || optionIndex >= options.Count) return false;

            string resolvedId = _pending.Id;
            var effects = options[optionIndex].Effects;
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                    ApplyEffect(effects[i]);
            }

            _seenEventIds.Add(resolvedId);
            _pending = null;
            StartCooldown();
            PresentNextQueued();
            return true;
        }

        /// <summary>Dismiss the pending event without picking (no cooldown reset).</summary>
        public void DismissPending()
        {
            _pending = null;
            PresentNextQueued();
        }

        private bool Enqueue(EventItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) return false;
            if (_seenEventIds.Contains(item.Id)) return false;
            if (_pending != null && _pending.Id == item.Id) return false;
            if (_queuedEventIds.Contains(item.Id)) return false;
            _queuedEventIds.Add(item.Id);
            return true;
        }

        private void PresentNextQueued()
        {
            if (_pending != null || database == null) return;
            while (_queuedEventIds.Count > 0)
            {
                string id = _queuedEventIds[0];
                _queuedEventIds.RemoveAt(0);
                var item = database.GetById(id);
                if (item == null || _seenEventIds.Contains(id) || !IsJobRequirementMet(item))
                    continue;
                _pending = item;
                return;
            }
        }

        public int GetCooldownTurnsRemaining() => Mathf.Max(0, _cooldownTurnsLeft);

        private void StartCooldown()
        {
            _cooldownTurnsLeft = EventCooldownTurns;
        }

        // ---------------------------------------------------------------- effects

        private void ApplyEffect(EventItem.EventEffect effect)
        {
            if (effect == null) return;
            if (UnityEngine.Random.value > effect.Chance) return;

            switch (effect.Kind)
            {
                case EventItem.EffectKind.GrantRelic:
                {
                    var relics = RelicManager.Instance;
                    if (relics == null) break;
                    int count = Mathf.Max(1, effect.IntAmount);
                    for (int i = 0; i < count; i++)
                        relics.AcquireById(effect.TargetId);
                    break;
                }

                case EventItem.EffectKind.GrantEmployee:
                {
                    if (effect.IntAmount == 0) break;
                    if (effect.IntAmount > 0)
                        EmployeeManager.Instance?.Add(effect.TargetId, effect.IntAmount);
                    else
                    {
                        var type = EmployeeManager.Instance != null
                            ? EmployeeManager.Instance.GetById(effect.TargetId)
                            : null;
                        if (type != null)
                        {
                            int owned = EmployeeManager.Instance.GetOwned(type);
                            EmployeeManager.Instance.SetOwned(
                                effect.TargetId,
                                Mathf.Max(0, owned + effect.IntAmount));
                        }
                    }

                    break;
                }

                case EventItem.EffectKind.ModifyElves:
                {
                    if (effect.IntAmount != 0)
                        ElfManager.Instance?.AddElves(effect.IntAmount);
                    break;
                }

                case EventItem.EffectKind.ModifyJobYield:
                {
                    var job = ResolveJob(effect.TargetId);
                    var modifiers = JobModifierManager.Instance;
                    if (job == null || modifiers == null) break;
                    modifiers.SetYieldMultiplier(job, modifiers.GetYieldMultiplier(job) * effect.FloatAmount);
                    break;
                }

                case EventItem.EffectKind.ModifyJobCapacity:
                {
                    var job = ResolveJob(effect.TargetId);
                    if (job == null || JobModifierManager.Instance == null) break;
                    JobModifierManager.Instance.AddCapacityBonus(job, effect.IntAmount);
                    break;
                }

                case EventItem.EffectKind.DisableJob:
                {
                    var job = ResolveJob(effect.TargetId);
                    if (job == null || JobModifierManager.Instance == null) break;
                    JobModifierManager.Instance.SetDisabled(job, true);
                    break;
                }

                case EventItem.EffectKind.ModifyWarehouse:
                {
                    ResourceStore.Instance?.AddWarehouseCapacityBonus(effect.IntAmount);
                    break;
                }

                case EventItem.EffectKind.ModifyJobFlavor:
                {
                    var job = ResolveJob(effect.TargetId);
                    if (job == null || JobModifierManager.Instance == null) break;
                    if (TryParseFlavor(effect.SecondTargetId, out var flavor)
                        || TryGetJobFlavor(job, out flavor))
                    {
                        JobModifierManager.Instance.SetBonusFlavor(job, flavor, effect.IntAmount);
                    }

                    break;
                }
            }
        }

        private static JobItem ResolveJob(string jobId)
        {
            return JobManager.Instance != null ? JobManager.Instance.GetById(jobId) : null;
        }

        private static bool TryParseFlavor(string text, out FlavorType flavor)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "spicy":
                case "热辣":
                case "辣":
                    flavor = FlavorType.Spicy;
                    return true;
                case "sour":
                case "酸涩":
                case "酸":
                    flavor = FlavorType.Sour;
                    return true;
                case "cold":
                case "寒冷":
                case "冰":
                    flavor = FlavorType.Cold;
                    return true;
                case "magic":
                case "鲜美":
                    flavor = FlavorType.Magic;
                    return true;
                default:
                    flavor = FlavorType.Spicy;
                    return false;
            }
        }

        /// <summary>风味修饰落到岗位默认产出风味上（如 冰晶果 → 寒冷）。</summary>
        private static bool TryGetJobFlavor(JobItem job, out FlavorType flavor)
        {
            if (job != null && job.OutputIngredient != null
                && IngredientYieldResolver.TryGetSingleFlavor(job.OutputIngredient, out flavor))
                return true;

            flavor = FlavorType.Spicy;
            return false;
        }

        // -------------------------------------------------------------- turn flow

        private void OnTurnResolved(TurnResult result)
        {
            if (_cooldownTurnsLeft > 0)
                _cooldownTurnsLeft--;

            if (HasPendingEvent) return;
            if (!EnableTurnEndEvents) return;
            if (_cooldownTurnsLeft > 0) return;

            var candidates = CollectEligible(item => item.TriggerLevelIndex == 0);
            if (candidates.Count == 0) return;
            if (UnityEngine.Random.value > TurnEndEventChance) return;

            Present(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            StartCooldown();
        }

        private void OnStageSettled(StageSettlementResult result)
        {
            // 正式关卡由 LevelManager 在奖励之后建立事件队列。
            var levels = LevelManager.Instance;
            if (levels != null && levels.HasLevels && levels.IsRunStarted)
                return;

            bool enableStageEnd = ReadConfig(cfg => cfg.EnableStageEndEvents, false);
            if (!enableStageEnd || HasPendingEvent) return;

            var candidates = CollectEligible(item => item.TriggerLevelIndex == 0);
            if (candidates.Count == 0) return;

            int count = ReadConfig(cfg => cfg.StageEndEventCount, 0);
            if (count <= 0) return;

            Enqueue(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            PresentNextQueued();
        }

        private List<EventItem> CollectEligible(Func<EventItem, bool> extraFilter)
        {
            var result = new List<EventItem>();
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i];
                if (item == null || !IsJobRequirementMet(item)) continue;
                if (_seenEventIds.Contains(item.Id)) continue;
                if (_pending != null && _pending.Id == item.Id) continue;
                if (_queuedEventIds.Contains(item.Id)) continue;
                if (extraFilter != null && !extraFilter(item)) continue;
                result.Add(item);
            }

            return result;
        }

        /// <summary>进阶专属事件：要求岗位已解锁。</summary>
        private static bool IsJobRequirementMet(EventItem item)
        {
            if (item == null || !item.HasRequiredJob) return true;
            var jobs = JobManager.Instance;
            var progression = JobProgressionManager.Instance;
            if (jobs == null || progression == null) return false;
            var job = jobs.GetById(item.RequiredJobId);
            return job != null && progression.IsUnlocked(job);
        }

        // -------------------------------------------------------------- lifecycle

        public void ResetRun()
        {
            _pending = null;
            _cooldownTurnsLeft = 0;
            ChiefIncentive = 0;
            _queuedEventIds.Clear();
            _seenEventIds.Clear();
        }

        // ------------------------------------------------------------------ save

        public void CaptureState(out int chiefIncentive, out int cooldownTurnsLeft, out string pendingEventId)
        {
            chiefIncentive = ChiefIncentive;
            cooldownTurnsLeft = _cooldownTurnsLeft;
            pendingEventId = _pending != null ? _pending.Id : string.Empty;
        }

        public void ApplyState(int chiefIncentive, int cooldownTurnsLeft, string pendingEventId)
        {
            ChiefIncentive = chiefIncentive;
            _cooldownTurnsLeft = Mathf.Max(0, cooldownTurnsLeft);
            _pending = string.IsNullOrEmpty(pendingEventId)
                ? null
                : (database != null ? database.GetById(pendingEventId) : null);
        }

        public void CaptureExtendedState(GameSaveData data)
        {
            if (data == null) return;
            data.EventQueuedIds.AddRange(_queuedEventIds);
            foreach (string id in _seenEventIds)
                data.EventSeenIds.Add(id);
        }

        public void ApplyExtendedState(GameSaveData data)
        {
            _queuedEventIds.Clear();
            _seenEventIds.Clear();
            if (data == null) return;

            var queued = data.EventQueuedIds ?? new List<string>();
            var seen = data.EventSeenIds ?? new List<string>();
            for (int i = 0; i < queued.Count; i++)
            {
                string id = queued[i];
                if (!string.IsNullOrWhiteSpace(id) && !_queuedEventIds.Contains(id))
                    _queuedEventIds.Add(id);
            }

            for (int i = 0; i < seen.Count; i++)
            {
                string id = seen[i];
                if (!string.IsNullOrWhiteSpace(id))
                    _seenEventIds.Add(id);
            }

            PresentNextQueued();
        }

        public void SetChiefIncentive(int value) => ChiefIncentive = value;

        private static T ReadConfig<T>(Func<GameConfig, T> reader, T fallback)
        {
            var config = ResourceStore.Instance != null
                ? ResourceStore.Instance.Config
                : Resources.Load<GameConfig>(ResourcesConfigPath);
            if (config == null) return fallback;
            return reader(config);
        }
    }
}
