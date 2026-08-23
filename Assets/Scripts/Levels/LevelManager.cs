using System;
using System.Collections.Generic;
using Soup.Events;
using Soup.Game;
using Soup.Relics;
using UnityEngine;

namespace Soup.Levels
{
    /// <summary>
    /// Campaign levels: reach TargetScore within MaxTurns, then clear rewards,
    /// then next level; clear all levels to win the run.
    /// </summary>
    [DefaultExecutionOrder(-96)]
    public class LevelManager : MonoBehaviour
    {
        public const string ResourcesDatabasePath = "LevelDatabase";

        [SerializeField] private LevelDatabase database;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly LevelClearRewardSession _rewards = new LevelClearRewardSession();

        private LevelItem _current;
        private int _levelListIndex;
        private int _levelTurnIndex;
        private int _levelStartScore;
        private int _levelsClearedCount;
        /// <summary>本关结算时的最终得分（清零 TurnManager 分后仍供关卡间/失败页显示）。</summary>
        private int _lastFinishedScore;
        private readonly List<bool> _levelChallengeReachedFlags = new List<bool>();
        private LevelOutcome _outcome = LevelOutcome.InProgress;
        /// <summary>Won this level; 关卡间奖励领取中。</summary>
        private bool _awaitingClearSequence;
        /// <summary>当前已订阅事件的 TurnManager（Instance 晚创建或被重建时需重绑）。</summary>
        private TurnManager _boundTurns;

        public static LevelManager Instance { get; private set; }

        public LevelDatabase Database => database;

        public LevelItem Current => _current;
        public int LevelListIndex => _levelListIndex;
        /// <summary>
        /// 当前回合号（从 1 开始）。例如 MaxTurns=10 时，值为 1..10。
        /// 玩家在「回合 10/10」时可正常操作，点击下一回合结算本回合后再判定胜负。
        /// </summary>
        public int LevelTurnIndex => _levelTurnIndex;

        public int LevelStartScore => _levelStartScore;
        public int LevelsClearedCount => _levelsClearedCount;
        public LevelOutcome Outcome => _outcome;

        public LevelClearRewardSession ClearRewards => _rewards;
        public bool HasActiveClearRewards => _rewards != null && _rewards.IsActive;

        public bool HasLevels => database != null && database.GetOrdered().Count > 0;
        public bool IsInProgress => _outcome == LevelOutcome.InProgress && _current != null;
        public bool IsWon => _outcome == LevelOutcome.Won;
        public bool IsLost => _outcome == LevelOutcome.Lost;
        public bool IsCampaignComplete =>
            IsWon && _current != null && GetNextLevel() == null && !_awaitingClearSequence;

        /// <summary>当前是否为战役最后一关（通关后无下一关）。</summary>
        public bool IsLastLevel => HasLevels && _current != null && GetNextLevel() == null;

        /// <summary>Whether the player may press「下一回合」.</summary>
        public bool CanAdvanceTurn =>
            !HasLevels
            || (_current != null
                && _outcome == LevelOutcome.InProgress
                && !_awaitingClearSequence);

        /// <summary>无关卡时仍可用手动结算；有关卡时通关自动处理。</summary>
        public bool CanSettleAndAdvance => !HasLevels;

        public bool UsesAutoSettle => HasLevels;

        public int TargetScore => _current != null ? _current.TargetScore : 0;
        public int ChallengeScore => _current != null ? _current.ChallengeScore : 0;
        public bool HasChallengeScore => ChallengeScore > 0;
        public int MaxTurns => _current != null ? _current.MaxTurns : 0;

        /// <summary>本关开始后的得分增量（酸涩、热辣在关底结算时计入）。</summary>
        public int ScoreGainedInLevel
        {
            get
            {
                // 关卡已结束后 TurnManager 分会被清零，改读结算缓存。
                if (_outcome != LevelOutcome.InProgress)
                    return Mathf.Max(0, _lastFinishedScore);

                var turns = TurnManager.Instance;
                int score = turns != null ? turns.Score : 0;
                return Mathf.Max(0, score - _levelStartScore);
            }
        }

        /// <summary>上一关（或本关刚结束）结算时的最终得分。</summary>
        public int LastFinishedScore => _lastFinishedScore;

        /// <summary>含当前回合在内的剩余回合数。</summary>
        public int RemainingTurns =>
            _current == null ? 0 : Mathf.Max(0, _current.MaxTurns - _levelTurnIndex + 1);

        public int ScoreRemaining =>
            _current == null ? 0 : Mathf.Max(0, _current.TargetScore - ScoreGainedInLevel);

        public bool ChallengeScoreReached =>
            HasChallengeScore && ScoreGainedInLevel >= ChallengeScore;

        public int UltimateChallengeScore =>
            _current != null ? _current.UltimateChallengeScore : 0;

        public bool HasUltimateChallengeScore => UltimateChallengeScore > 0;

        public bool UltimateChallengeScoreReached =>
            HasUltimateChallengeScore && ScoreGainedInLevel >= UltimateChallengeScore;

        /// <summary>五关均已通关，且每一关有挑战分的关卡都达到了挑战分。</summary>
        public bool AllClearedLevelsReachedChallenge()
        {
            var ordered = database != null ? database.GetOrdered() : null;
            if (ordered == null || ordered.Count == 0)
                return false;
            if (_levelsClearedCount < ordered.Count)
                return false;

            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].ChallengeScore <= 0)
                    continue;
                if (i >= _levelChallengeReachedFlags.Count || !_levelChallengeReachedFlags[i])
                    return false;
            }

            return true;
        }

        public event Action<LevelItem> LevelStarted;
        public event Action<LevelItem> LevelWon;
        public event Action<LevelItem> LevelLost;
        public event Action CampaignCompleted;
        public event Action Changed;
        public event Action ClearRewardsChanged;

        public static void Initialize(LevelDatabase db)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(LevelManager));
                Instance = go.AddComponent<LevelManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            Instance.database = db;
            LevelTuningStore.ApplySavedToDatabase(Instance.database);
            Instance.database?.RebuildIndex();
            if (Instance._current == null)
                Instance.ResetRun();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var db = Resources.Load<LevelDatabase>(ResourcesDatabasePath);
            if (db == null) return;
            Initialize(db);
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
                database = Resources.Load<LevelDatabase>(ResourcesDatabasePath);

            LevelTuningStore.ApplySavedToDatabase(database);
            database?.RebuildIndex();
            BindRewardSession(true);
            if (_current == null && HasLevels)
                ResetRun();
        }

        private void OnEnable()
        {
            EnsureTurnManagerBound();
            BindEventManager(true);
            BindRewardSession(true);
        }

        private void Start()
        {
            EnsureTurnManagerBound();
            BindEventManager(true);
            BindRewardSession(true);
            if (_current == null && HasLevels)
                ResetRun();
        }

        private void Update()
        {
            // TurnManager 常在本对象 OnEnable 之后才 EnsureExists；每帧确认订阅，否则关卡回合永不推进。
            EnsureTurnManagerBound();
        }

        private void OnDisable()
        {
            UnbindTurnManager();
            BindEventManager(false);
        }

        private void OnDestroy()
        {
            UnbindTurnManager();
            BindEventManager(false);
            BindRewardSession(false);
            if (Instance == this)
                Instance = null;
        }

        private void EnsureTurnManagerBound()
        {
            var turns = TurnManager.Instance;
            if (turns == _boundTurns) return;

            UnbindTurnManager();
            if (turns == null) return;

            turns.TurnResolved += OnTurnResolved;
            turns.StageSettled += OnStageSettled;
            turns.UndoApplied += OnUndoApplied;
            _boundTurns = turns;
        }

        private void UnbindTurnManager()
        {
            if (_boundTurns == null) return;

            _boundTurns.TurnResolved -= OnTurnResolved;
            _boundTurns.StageSettled -= OnStageSettled;
            _boundTurns.UndoApplied -= OnUndoApplied;
            _boundTurns = null;
        }

        private void BindEventManager(bool bind)
        {
            var events = EventManager.Instance;
            if (events == null) return;

            events.StageEventBatchCompleted -= OnStageEventBatchCompleted;
            if (!bind) return;

            events.StageEventBatchCompleted += OnStageEventBatchCompleted;
        }

        private void BindRewardSession(bool bind)
        {
            if (_rewards == null) return;
            _rewards.Changed -= OnRewardSessionChanged;
            _rewards.Completed -= OnRewardSessionCompleted;
            if (!bind) return;
            _rewards.Changed += OnRewardSessionChanged;
            _rewards.Completed += OnRewardSessionCompleted;
        }

        public void ResetRun()
        {
            database?.RebuildIndex();
            _levelListIndex = 0;
            _outcome = LevelOutcome.InProgress;
            _levelTurnIndex = 1;
            _levelStartScore = 0;
            _levelsClearedCount = 0;
            _lastFinishedScore = 0;
            _levelChallengeReachedFlags.Clear();
            _awaitingClearSequence = false;
            _rewards.Clear();

            var ordered = database != null ? database.GetOrdered() : null;
            if (ordered == null || ordered.Count == 0)
            {
                _current = null;
                RaiseChanged();
                return;
            }

            StartLevelAt(0);
        }

        public LevelItem GetById(string id) =>
            database != null ? database.GetById(id) : null;

        public LevelItem GetNextLevel()
        {
            if (database == null) return null;
            return database.GetAtOrderedIndex(_levelListIndex + 1);
        }

        /// <summary>
        /// After level-clear rewards, move to the next level.
        /// Returns false if campaign is done.
        /// </summary>
        public bool TryAdvanceToNextLevel()
        {
            if (!HasLevels) return true;
            if (_outcome != LevelOutcome.Won || _current == null)
                return false;

            var next = GetNextLevel();
            if (next == null)
            {
                RaiseChanged();
                return false;
            }

            StartLevelAt(_levelListIndex + 1);
            return true;
        }

        public void CaptureState(
            out string levelId,
            out int levelListIndex,
            out int levelTurnIndex,
            out int levelStartScore,
            out int levelFinishedScore,
            out int outcome,
            out bool awaitingSettle,
            out int levelsClearedCount,
            out bool rewardElvesClaimed,
            out bool rewardWarehouseClaimed,
            out bool rewardRelicClaimed,
            out bool rewardShopClaimed,
            out bool rewardAdvanceClaimed,
            out bool rewardEventsClaimed,
            out bool rewardStandardStageEventsStarted,
            out int rewardGatherCharges,
            out int rewardProcessCharges,
            out int rewardCookCharges,
            List<string> rewardRelicOfferIds,
            List<string> rewardShopOfferIds,
            List<string> rewardGatherUnlockOfferIds,
            List<string> rewardProcessUnlockOfferIds,
            List<bool> levelChallengeReachedFlags)
        {
            levelId = _current != null ? _current.Id : string.Empty;
            levelListIndex = _levelListIndex;
            levelTurnIndex = _levelTurnIndex;
            levelStartScore = 0;
            levelFinishedScore = _lastFinishedScore;
            outcome = (int)_outcome;
            awaitingSettle = _awaitingClearSequence;
            levelsClearedCount = _levelsClearedCount;

            levelChallengeReachedFlags?.Clear();
            if (levelChallengeReachedFlags != null)
            {
                for (int i = 0; i < _levelChallengeReachedFlags.Count; i++)
                    levelChallengeReachedFlags.Add(_levelChallengeReachedFlags[i]);
            }

            if (_awaitingClearSequence && _rewards.IsActive)
            {
                _rewards.Capture(
                    out rewardElvesClaimed,
                    out rewardWarehouseClaimed,
                    out rewardRelicClaimed,
                    out rewardShopClaimed,
                    out rewardAdvanceClaimed,
                    out rewardEventsClaimed,
                    out rewardStandardStageEventsStarted,
                    out rewardGatherCharges,
                    out rewardProcessCharges,
                    out rewardCookCharges,
                    rewardRelicOfferIds,
                    rewardShopOfferIds,
                    rewardGatherUnlockOfferIds,
                    rewardProcessUnlockOfferIds);
            }
            else
            {
                rewardElvesClaimed = false;
                rewardWarehouseClaimed = false;
                rewardRelicClaimed = false;
                rewardShopClaimed = false;
                rewardAdvanceClaimed = false;
                rewardEventsClaimed = false;
                rewardStandardStageEventsStarted = false;
                rewardGatherCharges = 0;
                rewardProcessCharges = 0;
                rewardCookCharges = 0;
                rewardRelicOfferIds?.Clear();
                rewardShopOfferIds?.Clear();
                rewardGatherUnlockOfferIds?.Clear();
                rewardProcessUnlockOfferIds?.Clear();
            }
        }

        public void ApplyState(
            string levelId,
            int levelListIndex,
            int levelTurnIndex,
            int levelStartScore,
            LevelOutcome outcome,
            bool awaitingSettle = false,
            int levelsClearedCount = 0,
            bool rewardElvesClaimed = false,
            bool rewardWarehouseClaimed = false,
            bool rewardRelicClaimed = false,
            bool rewardShopClaimed = false,
            bool rewardAdvanceClaimed = false,
            bool rewardEventsClaimed = false,
            bool rewardStandardStageEventsStarted = false,
            int rewardGatherCharges = 0,
            int rewardProcessCharges = 0,
            int rewardCookCharges = 0,
            IList<string> rewardRelicOfferIds = null,
            IList<string> rewardShopOfferIds = null,
            IList<string> rewardGatherUnlockOfferIds = null,
            IList<string> rewardProcessUnlockOfferIds = null,
            int levelFinishedScore = 0,
            IList<bool> levelChallengeReachedFlags = null)
        {
            database?.RebuildIndex();
            // 每关独立计分：存档中的 levelStartScore 忽略，本关得分即 TurnManager.Score。
            _ = levelStartScore;
            _levelStartScore = 0;
            _lastFinishedScore = Mathf.Max(0, levelFinishedScore);
            _levelsClearedCount = Mathf.Max(0, levelsClearedCount);
            _levelChallengeReachedFlags.Clear();
            if (levelChallengeReachedFlags != null)
            {
                for (int i = 0; i < levelChallengeReachedFlags.Count; i++)
                    _levelChallengeReachedFlags.Add(levelChallengeReachedFlags[i]);
            }
            _outcome = outcome;
            _awaitingClearSequence = awaitingSettle && outcome == LevelOutcome.Won;
            _rewards.Clear();

            // 当前回合号从 1 起。旧存档若存 0（未开始），升为 1。
            _levelTurnIndex = levelTurnIndex <= 0 ? 1 : levelTurnIndex;

            LevelItem item = null;
            if (!string.IsNullOrEmpty(levelId))
                item = GetById(levelId);

            var ordered = database != null ? database.GetOrdered() : null;
            if (item != null && ordered != null)
            {
                int idx = database.IndexOfOrdered(item);
                _levelListIndex = idx >= 0 ? idx : Mathf.Max(0, levelListIndex);
                _current = item;
            }
            else if (ordered != null && ordered.Count > 0)
            {
                _levelListIndex = Mathf.Clamp(levelListIndex, 0, ordered.Count - 1);
                _current = ordered[_levelListIndex];
            }
            else
            {
                _current = null;
                _levelListIndex = 0;
            }

            if (_outcome == LevelOutcome.InProgress)
                Evaluate(silent: true);

            if (_awaitingClearSequence || (_outcome == LevelOutcome.Won && !IsCampaignComplete))
            {
                _awaitingClearSequence = true;
                RaiseChanged();
                BeginLevelClearSequence(
                    restoreRewards: true,
                    rewardElvesClaimed,
                    rewardWarehouseClaimed,
                    rewardRelicClaimed,
                    rewardShopClaimed,
                    rewardAdvanceClaimed,
                    rewardEventsClaimed,
                    rewardStandardStageEventsStarted,
                    rewardGatherCharges,
                    rewardProcessCharges,
                    rewardCookCharges,
                    rewardRelicOfferIds,
                    rewardShopOfferIds,
                    rewardGatherUnlockOfferIds,
                    rewardProcessUnlockOfferIds);
                return;
            }

            RaiseChanged();
        }

        private void StartLevelAt(int listIndex)
        {
            var ordered = database != null ? database.GetOrdered() : null;
            if (ordered == null || listIndex < 0 || listIndex >= ordered.Count)
            {
                _current = null;
                _levelListIndex = listIndex;
                RaiseChanged();
                return;
            }

            _levelListIndex = listIndex;
            _current = ordered[listIndex];
            _levelTurnIndex = 1;
            _outcome = LevelOutcome.InProgress;
            _awaitingClearSequence = false;
            _rewards.Clear();

            // 每一关独立：进入新关时分数 / 食材 / 风味清零。
            // 事件已触发记录保留在 EventManager，跨五关累计去重。
            var turns = TurnManager.Instance;
            turns?.ResetLevelScore();
            _levelStartScore = 0;
            _lastFinishedScore = 0;
            RecallEmployeesToStandby();

            // 库存已清：此时检测遗物并发放关卡补给（如炖煮吱吱 +3000）。
            RelicManager.Instance?.ApplyLevelEnterRelicEffects();

            LevelStarted?.Invoke(_current);
            RaiseChanged();
        }

        private void OnTurnResolved(TurnResult _)
        {
            if (!HasLevels || _current == null) return;
            if (_outcome != LevelOutcome.InProgress) return;

            // 当前回合号刚被「下一回合」结算完毕。
            if (_levelTurnIndex >= _current.MaxTurns)
            {
                RaiseChanged();
                Evaluate(silent: false);
                return;
            }

            _levelTurnIndex++;
            RaiseChanged();
        }

        private void OnStageSettled(StageSettlementResult _)
        {
            if (!HasLevels || _current == null) return;
            RaiseChanged();
        }

        private void OnStageEventBatchCompleted()
        {
            if (!_awaitingClearSequence || !_rewards.IsActive) return;
            _rewards.MarkEventsResolved();
        }

        private void OnRewardSessionChanged() => ClearRewardsChanged?.Invoke();

        private void OnRewardSessionCompleted()
        {
            if (!_awaitingClearSequence) return;
            FinishLevelClearSequence();
        }

        private void OnUndoApplied()
        {
            RaiseChanged();
        }

        /// <summary>
        /// 在最后一回合结束后判定：得分 ≥ 目标则通关并进入关卡间。
        /// </summary>
        private void Evaluate(bool silent)
        {
            if (_current == null || _outcome != LevelOutcome.InProgress)
                return;

            if (_levelTurnIndex < _current.MaxTurns)
                return;

            TurnManager.Instance?.TrySettleSourAtStageEnd(out _);
            TurnManager.Instance?.TrySettleSpicyAtLevelEnd(out _);

            int gained = ScoreGainedInLevel;
            _lastFinishedScore = gained;

            // 胜负已定：先跑关卡结束遗物（升华等），再清分/召回。
            RelicManager.Instance?.ApplyLevelEndRelicEffects();

            if (gained >= _current.TargetScore)
            {
                _outcome = LevelOutcome.Won;
                _awaitingClearSequence = true;
                RecordCurrentLevelChallengeResult();
                // 关结束：分数、食材、风味立即清零（展示分走 _lastFinishedScore）。
                TurnManager.Instance?.ResetLevelScore();
                RecallEmployeesToStandby();
                if (!silent)
                {
                    LevelWon?.Invoke(_current);
                    BeginLevelClearSequence(restoreRewards: false);
                }

                return;
            }

            _outcome = LevelOutcome.Lost;
            _awaitingClearSequence = false;
            TurnManager.Instance?.ResetLevelScore();
            if (!silent)
            {
                LevelLost?.Invoke(_current);
                GameSessionLaunch.DeclareLevelDefeat();
            }
        }

        /// <summary>调试：跳过胜负判定，直接打开关卡间页面。</summary>
        public void DebugForceOpenClearRewards()
        {
            if (!HasLevels || _current == null)
            {
                Debug.LogWarning("[LevelManager] 无关卡，无法打开关卡间页面。");
                return;
            }

            var turns = TurnManager.Instance;
            _lastFinishedScore = turns != null ? Mathf.Max(0, turns.Score - _levelStartScore) : 0;
            _outcome = LevelOutcome.Won;
            _awaitingClearSequence = true;
            RecordCurrentLevelChallengeResult();
            turns?.ResetLevelScore();
            RecallEmployeesToStandby();
            BeginLevelClearSequence(restoreRewards: false);
        }

        /// <summary>调试：直接跳转到胜利结算（普通 / 挑战 / 终极挑战）。</summary>
        public void DebugForceCampaignVictory(DebugCampaignVictoryKind kind)
        {
            if (!HasLevels || database == null)
            {
                Debug.LogWarning("[LevelManager] 无关卡，无法调试胜利结算。");
                return;
            }

            var ordered = database.GetOrdered();
            if (ordered.Count == 0)
            {
                Debug.LogWarning("[LevelManager] 关卡列表为空。");
                return;
            }

            int lastIndex = ordered.Count - 1;
            var lastLevel = ordered[lastIndex];

            _levelListIndex = lastIndex;
            _current = lastLevel;
            _levelTurnIndex = lastLevel.MaxTurns;
            _outcome = LevelOutcome.Won;
            _awaitingClearSequence = false;
            _rewards.Clear();
            _levelsClearedCount = ordered.Count;

            _levelChallengeReachedFlags.Clear();
            for (int i = 0; i < ordered.Count; i++)
                _levelChallengeReachedFlags.Add(false);

            bool allChallenge = kind != DebugCampaignVictoryKind.Normal;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].ChallengeScore <= 0) continue;
                _levelChallengeReachedFlags[i] = allChallenge;
            }

            _lastFinishedScore = kind switch
            {
                DebugCampaignVictoryKind.Normal => lastLevel.TargetScore,
                DebugCampaignVictoryKind.Challenge => Mathf.Max(lastLevel.ChallengeScore, lastLevel.TargetScore),
                DebugCampaignVictoryKind.UltimateChallenge => Mathf.Max(
                    lastLevel.UltimateChallengeScore > 0
                        ? lastLevel.UltimateChallengeScore
                        : lastLevel.ChallengeScore,
                    lastLevel.TargetScore),
                _ => lastLevel.TargetScore
            };

            TurnManager.Instance?.ResetLevelScore();
            RecallEmployeesToStandby();
            CampaignCompleted?.Invoke();
            RaiseChanged();
            GameSessionLaunch.GoToVictorySettlement();
        }

        private void RecordCurrentLevelChallengeResult()
        {
            if (_current == null) return;
            while (_levelChallengeReachedFlags.Count <= _levelListIndex)
                _levelChallengeReachedFlags.Add(false);
            _levelChallengeReachedFlags[_levelListIndex] = ChallengeScoreReached;
        }

        /// <summary>
        /// 过关 / 开新关：可手动分配的员工全部下岗回待命（锁定岗如蘑菇人除外）。
        /// </summary>
        private static void RecallEmployeesToStandby()
        {
            ElfManager.Instance?.ClearAssignments();
            var map = UnityEngine.Object.FindObjectOfType<JobWorldMap>();
            map?.RefreshLabels();
        }

        /// <summary>
        /// 通关流程：非最后一关 → 关卡间领奖；最后一关 → 胜利结算场景。
        /// </summary>
        private void BeginLevelClearSequence(
            bool restoreRewards = false,
            bool rewardElvesClaimed = false,
            bool rewardWarehouseClaimed = false,
            bool rewardRelicClaimed = false,
            bool rewardShopClaimed = false,
            bool rewardAdvanceClaimed = false,
            bool rewardEventsClaimed = false,
            bool rewardStandardStageEventsStarted = false,
            int rewardGatherCharges = 0,
            int rewardProcessCharges = 0,
            int rewardCookCharges = 0,
            IList<string> rewardRelicOfferIds = null,
            IList<string> rewardShopOfferIds = null,
            IList<string> rewardGatherUnlockOfferIds = null,
            IList<string> rewardProcessUnlockOfferIds = null)
        {
            if (!HasLevels || _current == null) return;
            if (_outcome != LevelOutcome.Won) return;

            // 最后一关：跳过关卡间，进入全战役胜利结算。
            if (GetNextLevel() == null)
            {
                if (!restoreRewards)
                    _levelsClearedCount = Mathf.Max(0, _levelsClearedCount) + 1;
                else
                    _levelsClearedCount = Mathf.Max(1, _levelsClearedCount > 0 ? _levelsClearedCount : _levelListIndex + 1);

                _awaitingClearSequence = false;
                _rewards.Clear();
                CampaignCompleted?.Invoke();
                RaiseChanged();
                GameSessionLaunch.GoToVictorySettlement();
                return;
            }

            OpenClearRewards(
                restore: restoreRewards,
                rewardElvesClaimed,
                rewardWarehouseClaimed,
                rewardRelicClaimed,
                rewardShopClaimed,
                rewardAdvanceClaimed,
                    rewardEventsClaimed,
                    rewardStandardStageEventsStarted,
                    rewardGatherCharges,
                rewardProcessCharges,
                rewardCookCharges,
                rewardRelicOfferIds,
                rewardShopOfferIds,
                rewardGatherUnlockOfferIds,
                rewardProcessUnlockOfferIds);
        }

        private void OpenClearRewards(
            bool restore,
            bool rewardElvesClaimed = false,
            bool rewardWarehouseClaimed = false,
            bool rewardRelicClaimed = false,
            bool rewardShopClaimed = false,
            bool rewardAdvanceClaimed = false,
            bool rewardEventsClaimed = false,
            bool rewardStandardStageEventsStarted = false,
            int rewardGatherCharges = 0,
            int rewardProcessCharges = 0,
            int rewardCookCharges = 0,
            IList<string> rewardRelicOfferIds = null,
            IList<string> rewardShopOfferIds = null,
            IList<string> rewardGatherUnlockOfferIds = null,
            IList<string> rewardProcessUnlockOfferIds = null)
        {
            if (restore)
            {
                int cleared = Mathf.Max(1, _levelsClearedCount > 0 ? _levelsClearedCount : _levelListIndex + 1);
                _levelsClearedCount = cleared;
                _rewards.Restore(
                    cleared,
                    rewardElvesClaimed,
                    rewardWarehouseClaimed,
                    rewardRelicClaimed,
                    rewardShopClaimed,
                    rewardAdvanceClaimed,
                    rewardEventsClaimed,
                    rewardStandardStageEventsStarted,
                    rewardGatherCharges,
                    rewardProcessCharges,
                    rewardCookCharges,
                    rewardRelicOfferIds,
                    rewardShopOfferIds,
                    rewardGatherUnlockOfferIds,
                    rewardProcessUnlockOfferIds);
            }
            else
            {
                _levelsClearedCount = Mathf.Max(0, _levelsClearedCount) + 1;
                _rewards.BeginFresh(_levelsClearedCount);
            }

            RaiseChanged();
            ClearRewardsChanged?.Invoke();
            GameSessionLaunch.GoToInterLevel();
        }

        private void FinishLevelClearSequence()
        {
            if (!_awaitingClearSequence) return;
            _awaitingClearSequence = false;
            _rewards.Clear();

            if (!TryAdvanceToNextLevel())
            {
                CampaignCompleted?.Invoke();
                RaiseChanged();
            }
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
