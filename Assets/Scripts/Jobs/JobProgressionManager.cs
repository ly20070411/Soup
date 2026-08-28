using System.Collections.Generic;
using Soup.Employees;
using Soup.Game;
using Soup.Relics;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// Runtime unlock / tree-advance state for jobs in the current run.
    /// </summary>
    [DefaultExecutionOrder(-95)]
    public class JobProgressionManager : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly HashSet<JobItem> _unlocked = new HashSet<JobItem>();
        private readonly Dictionary<JobItem, JobAdvanceNodeId> _advancePaths = new Dictionary<JobItem, JobAdvanceNodeId>();
        /// <summary>我爱坨坨：所有采集岗按快乐坨坨结算/展示，进阶节点 id 仍挂在原岗位上。</summary>
        private bool _gatherJobsActAsHappyTuotuo;
        private readonly Dictionary<JobItem, JobItem> _designatedGatherAuraTargets = new Dictionary<JobItem, JobItem>();
        private readonly List<JobItem> _destroyedGatherJobs = new List<JobItem>();
        private readonly Dictionary<JobItem, float> _pendingGatherEfficiencyPenalty = new Dictionary<JobItem, float>();
        private readonly Dictionary<JobItem, JobEventMods> _eventMods = new Dictionary<JobItem, JobEventMods>();
        private readonly List<JobItem> _gatherOffer = new List<JobItem>();
        private readonly List<JobAdvanceNodeId> _choiceBuffer = new List<JobAdvanceNodeId>(2);
        private readonly List<JobAdvanceNodeId> _chainBuffer = new List<JobAdvanceNodeId>(2);

        private bool _gatherStarterPicked;
        private bool _processStarterPicked;
        private int _endTurnIncentivesGrantedThisLevel;
        private bool _levelEventsBound;

        public static JobProgressionManager Instance { get; private set; }

        public bool NeedsGatherStarterPick => !_gatherStarterPicked;
        public bool NeedsProcessStarterPick => !_processStarterPicked;
        public bool IsSetupComplete => _gatherStarterPicked && _processStarterPicked;

        public IReadOnlyCollection<JobItem> Unlocked => _unlocked;
        public IReadOnlyList<JobItem> CurrentGatherOffer => _gatherOffer;

        public static void Initialize()
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(JobProgressionManager));
                Instance = go.AddComponent<JobProgressionManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            Instance.ResetRun();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            Initialize();
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

            BootstrapDefaults();
            BindLevelEvents();
        }

        private void Start()
        {
            BootstrapDefaults();
            BindLevelEvents();
        }

        private void BindLevelEvents()
        {
            if (_levelEventsBound) return;
            var levels = Soup.Levels.LevelManager.Instance;
            if (levels == null) return;
            levels.LevelStarted -= OnLevelStarted;
            levels.LevelStarted += OnLevelStarted;
            _levelEventsBound = true;
        }

        private void OnLevelStarted(Soup.Levels.LevelItem _)
        {
            _endTurnIncentivesGrantedThisLevel = 0;
        }

        public void BootstrapDefaults()
        {
            if (JobManager.Instance == null) return;

            if (CountUnlocked(JobType.Cook) == 0)
                UnlockAllOfType(JobType.Cook);

            var mushroom = ResolveStartingGatherJob();
            if (mushroom != null)
                UnlockInternal(mushroom);
        }

        private void OnDestroy()
        {
            var levels = Soup.Levels.LevelManager.Instance;
            if (levels != null)
                levels.LevelStarted -= OnLevelStarted;
            if (Instance == this)
                Instance = null;
        }

        public void ResetRun()
        {
            _unlocked.Clear();
            _advancePaths.Clear();
            _designatedGatherAuraTargets.Clear();
            _destroyedGatherJobs.Clear();
            _pendingGatherEfficiencyPenalty.Clear();
            _eventMods.Clear();
            _gatherOffer.Clear();
            _endTurnIncentivesGrantedThisLevel = 0;
            _gatherStarterPicked = false;
            _processStarterPicked = false;
            _gatherJobsActAsHappyTuotuo = false;
            BootstrapDefaults();
        }

        public void ApplyState(
            IList<string> unlockedJobIds,
            IList<string> upgradeJobIds,
            IList<int> upgradePathIds,
            IList<string> gatherOfferIds,
            bool gatherStarterPicked,
            bool processStarterPicked,
            IList<string> auraSourceJobIds = null,
            IList<string> auraTargetJobIds = null,
            IList<string> destroyedGatherJobIds = null,
            IList<string> pendingEfficiencyPenaltyJobIds = null,
            IList<float> pendingEfficiencyPenaltyValues = null,
            int endTurnIncentivesGrantedThisLevel = 0,
            IList<JobEventModSave> eventMods = null,
            bool gatherJobsActAsHappyTuotuo = false)
        {
            _unlocked.Clear();
            _advancePaths.Clear();
            _designatedGatherAuraTargets.Clear();
            _destroyedGatherJobs.Clear();
            _pendingGatherEfficiencyPenalty.Clear();
            _eventMods.Clear();
            _gatherOffer.Clear();
            _endTurnIncentivesGrantedThisLevel = Mathf.Max(0, endTurnIncentivesGrantedThisLevel);
            _gatherStarterPicked = gatherStarterPicked;
            _processStarterPicked = processStarterPicked;
            _gatherJobsActAsHappyTuotuo = gatherJobsActAsHappyTuotuo;

            var jobs = JobManager.Instance;
            if (jobs != null && unlockedJobIds != null)
            {
                for (int i = 0; i < unlockedJobIds.Count; i++)
                {
                    var job = jobs.GetById(unlockedJobIds[i]);
                    if (job != null)
                        _unlocked.Add(job);
                }
            }

            if (jobs != null && upgradeJobIds != null && upgradePathIds != null)
            {
                int n = Mathf.Min(upgradeJobIds.Count, upgradePathIds.Count);
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(upgradeJobIds[i]))
                        continue;

                    var path = (JobAdvanceNodeId)upgradePathIds[i];
                    if (!JobAdvancePath.IsValid(path) || path == JobAdvanceNodeId.None)
                        continue;

                    var job = jobs.GetById(upgradeJobIds[i]);
                    if (job == null) continue;
                    _advancePaths[job] = path;
                    if (job.JobType != JobType.Cook)
                        _unlocked.Add(job);
                }
            }

            if (jobs != null && gatherOfferIds != null)
            {
                for (int i = 0; i < gatherOfferIds.Count; i++)
                {
                    var job = jobs.GetById(gatherOfferIds[i]);
                    if (job != null)
                        _gatherOffer.Add(job);
                }
            }

            if (jobs != null && auraSourceJobIds != null && auraTargetJobIds != null)
            {
                int n = Mathf.Min(auraSourceJobIds.Count, auraTargetJobIds.Count);
                for (int i = 0; i < n; i++)
                {
                    var source = jobs.GetById(auraSourceJobIds[i]);
                    var target = jobs.GetById(auraTargetJobIds[i]);
                    if (source == null || target == null) continue;
                    _designatedGatherAuraTargets[source] = target;
                }
            }

            if (jobs != null && destroyedGatherJobIds != null)
            {
                for (int i = 0; i < destroyedGatherJobIds.Count; i++)
                {
                    var job = jobs.GetById(destroyedGatherJobIds[i]);
                    if (job == null || job.JobType != JobType.Gather) continue;
                    if (!_destroyedGatherJobs.Contains(job))
                        _destroyedGatherJobs.Add(job);
                    _unlocked.Remove(job);
                    _advancePaths.Remove(job);
                }
            }

            if (jobs != null && pendingEfficiencyPenaltyJobIds != null && pendingEfficiencyPenaltyValues != null)
            {
                int n = Mathf.Min(pendingEfficiencyPenaltyJobIds.Count, pendingEfficiencyPenaltyValues.Count);
                for (int i = 0; i < n; i++)
                {
                    var job = jobs.GetById(pendingEfficiencyPenaltyJobIds[i]);
                    float penalty = pendingEfficiencyPenaltyValues[i];
                    if (job == null || penalty <= 0f) continue;
                    _pendingGatherEfficiencyPenalty[job] = penalty;
                }
            }

            if (jobs != null && eventMods != null)
            {
                for (int i = 0; i < eventMods.Count; i++)
                {
                    var row = eventMods[i];
                    if (row == null || string.IsNullOrEmpty(row.jobId)) continue;
                    var job = jobs.GetById(row.jobId);
                    if (job == null) continue;
                    _eventMods[job] = row.ToMods();
                }
            }

            BootstrapDefaults();
        }

        public bool IsUnlocked(JobItem job) => job != null && _unlocked.Contains(job);

        public JobAdvanceNodeId GetAdvancePath(JobItem job)
        {
            if (job == null) return JobAdvanceNodeId.None;
            return _advancePaths.TryGetValue(job, out var path) ? path : JobAdvanceNodeId.None;
        }

        public bool GatherJobsActAsHappyTuotuo => _gatherJobsActAsHappyTuotuo;

        /// <summary>
        /// 采集结算/进阶效果/展示所用的岗位定义。
        /// 我爱坨坨开启后，非快乐坨坨岗仍保留原解锁键与进阶路径，但读取快乐坨坨的数据树。
        /// </summary>
        public JobItem ResolveGatherDefinition(JobItem job)
        {
            if (job == null || job.JobType != JobType.Gather) return job;
            if (!_gatherJobsActAsHappyTuotuo) return job;
            if (job.Id == JobProgressionRules.HappyTuotuoJobId) return job;
            var happy = JobManager.Instance != null
                ? JobManager.Instance.GetById(JobProgressionRules.HappyTuotuoJobId)
                : null;
            return happy != null ? happy : job;
        }

        public bool HasUnlockedHappyTuotuoGather()
        {
            var happy = JobManager.Instance != null
                ? JobManager.Instance.GetById(JobProgressionRules.HappyTuotuoJobId)
                : null;
            return happy != null && IsUnlocked(happy) && !IsDestroyedGatherJob(happy);
        }

        /// <summary>我爱坨坨：全部已有采集岗改为按快乐坨坨运作（进阶分支路径保留）。</summary>
        public void ConvertAllGatherJobsToHappyTuotuo()
        {
            _gatherJobsActAsHappyTuotuo = true;
            GameFloatingToast.Show("我爱坨坨：所有采集岗变为快乐坨坨（进阶保留）", 3f);
        }

        public JobItem GetDesignatedGatherAuraTarget(JobItem source)
        {
            if (source == null) return null;
            return _designatedGatherAuraTargets.TryGetValue(source, out var target) ? target : null;
        }

        public bool SetDesignatedGatherAuraTarget(JobItem source, JobItem target)
        {
            if (source == null || target == null) return false;
            if (ReferenceEquals(source, target)) return false;
            if (source.JobType != JobType.Gather || target.JobType != JobType.Gather) return false;
            if (!IsUnlocked(source) || !IsUnlocked(target)) return false;
            _designatedGatherAuraTargets[source] = target;
            return true;
        }

        public void CaptureDesignatedGatherAuraTargets(List<string> sourceIds, List<string> targetIds)
        {
            sourceIds?.Clear();
            targetIds?.Clear();
            if (sourceIds == null || targetIds == null) return;
            foreach (var pair in _designatedGatherAuraTargets)
            {
                if (pair.Key == null || pair.Value == null) continue;
                if (string.IsNullOrEmpty(pair.Key.Id) || string.IsNullOrEmpty(pair.Value.Id)) continue;
                sourceIds.Add(pair.Key.Id);
                targetIds.Add(pair.Value.Id);
            }
        }

        public IReadOnlyList<JobItem> DestroyedGatherJobs => _destroyedGatherJobs;

        public bool IsDestroyedGatherJob(JobItem job) =>
            job != null && _destroyedGatherJobs.Contains(job);

        public void CaptureDestroyedGatherJobs(List<string> jobIds)
        {
            jobIds?.Clear();
            if (jobIds == null) return;
            for (int i = 0; i < _destroyedGatherJobs.Count; i++)
            {
                var job = _destroyedGatherJobs[i];
                if (job != null && !string.IsNullOrEmpty(job.Id))
                    jobIds.Add(job.Id);
            }
        }

        public void CapturePendingGatherEfficiencyPenalties(List<string> jobIds, List<float> values)
        {
            jobIds?.Clear();
            values?.Clear();
            if (jobIds == null || values == null) return;
            foreach (var pair in _pendingGatherEfficiencyPenalty)
            {
                if (pair.Key == null || string.IsNullOrEmpty(pair.Key.Id) || pair.Value <= 0f)
                    continue;
                jobIds.Add(pair.Key.Id);
                values.Add(pair.Value);
            }
        }

        /// <summary>读取该岗待结算的下一回合效率减成（不清除）。</summary>
        public float PeekPendingGatherEfficiencyPenalty(JobItem job)
        {
            if (job == null) return 0f;
            if (!_pendingGatherEfficiencyPenalty.TryGetValue(job, out float penalty))
                return 0f;
            return Mathf.Max(0f, penalty);
        }

        /// <summary>读取并清除该岗待结算的下一回合效率减成。</summary>
        public float ConsumePendingGatherEfficiencyPenalty(JobItem job)
        {
            if (job == null) return 0f;
            if (!_pendingGatherEfficiencyPenalty.TryGetValue(job, out float penalty))
                return 0f;
            _pendingGatherEfficiencyPenalty.Remove(job);
            return Mathf.Max(0f, penalty);
        }

        public void SetPendingGatherEfficiencyPenalty(JobItem job, float penalty)
        {
            if (job == null || penalty <= 0f) return;
            _pendingGatherEfficiencyPenalty[job] = penalty;
        }

        /// <summary>
        /// 本回合未采集的岗位清除待结算效率惩罚（如小刺球 2-2：休一轮则恢复，不空扣产量）。
        /// </summary>
        public void RecoverPendingGatherEfficiencyPenaltyForJobsNotGathered(
            ICollection<JobItem> gatheredThisTurn)
        {
            if (_pendingGatherEfficiencyPenalty.Count == 0) return;

            var gathered = gatheredThisTurn != null
                ? new HashSet<JobItem>(gatheredThisTurn)
                : new HashSet<JobItem>();

            var toClear = new List<JobItem>();
            foreach (var pair in _pendingGatherEfficiencyPenalty)
            {
                if (pair.Key != null && !gathered.Contains(pair.Key))
                    toClear.Add(pair.Key);
            }

            for (int i = 0; i < toClear.Count; i++)
                _pendingGatherEfficiencyPenalty.Remove(toClear[i]);
        }

        public int EndTurnIncentivesGrantedThisLevel => _endTurnIncentivesGrantedThisLevel;

        /// <summary>
        /// 回合结束：快乐坨坨等岗位按在岗人数掷骰产出激励，受每关上限约束。
        /// </summary>
        public int TryGrantEndTurnIncentives()
        {
            if (!JobAdvanceGatherMods.TryGetEndTurnIncentiveRoll(
                    out var sourceJob, out float chance, out int maxPerLevel))
                return 0;

            int remaining = maxPerLevel - _endTurnIncentivesGrantedThisLevel;
            if (remaining <= 0) return 0;

            var employees = EmployeeManager.Instance;
            int workers = employees != null ? employees.GetAssignedCountOnJob(sourceJob) : 0;
            if (workers <= 0) return 0;

            var relics = Soup.Relics.RelicManager.Instance;
            if (relics == null) return 0;
            var incentive = relics.GetById(Soup.Relics.RelicManager.IncentiveId);
            if (incentive == null) return 0;

            int granted = 0;
            for (int i = 0; i < workers && granted < remaining; i++)
            {
                if (UnityEngine.Random.value >= chance) continue;
                if (!relics.Acquire(incentive)) continue;
                granted++;
                _endTurnIncentivesGrantedThisLevel++;
            }

            return granted;
        }

        public List<JobItem> GetDestroyableGatherJobs(JobItem except)
        {
            var list = GetUnlocked(JobType.Gather);
            // 永久起始岗（蘑菇）不可被献祭/摧毁，与替换流程的 IsPermanentGatherJob 保护一致。
            list.RemoveAll(j => j == null
                || ReferenceEquals(j, except)
                || IsDestroyedGatherJob(j)
                || IsPermanentGatherJob(j));
            return list;
        }

        public bool TryDestroyGatherJob(JobItem job)
        {
            if (job == null || job.JobType != JobType.Gather) return false;
            if (!IsUnlocked(job)) return false;
            if (IsDestroyedGatherJob(job)) return false;
            if (IsPermanentGatherJob(job)) return false;

            // Unassign occupying workers before locking.
            var elves = Soup.Game.ElfManager.Instance;
            if (elves != null)
            {
                int assigned = elves.GetAssigned(job);
                if (assigned > 0)
                    elves.TryUnassign(job, assigned);
            }

            LockInternal(job);
            if (!_destroyedGatherJobs.Contains(job))
                _destroyedGatherJobs.Add(job);

            Soup.Game.GameFloatingToast.Show($"摧毁采集岗：{job.DisplayName}", 2.8f);
            return true;
        }

        public bool TryDestroyRandomOtherGather(JobItem except, out JobItem destroyed)
        {
            destroyed = null;
            var candidates = GetDestroyableGatherJobs(except);
            if (candidates.Count == 0) return false;
            destroyed = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return TryDestroyGatherJob(destroyed);
        }

        /// <summary>进阶深度：0 / 1 / 2。</summary>
        public int GetUpgradeLevel(JobItem job) => JobAdvancePath.Depth(GetAdvancePath(job));

        public bool HasAdvanceNode(JobItem job, JobAdvanceNodeId node) =>
            JobAdvancePath.HasTaken(GetAdvancePath(job), node);

        public int GetEffectiveMaxWorkers(JobItem job)
        {
            if (job == null) return 0;
            var def = ResolveGatherDefinition(job);
            if (def == null) return 0;
            if (!def.HasWorkerLimit) return int.MaxValue;
            int cap = def.GetEffectiveMaxWorkers(GetAdvancePath(job));
            var mods = GetEventMods(job);
            if (mods != null)
                cap += mods.MaxWorkersDelta;
            cap += RelicEffectRunner.SumAllJobMaxWorkersBonus();
            return Mathf.Max(0, cap);
        }

        public JobEventMods GetEventMods(JobItem job)
        {
            if (job == null) return null;
            return _eventMods.TryGetValue(job, out var mods) ? mods : null;
        }

        public float GetEventYieldMultiplier(JobItem job)
        {
            var mods = GetEventMods(job);
            if (mods == null) return 1f;
            return Mathf.Max(0f, 1f + mods.YieldBonus);
        }

        public JobEventMods ModifyEventMods(JobItem job)
        {
            if (job == null) return null;
            if (!_eventMods.TryGetValue(job, out var mods) || mods == null)
            {
                mods = new JobEventMods();
                _eventMods[job] = mods;
            }

            return mods;
        }

        public void AddEventYieldBonus(JobItem job, float bonus)
        {
            var mods = ModifyEventMods(job);
            if (mods == null) return;
            mods.YieldBonus += bonus;
        }

        public void AddEventMaxWorkersDelta(JobItem job, int delta)
        {
            var mods = ModifyEventMods(job);
            if (mods == null) return;
            mods.MaxWorkersDelta += delta;
            EmployeeManager.Instance?.ClampAssignmentsToCapacity();
        }

        public void AddEventRawAndColdPerUnit(JobItem job, int rawDelta, int coldDelta)
        {
            var mods = ModifyEventMods(job);
            if (mods == null) return;
            mods.RawPerUnitDelta += rawDelta;
            mods.ColdPerUnitDelta += coldDelta;
        }

        public void EnableEventAllFourFlavors(JobItem job)
        {
            var mods = ModifyEventMods(job);
            if (mods == null) return;
            mods.ProduceAllFourFlavors = true;
        }

        public void CaptureEventMods(List<JobEventModSave> dest)
        {
            dest?.Clear();
            if (dest == null) return;
            foreach (var pair in _eventMods)
            {
                if (pair.Key == null || string.IsNullOrEmpty(pair.Key.Id) || pair.Value == null)
                    continue;
                dest.Add(JobEventModSave.From(pair.Key.Id, pair.Value));
            }
        }

        public int CountUnlocked(JobType type)
        {
            int count = 0;
            foreach (var job in _unlocked)
            {
                if (job != null && job.JobType == type)
                    count++;
            }

            return count;
        }

        public List<JobItem> GetUnlocked(JobType type)
        {
            var list = new List<JobItem>();
            foreach (var job in _unlocked)
            {
                if (job != null && job.JobType == type)
                    list.Add(job);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return list;
        }

        public List<JobItem> GetLocked(JobType type)
        {
            var list = new List<JobItem>();
            var jobs = JobManager.Instance != null ? JobManager.Instance.All : null;
            if (jobs == null) return list;

            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                if (job == null || job.JobType != type) continue;
                if (_unlocked.Contains(job)) continue;
                if (type == JobType.Gather && IsDestroyedGatherJob(job)) continue;
                list.Add(job);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return list;
        }

        public bool CanUnlockMore(JobType type)
        {
            if (type == JobType.Cook) return false;
            int max = JobProgressionRules.MaxStations(type);
            int used = CountUnlocked(type);
            if (type == JobType.Gather)
                used += _destroyedGatherJobs.Count;
            return used < max && GetLocked(type).Count > 0;
        }

        public bool CanReplaceGather =>
            CountUnlocked(JobType.Gather) >= JobProgressionRules.GatherMaxStations
            && GetLocked(JobType.Gather).Count > 0
            && GetReplaceableGatherJobs().Count > 0;

        public bool IsPermanentGatherJob(JobItem job)
        {
            if (job == null || job.JobType != JobType.Gather) return false;
            return job.Id == JobProgressionRules.StartingGatherJobId;
        }

        public List<JobItem> GetReplaceableGatherJobs()
        {
            var list = GetUnlocked(JobType.Gather);
            list.RemoveAll(IsPermanentGatherJob);
            return list;
        }

        public bool CanUpgrade(JobItem job)
        {
            if (!CanUpgradeBasics(job)) return false;

            JobAdvancePath.GetChoices(GetAdvancePath(job), _choiceBuffer);
            for (int i = 0; i < _choiceBuffer.Count; i++)
            {
                if (IsAdvanceChoiceAllowed(job, _choiceBuffer[i]))
                    return true;
            }

            return false;
        }

        public bool CanAdvance(JobItem job, JobAdvanceNodeId choice)
        {
            if (!CanUpgradeBasics(job)) return false;
            return IsAdvanceChoiceAllowed(job, choice);
        }

        public void GetAvailableAdvanceChoices(JobItem job, List<JobAdvanceNodeId> results)
        {
            results?.Clear();
            if (results == null || job == null) return;
            if (!CanUpgradeBasics(job)) return;
            JobAdvancePath.GetChoices(GetAdvancePath(job), results);
            for (int i = results.Count - 1; i >= 0; i--)
            {
                if (!IsAdvanceChoiceAllowed(job, results[i]))
                    results.RemoveAt(i);
            }
        }

        private bool CanUpgradeBasics(JobItem job)
        {
            if (job == null) return false;
            if (job.JobType != JobType.Cook && !IsUnlocked(job)) return false;

            int depth = GetUpgradeLevel(job);
            int max = JobProgressionRules.MaxUpgradesPerJob(job.JobType);
            if (depth >= max) return false;

            return JobAdvancePath.TryGetChoices(GetAdvancePath(job), out _, out _);
        }

        private bool IsAdvanceChoiceAllowed(JobItem job, JobAdvanceNodeId choice)
        {
            if (!JobAdvancePath.IsValidNext(GetAdvancePath(job), choice)) return false;

            var def = ResolveGatherDefinition(job);
            def.EnsureAdvanceTreeDefaults();
            var node = def.GetAdvanceNode(choice);
            if (node != null && node.IsNoneAdvanceNode())
                return false;
            if (node != null && node.DestroyOtherGatherOnTake && GetDestroyableGatherJobs(job).Count == 0)
                return false;

            return true;
        }

        /// <summary>按选定树节点进阶。互斥：选 1 后不能再走 2；选 1-1 后不能再走 1-2。</summary>
        public bool TryAdvance(JobItem job, JobAdvanceNodeId choice)
        {
            return TryAdvance(job, choice, out _);
        }

        public bool TryAdvance(JobItem job, JobAdvanceNodeId choice, out JobItem destroyedGather)
        {
            destroyedGather = null;
            if (!CanAdvance(job, choice)) return false;

            _advancePaths[job] = choice;
            if (job.JobType != JobType.Cook)
                UnlockInternal(job);

            ApplyAdvanceOnTake(job, choice, out destroyedGather);
            EmployeeManager.Instance?.ClampAssignmentsToCapacity();
            return true;
        }

        private void ApplyAdvanceOnTake(JobItem job, JobAdvanceNodeId choice, out JobItem destroyedGather)
        {
            destroyedGather = null;
            if (job == null || choice == JobAdvanceNodeId.None) return;
            var def = ResolveGatherDefinition(job);
            def.EnsureAdvanceTreeDefaults();
            var node = def.GetAdvanceNode(choice);
            if (node == null) return;

            if (node.GrantEmployeeCount > 0 && !string.IsNullOrWhiteSpace(node.GrantEmployeeId))
                EmployeeManager.Instance?.Add(node.GrantEmployeeId, node.GrantEmployeeCount);

            if (node.NeedsDesignatedGatherTarget)
                EnsureDesignatedGatherAuraTarget(job);

            if (node.DestroyOtherGatherOnTake)
                TryDestroyRandomOtherGather(job, out destroyedGather);
        }

        private void EnsureDesignatedGatherAuraTarget(JobItem source)
        {
            if (source == null) return;
            if (GetDesignatedGatherAuraTarget(source) != null) return;

            foreach (var candidate in GetUnlocked(JobType.Gather))
            {
                if (candidate == null || ReferenceEquals(candidate, source)) continue;
                SetDesignatedGatherAuraTarget(source, candidate);
                return;
            }
        }

        /// <summary>调试用：自动选当前可选的第一条分支。</summary>
        public bool TryUpgrade(JobItem job)
        {
            GetAvailableAdvanceChoices(job, _choiceBuffer);
            if (_choiceBuffer.Count == 0) return false;
            return TryAdvance(job, _choiceBuffer[0]);
        }

        public bool TryPickGatherStarter(JobItem job)
        {
            if (_gatherStarterPicked || job == null || job.JobType != JobType.Gather)
                return false;
            if (job.Id == JobProgressionRules.StartingGatherJobId)
                return false;

            UnlockInternal(job);
            _gatherStarterPicked = true;
            return true;
        }

        public bool TryPickProcessStarter(JobItem job)
        {
            if (_processStarterPicked || job == null || job.JobType != JobType.Process)
                return false;

            UnlockInternal(job);
            _processStarterPicked = true;
            return true;
        }

        /// <summary>无可选岗位时跳过开局采集选择（仍保留蘑菇等默认岗）。</summary>
        public void MarkGatherStarterComplete()
        {
            _gatherStarterPicked = true;
        }

        /// <summary>无可选岗位时跳过开局处理选择。</summary>
        public void MarkProcessStarterComplete()
        {
            _processStarterPicked = true;
        }

        public bool TryUnlockFromGatherOffer(JobItem job)
        {
            if (job == null || !_gatherOffer.Contains(job)) return false;
            if (!CanUnlockMore(JobType.Gather)) return false;

            bool ok = UnlockInternal(job);
            if (ok)
                _gatherOffer.Clear();
            return ok;
        }

        /// <summary>Unlock any locked gather job into a free station (no offer gate).</summary>
        public bool TryUnlockGatherJob(JobItem job)
        {
            if (job == null || job.JobType != JobType.Gather) return false;
            if (IsUnlocked(job)) return false;
            if (!CanUnlockMore(JobType.Gather)) return false;
            bool ok = UnlockInternal(job);
            if (ok)
                _gatherOffer.Clear();
            return ok;
        }

        public bool TryReplaceGatherJob(JobItem outgoing, JobItem incoming)
        {
            if (!CanReplaceGather) return false;
            if (outgoing == null || incoming == null) return false;
            if (outgoing.JobType != JobType.Gather || incoming.JobType != JobType.Gather) return false;
            if (IsPermanentGatherJob(outgoing)) return false;
            if (!IsUnlocked(outgoing)) return false;
            if (IsUnlocked(incoming)) return false;
            if (_gatherOffer.Count > 0 && !_gatherOffer.Contains(incoming)) return false;

            LockInternal(outgoing);
            UnlockInternal(incoming);
            _gatherOffer.Clear();
            return true;
        }

        public bool TryUnlockProcessJob(JobItem job)
        {
            if (job == null || job.JobType != JobType.Process) return false;
            if (IsUnlocked(job)) return false;
            if (!CanUnlockMore(JobType.Process)) return false;
            return UnlockInternal(job);
        }

        public void RefreshGatherOffer(System.Random rng = null)
        {
            _gatherOffer.Clear();
            if (!CanUnlockMore(JobType.Gather) && !CanReplaceGather) return;

            var locked = GetLocked(JobType.Gather);
            if (locked.Count == 0) return;

            int offerCount = Mathf.Min(JobProgressionRules.GatherNewJobOfferCount, locked.Count);
            Shuffle(locked, rng);
            for (int i = 0; i < offerCount; i++)
                _gatherOffer.Add(locked[i]);
        }

        public string DescribeNode(JobItem job, JobAdvanceNodeId nodeId)
        {
            if (job == null || nodeId == JobAdvanceNodeId.None)
                return string.Empty;

            var def = ResolveGatherDefinition(job);
            def.EnsureAdvanceTreeDefaults();
            var node = def.GetAdvanceNode(nodeId);
            return node != null ? node.ToSummary(nodeId) : JobAdvancePath.ToLabel(nodeId);
        }

        public string DescribeCurrentPath(JobItem job)
        {
            if (job == null) return string.Empty;
            var path = GetAdvancePath(job);
            if (path == JobAdvanceNodeId.None)
                return "未进阶";

            JobAdvancePath.GetChain(path, _chainBuffer);
            var parts = new List<string>(_chainBuffer.Count);
            for (int i = 0; i < _chainBuffer.Count; i++)
                parts.Add(JobAdvancePath.ToLabel(_chainBuffer[i]));
            return string.Join(" → ", parts);
        }

        public string DescribeUpgradePreview(JobItem job)
        {
            if (job == null) return string.Empty;
            if (!CanUpgrade(job))
                return GetUpgradeLevel(job) >= JobProgressionRules.MaxUpgradesPerJob(job.JobType)
                    ? "已满级"
                    : "不可进阶";

            GetAvailableAdvanceChoices(job, _choiceBuffer);
            if (_choiceBuffer.Count == 0) return "已满级";

            var def = ResolveGatherDefinition(job);
            def.EnsureAdvanceTreeDefaults();
            var parts = new List<string>(_choiceBuffer.Count);
            for (int i = 0; i < _choiceBuffer.Count; i++)
                parts.Add(DescribeNode(job, _choiceBuffer[i]));
            return string.Join("  |  ", parts);
        }

        public string DescribeAdvanceTree(JobItem job)
        {
            if (job == null) return string.Empty;
            var def = ResolveGatherDefinition(job);
            def.EnsureAdvanceTreeDefaults();
            return def.BuildTreeDiagram(GetAdvancePath(job));
        }

        private JobItem ResolveStartingGatherJob()
        {
            var jobs = JobManager.Instance;
            if (jobs == null) return null;

            var byId = jobs.GetById(JobProgressionRules.StartingGatherJobId);
            if (byId != null) return byId;
            return jobs.FindByName("蘑菇");
        }

        private void UnlockAllOfType(JobType type)
        {
            var jobs = JobManager.Instance != null ? JobManager.Instance.All : null;
            if (jobs == null) return;

            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                if (job != null && job.JobType == type)
                    UnlockInternal(job);
            }
        }

        private bool UnlockInternal(JobItem job)
        {
            if (job == null) return false;
            if (IsDestroyedGatherJob(job)) return false;
            if (!_unlocked.Add(job)) return false;
            // 新解锁采集岗后，为尚无目标的「指定其它岗」效果补绑。
            if (job.JobType == JobType.Gather)
                EnsureMissingDesignatedGatherAuraTargets();
            return true;
        }

        private void EnsureMissingDesignatedGatherAuraTargets()
        {
            foreach (var source in GetUnlocked(JobType.Gather))
            {
                if (source == null) continue;
                var path = GetAdvancePath(source);
                if (path == JobAdvanceNodeId.None) continue;
                var mods = JobAdvanceGatherMods.From(source, path);
                bool needs =
                    mods.DesignatedGatherEfficiencyPerWorker > 0f
                    || mods.DesignatedPairFlavorYieldBonus > 0f
                    || mods.DesignatedPairAllYieldBonus > 0f;
                if (!needs) continue;
                EnsureDesignatedGatherAuraTarget(source);
            }
        }

        private bool LockInternal(JobItem job)
        {
            if (job == null) return false;
            bool removed = _unlocked.Remove(job);
            _advancePaths.Remove(job);
            _designatedGatherAuraTargets.Remove(job);
            // 锁岗时清理挂在该岗上的残留状态，避免同 JobItem 重新解锁时旧修饰「复活」。
            _pendingGatherEfficiencyPenalty.Remove(job);
            _eventMods.Remove(job);

            // Drop aura bindings that pointed at a now-locked job.
            if (removed)
            {
                var clear = new List<JobItem>();
                foreach (var pair in _designatedGatherAuraTargets)
                {
                    if (ReferenceEquals(pair.Value, job))
                        clear.Add(pair.Key);
                }

                for (int i = 0; i < clear.Count; i++)
                    _designatedGatherAuraTargets.Remove(clear[i]);
            }

            return removed;
        }

        private static void Shuffle(List<JobItem> list, System.Random rng)
        {
            if (list == null || list.Count <= 1) return;
            rng ??= new System.Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
