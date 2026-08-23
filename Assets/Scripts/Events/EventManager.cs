using System;
using System.Collections.Generic;
using Soup.Game;
using Soup.Jobs;
using Soup.Levels;
using UnityEngine;

namespace Soup.Events
{
    /// <summary>
    /// 运行时事件目录与待选队列。
    /// 关卡通关后抽取通关事件（默认 2 个，至多 1 个进阶专属）。
    /// 已触发事件在本大关（整局战役，含全部关卡如五关）内不重复；仅新开一局时清空。
    /// </summary>
    [DefaultExecutionOrder(-95)]
    public class EventManager : MonoBehaviour
    {
        public const string ResourcesDatabasePath = "EventDatabase";
        public const string ResourcesConfigPath = "GameConfig";

        /// <summary>进阶专属相对一般事件的权重倍率。</summary>
        public const float AdvancedWeightMultiplier = 3f;

        [SerializeField] private EventDatabase database;
        [SerializeField] private GameConfig config;
        [SerializeField] private bool dontDestroyOnLoad = true;

        /// <summary>本大关内已触发过的事件 id（跨关卡累计，进下一关不清空）。</summary>
        private readonly List<string> _seenEventIds = new List<string>();
        private readonly Queue<EventItem> _pendingQueue = new Queue<EventItem>();
        private EventItem _pendingEvent;
        /// <summary>Turn index when the last random turn-end event was presented. 0 = never.</summary>
        private int _lastRandomEventTurn;
        private bool _stageEventBatchActive;

        public static EventManager Instance { get; private set; }

        public EventDatabase Database => database;
        public GameConfig Config => config;

        public IReadOnlyList<EventItem> All =>
            database != null ? database.Events : Array.Empty<EventItem>();

        public EventItem PendingEvent => _pendingEvent;

        public bool HasPendingEvent => _pendingEvent != null;

        public int QueuedEventCount => _pendingQueue.Count;

        public bool HasStageEventBatch => _stageEventBatchActive;

        public int LastRandomEventTurn => _lastRandomEventTurn;

        public bool EnableTurnEndEvents =>
            config != null ? config.EnableTurnEndEvents : false;

        public float TurnEndEventChance =>
            config != null ? Mathf.Clamp01(config.TurnEndEventChance) : 0.45f;

        /// <summary>两次随机事件至少间隔的回合数（含冷却窗口）。</summary>
        public int EventCooldownTurns =>
            config != null ? Mathf.Max(1, config.EventCooldownTurns) : 3;

        public bool EnableStageEndEvents =>
            config != null ? config.EnableStageEndEvents : true;

        public int StageEndEventCount =>
            config != null ? Mathf.Max(0, config.StageEndEventCount) : 2;

        public event Action<EventItem> EventPresented;
        public event Action<EventItem, int> EventResolved;
        public event Action PendingCleared;
        /// <summary>关卡通关事件批次全部选完（或没有可抽事件）时触发。</summary>
        public event Action StageEventBatchCompleted;

        public static void Initialize(EventDatabase db, GameConfig gameConfig = null)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(EventManager));
                Instance = go.AddComponent<EventManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            Instance.database = db;
            if (gameConfig != null)
                Instance.config = gameConfig;
            Instance.database?.RebuildIndex();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var db = Resources.Load<EventDatabase>(ResourcesDatabasePath);
            if (db == null) return;
            var cfg = Resources.Load<GameConfig>(ResourcesConfigPath);
            Initialize(db, cfg);
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
            if (config == null)
                config = Resources.Load<GameConfig>(ResourcesConfigPath);

            database?.RebuildIndex();
        }

        private void OnEnable() => EnsureTurnManagerBound();

        private void Start() => EnsureTurnManagerBound();

        private void Update() => EnsureTurnManagerBound();

        private void OnDisable() => UnbindTurnManager();

        private void OnDestroy()
        {
            UnbindTurnManager();
            if (Instance == this)
                Instance = null;
        }

        private TurnManager _boundTurns;

        private void EnsureTurnManagerBound()
        {
            var turns = TurnManager.Instance;
            if (turns == _boundTurns) return;

            UnbindTurnManager();
            if (turns == null) return;

            turns.TurnResolved += OnTurnResolved;
            _boundTurns = turns;
        }

        private void UnbindTurnManager()
        {
            if (_boundTurns == null) return;
            _boundTurns.TurnResolved -= OnTurnResolved;
            _boundTurns = null;
        }

        public void ResetRun()
        {
            ClearSeenEventsForMajorStage();
            _lastRandomEventTurn = 0;
            _pendingQueue.Clear();
            _stageEventBatchActive = false;
            ClearPending(notify: true);
        }

        /// <summary>新大关 / 新开一局：清空已触发事件（关卡切换时勿调用）。</summary>
        public void ClearSeenEventsForMajorStage()
        {
            _seenEventIds.Clear();
        }

        public void ApplyState(IList<string> seenEventIds, int lastRandomEventTurn = 0)
        {
            _lastRandomEventTurn = Mathf.Max(0, lastRandomEventTurn);
            _seenEventIds.Clear();
            if (seenEventIds != null)
            {
                for (int i = 0; i < seenEventIds.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(seenEventIds[i]))
                        _seenEventIds.Add(seenEventIds[i]);
                }
            }

            _pendingQueue.Clear();
            _stageEventBatchActive = false;
            ClearPending(notify: true);
        }

        public IReadOnlyList<string> GetSeenEventIds() => _seenEventIds;

        public EventItem GetById(string id) =>
            database != null ? database.GetById(id) : null;

        /// <summary>
        /// Turns remaining before another random turn-end event may roll.
        /// 0 = ready now.
        /// </summary>
        public int GetCooldownTurnsRemaining(int currentTurnIndex = -1)
        {
            if (_lastRandomEventTurn <= 0)
                return 0;

            int turn = currentTurnIndex >= 0
                ? currentTurnIndex
                : (TurnManager.Instance != null ? TurnManager.Instance.TurnIndex : 0);
            int elapsed = turn - _lastRandomEventTurn;
            int need = EventCooldownTurns;
            return Mathf.Max(0, need - elapsed);
        }

        public bool IsRandomEventOnCooldown(int currentTurnIndex = -1) =>
            GetCooldownTurnsRemaining(currentTurnIndex) > 0;

        /// <summary>Force-present a specific event (debug / scripted). Does not start cooldown.</summary>
        public bool Present(EventItem eventItem, bool countAsRandomTurnEvent = false)
        {
            if (eventItem == null) return false;
            if (!CanScheduleEvent(eventItem))
                return false;

            if (HasPendingEvent)
            {
                _pendingQueue.Enqueue(eventItem);
                return true;
            }

            return PresentImmediate(eventItem, countAsRandomTurnEvent);
        }

        public bool PresentById(string id) => Present(GetById(id));

        /// <summary>调试：强制弹出指定事件（忽略已触发 / 互斥限制，不计入回合随机冷却）。</summary>
        public bool PresentForDebug(EventItem eventItem)
        {
            if (eventItem == null) return false;
            if (HasPendingEvent)
                return false;

            _pendingQueue.Clear();
            _stageEventBatchActive = false;
            return PresentImmediate(eventItem, countAsRandomTurnEvent: false);
        }

        public bool PresentForDebugById(string id) => PresentForDebug(GetById(id));

        /// <summary>
        /// 关卡通关后：抽取至多 <see cref="StageEndEventCount"/> 个事件（至多一个进阶专属），
        /// 全部选完后触发 <see cref="StageEventBatchCompleted"/>。
        /// 若无可抽事件，立即触发完成回调。
        /// </summary>
        public int PresentStageEvents()
        {
            // Avoid stacking another batch while one is already in flight.
            if (_stageEventBatchActive || HasPendingEvent || _pendingQueue.Count > 0)
                return _pendingQueue.Count + (HasPendingEvent ? 1 : 0);

            if (!EnableStageEndEvents || database == null || database.Count == 0 || StageEndEventCount <= 0)
            {
                NotifyStageEventBatchCompleted();
                return 0;
            }

            var picks = PickStageEventPair(StageEndEventCount);
            if (picks.Count == 0)
            {
                NotifyStageEventBatchCompleted();
                return 0;
            }

            _stageEventBatchActive = true;
            int enqueued = 0;
            for (int i = 0; i < picks.Count; i++)
            {
                var pick = picks[i];
                if (pick == null || !CanScheduleEvent(pick))
                    continue;
                _pendingQueue.Enqueue(pick);
                enqueued++;
            }

            if (enqueued <= 0 && !HasPendingEvent)
            {
                _stageEventBatchActive = false;
                NotifyStageEventBatchCompleted();
                return 0;
            }

            TryPresentNextFromQueue();
            return enqueued;
        }

        /// <summary>
        /// 立刻抽取并弹出若干通关事件（遗物「三个问号按钮」等）。
        /// 可与进行中的事件批次叠加入队，但不会重复入队同一事件或互斥组。
        /// </summary>
        public int PresentBonusStageEvents(int count)
        {
            if (count <= 0 || database == null || database.Count == 0)
                return 0;

            var picks = PickStageEventPair(count);
            if (picks.Count == 0)
                return 0;

            _stageEventBatchActive = true;
            int enqueued = 0;
            for (int i = 0; i < picks.Count; i++)
            {
                var pick = picks[i];
                if (pick == null || !CanScheduleEvent(pick))
                    continue;
                _pendingQueue.Enqueue(pick);
                enqueued++;
            }

            if (enqueued <= 0 && !HasPendingEvent)
            {
                _stageEventBatchActive = false;
                return 0;
            }

            TryPresentNextFromQueue();
            return enqueued;
        }

        /// <summary>
        /// Roll for an AfterTurn event using GameConfig chance + cooldown.
        /// Campaign mode (有关卡) skips this — events only fire after clearing a level.
        /// </summary>
        public bool TryPresentAfterTurn()
        {
            if (HasPendingEvent || _pendingQueue.Count > 0) return false;
            if (_stageEventBatchActive) return false;

            var levels = LevelManager.Instance;
            if (levels != null && levels.HasLevels)
                return false;

            if (!EnableTurnEndEvents) return false;
            if (database == null || database.Count == 0) return false;

            int turn = TurnManager.Instance != null ? TurnManager.Instance.TurnIndex : 0;
            if (IsRandomEventOnCooldown(turn))
                return false;

            if (UnityEngine.Random.value > TurnEndEventChance)
                return false;

            var pick = PickWeightedFromMoment(EventTriggerMoment.AfterTurn, excludeAdvancedIfAnyPicked: false, alreadyPickedAdvanced: false, exclude: null);
            return Present(pick, countAsRandomTurnEvent: true);
        }

        public bool TryChooseOption(int optionIndex, out string message)
        {
            message = string.Empty;
            if (_pendingEvent == null)
            {
                message = "当前没有待选事件";
                return false;
            }

            var options = _pendingEvent.Options;
            if (options == null || optionIndex < 0 || optionIndex >= options.Count)
            {
                message = "无效选项";
                return false;
            }

            var option = options[optionIndex];
            if (option == null)
            {
                message = "选项为空";
                return false;
            }

            var resolved = _pendingEvent;
            // 先记入已触发，避免选项内获得遗物（如三个问号）再次抽到本事件。
            MarkEventResolved(resolved);
            EventEffectRunner.Apply(option);

            _pendingEvent = null;
            EventResolved?.Invoke(resolved, optionIndex);
            PendingCleared?.Invoke();

            TryPresentNextFromQueue();
            TryCompleteStageEventBatch();

            message = $"{resolved.DisplayName}：{option.Label}";
            return true;
        }

        private void MarkEventResolved(EventItem resolved)
        {
            if (resolved == null) return;

            if (!_seenEventIds.Contains(resolved.Id))
                _seenEventIds.Add(resolved.Id);

            MarkExclusionGroupSeen(resolved.ExclusionGroup);
        }

        private void MarkExclusionGroupSeen(string group)
        {
            if (string.IsNullOrWhiteSpace(group) || database == null || database.Events == null)
                return;

            var events = database.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var item = events[i];
                if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                if (!string.Equals(item.ExclusionGroup, group, System.StringComparison.Ordinal))
                    continue;
                if (!_seenEventIds.Contains(item.Id))
                    _seenEventIds.Add(item.Id);
            }
        }

        public void ClearPending(bool notify = false)
        {
            bool hadAnything = _pendingEvent != null || _pendingQueue.Count > 0 || _stageEventBatchActive;
            if (!hadAnything) return;
            _pendingEvent = null;
            _pendingQueue.Clear();
            _stageEventBatchActive = false;
            if (notify)
                PendingCleared?.Invoke();
        }

        private void OnTurnResolved(TurnResult _)
        {
            TryPresentAfterTurn();
        }

        private void TryCompleteStageEventBatch()
        {
            if (!_stageEventBatchActive) return;
            if (_pendingEvent != null || _pendingQueue.Count > 0) return;
            _stageEventBatchActive = false;
            NotifyStageEventBatchCompleted();
        }

        private void NotifyStageEventBatchCompleted()
        {
            StageEventBatchCompleted?.Invoke();
        }

        private bool PresentImmediate(EventItem eventItem, bool countAsRandomTurnEvent)
        {
            if (eventItem == null || !CanScheduleEvent(eventItem)) return false;

            _pendingEvent = eventItem;
            if (countAsRandomTurnEvent)
            {
                int turn = TurnManager.Instance != null ? TurnManager.Instance.TurnIndex : 0;
                _lastRandomEventTurn = Mathf.Max(1, turn);
            }

            EventPresented?.Invoke(eventItem);
            return true;
        }

        private bool TryPresentNextFromQueue()
        {
            if (HasPendingEvent) return false;
            while (_pendingQueue.Count > 0)
            {
                var next = _pendingQueue.Dequeue();
                if (next == null) continue;
                if (!CanScheduleEvent(next))
                    continue;
                return PresentImmediate(next, countAsRandomTurnEvent: false);
            }

            return false;
        }

        /// <summary>
        /// Pick up to <paramref name="count"/> AfterStage events.
        /// Advanced exclusive weight ×3 when related job upgraded ≥1; at most one advanced in the set.
        /// </summary>
        private List<EventItem> PickStageEventPair(int count)
        {
            var result = new List<EventItem>(count);
            var exclude = new HashSet<EventItem>();
            var excludeIds = new HashSet<string>();
            bool pickedAdvanced = false;

            for (int n = 0; n < count; n++)
            {
                var pick = PickWeightedFromMoment(
                    EventTriggerMoment.AfterStage,
                    excludeAdvancedIfAnyPicked: true,
                    alreadyPickedAdvanced: pickedAdvanced,
                    exclude: exclude,
                    excludeIds: excludeIds);

                if (pick == null)
                    break;

                result.Add(pick);
                exclude.Add(pick);
                if (!string.IsNullOrEmpty(pick.Id))
                    excludeIds.Add(pick.Id);
                if (pick.IsAdvancedExclusive)
                    pickedAdvanced = true;
            }

            return result;
        }

        private EventItem PickWeightedFromMoment(
            EventTriggerMoment moment,
            bool excludeAdvancedIfAnyPicked,
            bool alreadyPickedAdvanced,
            HashSet<EventItem> exclude,
            HashSet<string> excludeIds = null)
        {
            if (database == null) return null;

            var pool = database.FindByTrigger(moment);
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                var item = pool[i];
                if (!IsEligibleForPick(item, excludeAdvancedIfAnyPicked, alreadyPickedAdvanced, exclude, excludeIds))
                    continue;
                total += GetEffectiveWeight(item);
            }

            if (total <= 0f) return null;

            float roll = UnityEngine.Random.Range(0f, total);
            float cursor = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                var item = pool[i];
                if (!IsEligibleForPick(item, excludeAdvancedIfAnyPicked, alreadyPickedAdvanced, exclude, excludeIds))
                    continue;
                cursor += GetEffectiveWeight(item);
                if (roll <= cursor)
                    return item;
            }

            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (IsEligibleForPick(pool[i], excludeAdvancedIfAnyPicked, alreadyPickedAdvanced, exclude, excludeIds))
                    return pool[i];
            }

            return null;
        }

        private float GetEffectiveWeight(EventItem item)
        {
            if (item == null) return 0f;
            float w = Mathf.Max(0f, item.Weight);
            if (item.IsAdvancedExclusive)
                w *= AdvancedWeightMultiplier;
            return w;
        }

        private bool IsEligibleForPick(
            EventItem item,
            bool excludeAdvancedIfAnyPicked,
            bool alreadyPickedAdvanced,
            HashSet<EventItem> exclude,
            HashSet<string> excludeIds = null)
        {
            if (!IsEligibleBase(item)) return false;
            if (exclude != null && exclude.Contains(item)) return false;
            if (excludeIds != null
                && !string.IsNullOrEmpty(item.Id)
                && excludeIds.Contains(item.Id))
                return false;
            if (IsExclusionGroupBlockedInBatch(item, exclude))
                return false;

            if (item.IsAdvancedExclusive)
            {
                if (excludeAdvancedIfAnyPicked && alreadyPickedAdvanced)
                    return false;
                if (!IsAdvancedJobUnlocked(item))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 同批次抽取时，互斥组内已有候选则不可再抽（入队前尚未写入队列，仅靠 IsExclusionGroupBlocked 拦不住）。
        /// </summary>
        private bool IsExclusionGroupBlockedInBatch(EventItem item, HashSet<EventItem> batchPicks)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ExclusionGroup))
                return false;
            if (batchPicks == null || batchPicks.Count == 0)
                return false;

            string group = item.ExclusionGroup;
            foreach (var picked in batchPicks)
            {
                if (picked != null
                    && string.Equals(picked.ExclusionGroup, group, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsAdvancedJobUnlocked(EventItem item)
        {
            if (item == null || item.RelatedJob == null)
                return false;

            var progression = JobProgressionManager.Instance;
            if (progression == null) return false;
            return progression.GetUpgradeLevel(item.RelatedJob) >= 1;
        }

        private bool IsEligibleBase(EventItem item)
        {
            if (item == null) return false;
            if (item.Options == null || item.Options.Count == 0) return false;
            if (item.Weight <= 0f) return false;
            if (_seenEventIds.Contains(item.Id))
                return false;
            if (IsEventIdScheduled(item.Id))
                return false;
            if (IsExclusionGroupBlocked(item))
                return false;
            if (!IsStageRequirementMet(item))
                return false;
            return true;
        }

        /// <summary>同一事件不可同时出现在 pending / 队列中。</summary>
        private bool IsEventIdScheduled(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return false;
            if (_pendingEvent != null && _pendingEvent.Id == eventId)
                return true;

            foreach (var queued in _pendingQueue)
            {
                if (queued != null && queued.Id == eventId)
                    return true;
            }

            return false;
        }

        /// <summary>互斥组内任一事件已触发或已在队列中，则整组不可再抽/入队。</summary>
        private bool IsExclusionGroupBlocked(EventItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ExclusionGroup))
                return false;
            if (database == null || database.Events == null)
                return false;

            string group = item.ExclusionGroup;
            var events = database.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var other = events[i];
                if (other == null || string.IsNullOrEmpty(other.Id)) continue;
                if (!string.Equals(other.ExclusionGroup, group, System.StringComparison.Ordinal))
                    continue;
                if (_seenEventIds.Contains(other.Id) || IsEventIdScheduled(other.Id))
                    return true;
            }

            return false;
        }

        private bool CanScheduleEvent(EventItem item)
        {
            if (item == null) return false;
            if (IsEventIdScheduled(item.Id))
                return false;
            if (IsExclusionGroupBlocked(item))
                return false;
            if (_seenEventIds.Contains(item.Id))
                return false;
            return true;
        }

        private static bool IsStageRequirementMet(EventItem item)
        {
            if (item == null || item.RequiredStageIndex <= 0)
                return true;

            var levels = LevelManager.Instance;
            if (levels == null || !levels.HasLevels)
                return false;

            // PresentStageEvents runs after a level clear; LevelsClearedCount is the stage just cleared.
            return levels.LevelsClearedCount == item.RequiredStageIndex;
        }
    }
}
