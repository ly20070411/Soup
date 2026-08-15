using System;
using System.Collections.Generic;
using Soup.Events;
using Soup.Game;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Levels
{
    public enum LevelOutcome
    {
        InProgress = 0,
        Won = 1,
        Lost = 2
    }

    public enum LevelRunMode
    {
        Campaign = 0,
        Practice = 1
    }

    public enum LevelRewardKind
    {
        GatherJob = 0,
        ProcessJob = 1,
        UpgradeJob = 2,
        Relic = 3
    }

    [Serializable]
    public sealed class LevelRewardOffer
    {
        public LevelRewardKind Kind;
        public string ContentId = string.Empty;
        public string Title = string.Empty;
        public string Description = string.Empty;
    }

    /// <summary>
    /// 正式关卡流程：简报 → 回合 → 自动酸涩结算 → 剧情 → 奖励/事件 → 下一关。
    /// 练习模式只运行所选关卡，不继承奖励，也不会推进战役解锁。
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class LevelManager : MonoBehaviour
    {
        public const string ResourcesDatabasePath = "LevelDatabase";

        [SerializeField] private LevelDatabase database;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private int _levelIndex;
        private int _levelTurnIndex;
        private int _scoreGainedInLevel;
        private LevelOutcome _outcome = LevelOutcome.InProgress;
        private LevelRunMode _runMode = LevelRunMode.Campaign;
        private bool _clearRewardsActive;
        private bool _rewardClaimed = true;
        private bool _briefingActive;
        private bool _outroActive;
        private bool _started;
        private bool _subscribedTurns;
        private int _lastSourUsed;
        private int _lastSourScore;
        private string _levelStartSnapshotJson = string.Empty;
        private readonly List<LevelRewardOffer> _rewardOffers = new List<LevelRewardOffer>();

        public static LevelManager Instance { get; private set; }

        /// <summary>已解锁的最高关卡索引（0 起），仅连续战役会推进。</summary>
        public static int UnlockedLevelIndex
        {
            get => PlayerPrefs.GetInt(UnlockedLevelKey, 0);
            private set
            {
                PlayerPrefs.SetInt(UnlockedLevelKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        private const string UnlockedLevelKey = "Soup.UnlockedLevelIndex";

        public LevelDatabase Database => database;
        public bool HasLevels => database != null && database.Count > 0;
        public bool IsRunStarted => _started;
        public LevelRunMode RunMode => _runMode;
        public bool IsPracticeMode => _runMode == LevelRunMode.Practice;

        public LevelItem Current =>
            HasLevels && _levelIndex >= 0 && _levelIndex < database.Count
                ? database.Levels[_levelIndex]
                : null;

        public int LevelIndex => _levelIndex;
        public int LevelTurnIndex => _levelTurnIndex;
        public int ScoreGainedInLevel => _scoreGainedInLevel;
        public LevelOutcome Outcome => _outcome;
        public int LastSourUsed => _lastSourUsed;
        public int LastSourScore => _lastSourScore;
        public bool IsBriefingActive => _briefingActive;
        public bool IsOutroActive => _outroActive;
        public bool RewardClaimed => _rewardClaimed;
        public IReadOnlyList<LevelRewardOffer> RewardOffers => _rewardOffers;

        public bool IsCampaignComplete =>
            _runMode == LevelRunMode.Campaign
            && _outcome == LevelOutcome.Won
            && HasLevels
            && _levelIndex >= database.Count - 1;

        public bool IsRunComplete =>
            _outcome == LevelOutcome.Won
            && (_runMode == LevelRunMode.Practice || IsCampaignComplete);

        public bool CanAdvanceTurn =>
            (!_started || !HasLevels)
            || (_outcome == LevelOutcome.InProgress
                && !_briefingActive
                && !(EventManager.Instance?.HasPendingEvent ?? false));

        public bool HasActiveClearRewards => _clearRewardsActive;

        public bool CanAdvanceToNextLevel =>
            _runMode == LevelRunMode.Campaign
            && _outcome == LevelOutcome.Won
            && !_outroActive
            && _rewardClaimed
            && !(EventManager.Instance?.HasPendingEventSequence ?? false)
            && HasLevels
            && _levelIndex < database.Count - 1;

        public event Action<LevelItem> LevelStarted;
        public event Action<LevelOutcome> LevelFinished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            Initialize();
        }

        public static void Initialize(LevelDatabase db = null)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(LevelManager));
                Instance = go.AddComponent<LevelManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            if (db != null)
                Instance.database = db;
            Instance.database?.RebuildIndex();
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

            database?.RebuildIndex();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!_subscribedTurns)
                TrySubscribeTurns();
        }

        private void TrySubscribeTurns()
        {
            var turns = TurnManager.Instance;
            if (turns == null) return;

            turns.TurnResolved -= OnTurnResolved;
            turns.TurnResolved += OnTurnResolved;
            _subscribedTurns = true;
        }

        private void OnEnable() => TrySubscribeTurns();

        private void OnDisable()
        {
            var turns = TurnManager.Instance;
            if (turns != null)
                turns.TurnResolved -= OnTurnResolved;
            _subscribedTurns = false;
        }

        private void OnTurnResolved(TurnResult result)
        {
            if (!_started || !HasLevels || Current == null) return;
            if (_outcome != LevelOutcome.InProgress) return;

            _levelTurnIndex++;
            if (result != null)
                _scoreGainedInLevel += Mathf.Max(0, result.ScoreGained);

            bool reachedTarget = _scoreGainedInLevel >= Current.TargetScore;
            bool usedLastTurn = _levelTurnIndex >= Current.MaxTurns;
            if (!reachedTarget && !usedLastTurn) return;

            // 达标或最后一回合都先结算酸涩，再做最终胜负判定。
            var settlement = TurnManager.Instance?.SettleStage();
            _lastSourUsed = settlement != null ? settlement.SourUsed : 0;
            _lastSourScore = settlement != null ? settlement.SourScore : 0;
            _scoreGainedInLevel += _lastSourScore;

            if (_scoreGainedInLevel >= Current.TargetScore)
                CompleteLevel();
            else
                FailLevel();
        }

        private void CompleteLevel()
        {
            _outcome = LevelOutcome.Won;
            _clearRewardsActive = true;
            _outroActive = !string.IsNullOrWhiteSpace(Current?.StoryOutro);

            if (_runMode == LevelRunMode.Practice)
            {
                _rewardClaimed = true;
                _rewardOffers.Clear();
            }
            else
            {
                if (_levelIndex + 1 > UnlockedLevelIndex)
                    UnlockedLevelIndex = _levelIndex + 1;
                PrepareRewardOffers();
            }

            QueuePostRewardEventsIfReady();
            LevelFinished?.Invoke(_outcome);
        }

        private void FailLevel()
        {
            _outcome = LevelOutcome.Lost;
            _clearRewardsActive = false;
            _outroActive = false;
            _rewardOffers.Clear();
            LevelFinished?.Invoke(_outcome);
        }

        /// <summary>开始连续战役或单关练习。</summary>
        public bool BeginRun(int startLevelIndex, LevelRunMode mode)
        {
            if (!HasLevels) return false;
            if (startLevelIndex < 0 || startLevelIndex >= database.Count) return false;
            if (mode == LevelRunMode.Campaign)
                startLevelIndex = 0;

            _runMode = mode;
            _levelStartSnapshotJson = string.Empty;
            return BeginLevelInternal(startLevelIndex);
        }

        /// <summary>兼容旧入口；直接进入指定关卡时按练习模式处理。</summary>
        public void BeginLevel(int index)
        {
            _runMode = index == 0 ? LevelRunMode.Campaign : LevelRunMode.Practice;
            BeginLevelInternal(index);
        }

        private bool BeginLevelInternal(int index)
        {
            if (!HasLevels || index < 0 || index >= database.Count) return false;

            _levelIndex = index;
            _levelTurnIndex = 0;
            _scoreGainedInLevel = 0;
            _outcome = LevelOutcome.InProgress;
            _clearRewardsActive = false;
            _rewardClaimed = true;
            _briefingActive = true;
            _outroActive = false;
            _lastSourUsed = 0;
            _lastSourScore = 0;
            _rewardOffers.Clear();
            _started = true;

            TurnManager.Instance?.ResetLevelScore();
            LevelStarted?.Invoke(Current);
            return true;
        }

        public void AcknowledgeBriefing()
        {
            if (_started && _outcome == LevelOutcome.InProgress)
                _briefingActive = false;
        }

        public void AcknowledgeOutro()
        {
            if (_outcome == LevelOutcome.Won)
            {
                _outroActive = false;
                QueuePostRewardEventsIfReady();
            }
        }

        /// <summary>
        /// 开局三次选择完成后、以及进入下一关时保存完整关卡起点。
        /// </summary>
        public void CommitLevelStartSnapshot()
        {
            if (!_started || !HasLevels) return;
            var snapshot = GameSaveService.Capture();
            snapshot.LevelStartSnapshotJson = string.Empty;
            _levelStartSnapshotJson = JsonUtility.ToJson(snapshot);
        }

        public bool AdvanceToNextLevel()
        {
            if (!CanAdvanceToNextLevel) return false;

            int next = _levelIndex + 1;
            if (!BeginLevelInternal(next)) return false;
            CommitLevelStartSnapshot();
            return true;
        }

        /// <summary>失败重试：恢复进入本关时的资源、员工、遗物、岗位、事件和随机数状态。</summary>
        public bool RetryCurrentLevel()
        {
            if (!HasLevels || _outcome != LevelOutcome.Lost) return false;
            if (string.IsNullOrWhiteSpace(_levelStartSnapshotJson))
                return BeginLevelInternal(_levelIndex);

            string preservedSnapshot = _levelStartSnapshotJson;
            var data = JsonUtility.FromJson<GameSaveData>(preservedSnapshot);
            if (data == null) return false;

            GameSaveService.Apply(data);
            _levelStartSnapshotJson = preservedSnapshot;
            TurnManager.Instance?.ClearUndoSnapshot();
            return true;
        }

        public bool TryClaimReward(int offerIndex)
        {
            if (_runMode != LevelRunMode.Campaign || _outcome != LevelOutcome.Won) return false;
            if (_rewardClaimed || offerIndex < 0 || offerIndex >= _rewardOffers.Count) return false;

            var offer = _rewardOffers[offerIndex];
            bool applied = ApplyReward(offer);
            if (!applied) return false;

            _rewardClaimed = true;
            _rewardOffers.Clear();
            QueuePostRewardEventsIfReady();
            return true;
        }

        private static bool ApplyReward(LevelRewardOffer offer)
        {
            if (offer == null || string.IsNullOrWhiteSpace(offer.ContentId)) return false;

            if (offer.Kind == LevelRewardKind.Relic)
                return RelicManager.Instance?.AcquireById(offer.ContentId) == true;

            var jobs = JobManager.Instance;
            var progression = JobProgressionManager.Instance;
            var job = jobs != null ? jobs.GetById(offer.ContentId) : null;
            if (job == null || progression == null) return false;

            return offer.Kind switch
            {
                LevelRewardKind.GatherJob => progression.TryUnlockGatherJob(job),
                LevelRewardKind.ProcessJob => progression.TryUnlockProcessJob(job),
                LevelRewardKind.UpgradeJob => progression.TryUpgrade(job),
                _ => false
            };
        }

        private void PrepareRewardOffers()
        {
            _rewardOffers.Clear();
            _rewardClaimed = false;

            var progression = JobProgressionManager.Instance;
            var relics = RelicManager.Instance;
            var rng = new System.Random(1733 + _levelIndex * 7919);
            JobItem upgradeReward = null;

            if (progression != null)
            {
                if (progression.CanUnlockMore(JobType.Gather))
                {
                    var gather = progression.GetLocked(JobType.Gather);
                    AddJobOffer(Pick(gather, rng), LevelRewardKind.GatherJob, "新采集岗");
                }

                if (progression.CanUnlockMore(JobType.Process))
                {
                    var process = progression.GetLocked(JobType.Process);
                    AddJobOffer(Pick(process, rng), LevelRewardKind.ProcessJob, "新处理岗");
                }

                var upgradeable = new List<JobItem>();
                foreach (var job in progression.Unlocked)
                {
                    if (job != null && progression.CanUpgrade(job))
                        upgradeable.Add(job);
                }

                upgradeReward = Pick(upgradeable, rng);
                if (_levelIndex == 0)
                    AddJobOffer(upgradeReward, LevelRewardKind.UpgradeJob, "岗位进阶");
            }

            if (_rewardOffers.Count < 3 && relics != null)
            {
                var candidates = relics.GetRelicsForStage(RelicAcquireStage.Event);
                candidates.RemoveAll(item => item == null || relics.Has(item));
                while (_rewardOffers.Count < 3 && candidates.Count > 0)
                {
                    var relic = Pop(candidates, rng);
                    _rewardOffers.Add(new LevelRewardOffer
                    {
                        Kind = LevelRewardKind.Relic,
                        ContentId = relic.Id,
                        Title = $"事件遗物 · {relic.DisplayName}",
                        Description = relic.Description
                    });
                }
            }

            if (_rewardOffers.Count < 3)
                AddJobOffer(upgradeReward, LevelRewardKind.UpgradeJob, "岗位进阶");

            if (_rewardOffers.Count == 0)
                _rewardClaimed = true;
        }

        private void QueuePostRewardEventsIfReady()
        {
            if (_runMode != LevelRunMode.Campaign || _outcome != LevelOutcome.Won) return;
            if (_outroActive || !_rewardClaimed) return;
            if (EventManager.Instance?.HasPendingEventSequence == true) return;
            EventManager.Instance?.QueueLevelClearEvents(_levelIndex + 1);
        }

        private void AddJobOffer(JobItem job, LevelRewardKind kind, string prefix)
        {
            if (job == null || _rewardOffers.Count >= 3) return;
            string description = kind == LevelRewardKind.UpgradeJob
                ? JobProgressionManager.Instance?.DescribeUpgradePreview(job)
                : job.GetEffectSummary();
            _rewardOffers.Add(new LevelRewardOffer
            {
                Kind = kind,
                ContentId = job.Id,
                Title = $"{prefix} · {job.DisplayName}",
                Description = description ?? string.Empty
            });
        }

        private static T Pick<T>(List<T> values, System.Random rng) where T : class
        {
            if (values == null || values.Count == 0) return null;
            return values[rng.Next(values.Count)];
        }

        private static T Pop<T>(List<T> values, System.Random rng) where T : class
        {
            if (values == null || values.Count == 0) return null;
            int index = rng.Next(values.Count);
            T value = values[index];
            values.RemoveAt(index);
            return value;
        }

        /// <summary>调试：强制打开正式关间流程。</summary>
        public void DebugForceOpenClearRewards()
        {
            if (!HasLevels || _outcome == LevelOutcome.Lost) return;
            _outcome = LevelOutcome.Won;
            _clearRewardsActive = true;
            _outroActive = false;
            if (_runMode == LevelRunMode.Campaign)
                PrepareRewardOffers();
        }

        public void ResetRun(bool beginFirstLevel = true)
        {
            _started = false;
            _runMode = LevelRunMode.Campaign;
            _levelIndex = 0;
            _levelTurnIndex = 0;
            _scoreGainedInLevel = 0;
            _outcome = LevelOutcome.InProgress;
            _clearRewardsActive = false;
            _rewardClaimed = true;
            _briefingActive = false;
            _outroActive = false;
            _lastSourUsed = 0;
            _lastSourScore = 0;
            _levelStartSnapshotJson = string.Empty;
            _rewardOffers.Clear();
            if (beginFirstLevel && HasLevels)
                BeginRun(0, LevelRunMode.Campaign);
        }

        // ------------------------------------------------------------------ save

        public void CaptureState(
            out int levelIndex,
            out int turnIndex,
            out int scoreInLevel,
            out int outcomeInt,
            out bool clearRewardsActive)
        {
            levelIndex = _levelIndex;
            turnIndex = _levelTurnIndex;
            scoreInLevel = _scoreGainedInLevel;
            outcomeInt = (int)_outcome;
            clearRewardsActive = _clearRewardsActive;
        }

        public void CaptureExtendedState(GameSaveData data)
        {
            if (data == null) return;
            data.LevelRunModeInt = (int)_runMode;
            data.LevelRewardClaimed = _rewardClaimed;
            data.LevelBriefingActive = _briefingActive;
            data.LevelOutroActive = _outroActive;
            data.LevelStarted = _started;
            data.LevelLastSourUsed = _lastSourUsed;
            data.LevelLastSourScore = _lastSourScore;
            data.LevelStartSnapshotJson = _levelStartSnapshotJson ?? string.Empty;
            for (int i = 0; i < _rewardOffers.Count; i++)
            {
                var offer = _rewardOffers[i];
                data.LevelRewardKinds.Add((int)offer.Kind);
                data.LevelRewardContentIds.Add(offer.ContentId);
                data.LevelRewardTitles.Add(offer.Title);
                data.LevelRewardDescriptions.Add(offer.Description);
            }
        }

        public void ApplyState(
            int levelIndex,
            int turnIndex,
            int scoreInLevel,
            int outcomeInt,
            bool clearRewardsActive)
        {
            if (!HasLevels) return;
            _levelIndex = Mathf.Clamp(levelIndex, 0, database.Count - 1);
            _levelTurnIndex = Mathf.Max(0, turnIndex);
            _scoreGainedInLevel = Mathf.Max(0, scoreInLevel);
            _outcome = (LevelOutcome)Mathf.Clamp(outcomeInt, 0, 2);
            _clearRewardsActive = clearRewardsActive;
            _started = true;
        }

        public void ApplyExtendedState(GameSaveData data)
        {
            if (data == null) return;
            _runMode = (LevelRunMode)Mathf.Clamp(data.LevelRunModeInt, 0, 1);
            _rewardClaimed = data.LevelRewardClaimed;
            _briefingActive = data.LevelBriefingActive;
            _outroActive = data.LevelOutroActive;
            _started = data.LevelStarted || _started;
            _lastSourUsed = Mathf.Max(0, data.LevelLastSourUsed);
            _lastSourScore = Mathf.Max(0, data.LevelLastSourScore);
            _levelStartSnapshotJson = data.LevelStartSnapshotJson ?? string.Empty;
            _rewardOffers.Clear();

            if (data.LevelRewardKinds == null
                || data.LevelRewardContentIds == null
                || data.LevelRewardTitles == null
                || data.LevelRewardDescriptions == null)
                return;

            int count = Mathf.Min(
                data.LevelRewardKinds.Count,
                Mathf.Min(
                    data.LevelRewardContentIds.Count,
                    Mathf.Min(data.LevelRewardTitles.Count, data.LevelRewardDescriptions.Count)));
            for (int i = 0; i < count; i++)
            {
                _rewardOffers.Add(new LevelRewardOffer
                {
                    Kind = (LevelRewardKind)Mathf.Clamp(data.LevelRewardKinds[i], 0, 3),
                    ContentId = data.LevelRewardContentIds[i] ?? string.Empty,
                    Title = data.LevelRewardTitles[i] ?? string.Empty,
                    Description = data.LevelRewardDescriptions[i] ?? string.Empty
                });
            }
        }
    }
}
