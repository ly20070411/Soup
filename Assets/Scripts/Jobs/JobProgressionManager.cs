using System.Collections.Generic;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// Runtime unlock / upgrade state for jobs in the current run.
    /// </summary>
    [DefaultExecutionOrder(-95)]
    public class JobProgressionManager : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly HashSet<JobItem> _unlocked = new HashSet<JobItem>();
        private readonly Dictionary<JobItem, int> _upgradeLevels = new Dictionary<JobItem, int>();
        private readonly List<JobItem> _gatherOffer = new List<JobItem>();

        private bool _gatherStarterPicked;
        private bool _processStarterPicked;

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
        }

        private void Start()
        {
            // JobManager may initialize in the same frame; refresh defaults once more.
            BootstrapDefaults();
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
            if (Instance == this)
                Instance = null;
        }

        public void ResetRun()
        {
            _unlocked.Clear();
            _upgradeLevels.Clear();
            _gatherOffer.Clear();
            _gatherStarterPicked = false;
            _processStarterPicked = false;
            BootstrapDefaults();
        }

        public void ApplyState(
            IList<string> unlockedJobIds,
            IList<string> upgradeJobIds,
            IList<int> upgradeLevels,
            IList<string> gatherOfferIds,
            bool gatherStarterPicked,
            bool processStarterPicked)
        {
            _unlocked.Clear();
            _upgradeLevels.Clear();
            _gatherOffer.Clear();
            _gatherStarterPicked = gatherStarterPicked;
            _processStarterPicked = processStarterPicked;

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

            if (jobs != null && upgradeJobIds != null && upgradeLevels != null)
            {
                int n = Mathf.Min(upgradeJobIds.Count, upgradeLevels.Count);
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(upgradeJobIds[i]) || upgradeLevels[i] <= 0)
                        continue;
                    var job = jobs.GetById(upgradeJobIds[i]);
                    if (job == null) continue;
                    _upgradeLevels[job] = upgradeLevels[i];
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

            BootstrapDefaults();
        }

        public bool IsUnlocked(JobItem job) => job != null && _unlocked.Contains(job);

        public int GetUpgradeLevel(JobItem job)
        {
            if (job == null) return 0;
            return _upgradeLevels.TryGetValue(job, out var level) ? level : 0;
        }

        public int GetEffectiveMaxWorkers(JobItem job)
        {
            if (job == null) return 0;
            if (!job.HasWorkerLimit) return int.MaxValue;
            return job.GetEffectiveMaxWorkers(GetUpgradeLevel(job));
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
                list.Add(job);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return list;
        }

        public bool CanUnlockMore(JobType type)
        {
            if (type == JobType.Cook) return false;
            int max = JobProgressionRules.MaxStations(type);
            return CountUnlocked(type) < max && GetLocked(type).Count > 0;
        }

        /// <summary>采集已满且仍有未解锁岗位时，可换岗。</summary>
        public bool CanReplaceGather =>
            CountUnlocked(JobType.Gather) >= JobProgressionRules.GatherMaxStations
            && GetLocked(JobType.Gather).Count > 0
            && GetReplaceableGatherJobs().Count > 0;

        public bool IsPermanentGatherJob(JobItem job)
        {
            if (job == null || job.JobType != JobType.Gather) return false;
            return job.Id == JobProgressionRules.StartingGatherJobId;
        }

        /// <summary>可被换下的采集岗（蘑菇固定岗除外）。</summary>
        public List<JobItem> GetReplaceableGatherJobs()
        {
            var list = GetUnlocked(JobType.Gather);
            list.RemoveAll(IsPermanentGatherJob);
            return list;
        }

        public bool CanUpgrade(JobItem job)
        {
            if (job == null) return false;
            if (job.JobType != JobType.Cook && !IsUnlocked(job)) return false;

            int level = GetUpgradeLevel(job);
            int max = JobProgressionRules.MaxUpgradesPerJob(job.JobType);
            return level < max;
        }

        public bool TryUpgrade(JobItem job)
        {
            if (!CanUpgrade(job)) return false;

            int next = GetUpgradeLevel(job) + 1;
            _upgradeLevels[job] = next;
            if (job.JobType != JobType.Cook)
                UnlockInternal(job);
            return true;
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

        /// <summary>
        /// Gather advancement: unlock one of two offered locked jobs.
        /// </summary>
        public bool TryUnlockFromGatherOffer(JobItem job)
        {
            if (job == null || !_gatherOffer.Contains(job)) return false;
            if (!CanUnlockMore(JobType.Gather)) return false;

            bool ok = UnlockInternal(job);
            _gatherOffer.Clear();
            return ok;
        }

        /// <summary>
        /// Replace an unlocked gather job with one from the current offer when slots are full.
        /// Permanent starter (mushroom) cannot be replaced. Incoming job starts at Lv0.
        /// </summary>
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

        /// <summary>
        /// Process advancement: unlock any remaining process job (pick one).
        /// </summary>
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
            // Allow offers both when unlocking a free slot and when replacing at capacity.
            if (!CanUnlockMore(JobType.Gather) && !CanReplaceGather) return;

            var locked = GetLocked(JobType.Gather);
            if (locked.Count == 0) return;

            int offerCount = Mathf.Min(JobProgressionRules.GatherNewJobOfferCount, locked.Count);
            Shuffle(locked, rng);
            for (int i = 0; i < offerCount; i++)
                _gatherOffer.Add(locked[i]);
        }

        public string DescribeUpgradePreview(JobItem job)
        {
            if (job == null) return string.Empty;

            int level = GetUpgradeLevel(job);
            if (level >= JobProgressionRules.MaxUpgradesPerJob(job.JobType))
                return "已满级";

            var tier = job.GetUpgradeTier(level);
            if (tier == null)
            {
                job.EnsureUpgradeTierSize();
                tier = job.GetUpgradeTier(level);
            }

            return tier != null ? tier.ToSummary(level) : $"Lv{level + 1}";
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
            return _unlocked.Add(job);
        }

        private bool LockInternal(JobItem job)
        {
            if (job == null) return false;
            bool removed = _unlocked.Remove(job);
            _upgradeLevels.Remove(job);
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
