using System.Collections.Generic;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Runtime elf pool: total count, free count, and per-job assignments.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class ElfManager : MonoBehaviour
    {
        public const string ResourcesConfigPath = "GameConfig";

        [SerializeField] private GameConfig config;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private int _totalCount;
        private readonly Dictionary<JobItem, int> _assignments = new Dictionary<JobItem, int>();

        public static ElfManager Instance { get; private set; }

        public GameConfig Config => config;

        /// <summary>小精灵总数。</summary>
        public int TotalCount => _totalCount;

        /// <summary>已分配到岗位的小精灵数量。</summary>
        public int AssignedCount
        {
            get
            {
                int sum = 0;
                foreach (var pair in _assignments)
                    sum += pair.Value;
                return sum;
            }
        }

        /// <summary>尚未分配的空闲小精灵数量。</summary>
        public int FreeCount => Mathf.Max(0, _totalCount - AssignedCount);

        public static void Initialize(GameConfig gameConfig)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(ElfManager));
                Instance = go.AddComponent<ElfManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            Instance.config = gameConfig;
            Instance.ResetFromConfig();
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

            if (config == null)
                config = Resources.Load<GameConfig>(ResourcesConfigPath);

            ResetFromConfig();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ResetFromConfig()
        {
            _assignments.Clear();
            _totalCount = config != null ? Mathf.Max(0, config.StartingElfCount) : 0;
        }

        public void SetTotalCount(int value)
        {
            _totalCount = Mathf.Max(0, value);
            ClampAssignmentsToCapacity();
        }

        public void AddElves(int amount)
        {
            if (amount == 0) return;
            _totalCount = Mathf.Max(0, _totalCount + amount);
            if (amount < 0)
                ClampAssignmentsToCapacity();
        }

        public int GetAssigned(JobItem job)
        {
            if (job == null) return 0;
            return _assignments.TryGetValue(job, out var count) ? count : 0;
        }

        public int GetJobCapacity(JobItem job)
        {
            if (job == null) return 0;
            return job.HasWorkerLimit ? job.MaxWorkers : int.MaxValue;
        }

        public int GetRemainingCapacity(JobItem job)
        {
            if (job == null) return 0;
            int capacity = GetJobCapacity(job);
            if (capacity == int.MaxValue)
            {
                // Cook stations are mutually exclusive: switching frees other cook workers.
                int free = FreeCount;
                if (job.JobType == JobType.Cook)
                    free += CountAssignedToOtherCooks(job);
                return free;
            }

            return Mathf.Max(0, capacity - GetAssigned(job));
        }

        /// <summary>Currently selected cook job, or null if none.</summary>
        public JobItem GetActiveCookJob()
        {
            foreach (var pair in _assignments)
            {
                if (pair.Key != null && pair.Key.JobType == JobType.Cook && pair.Value > 0)
                    return pair.Key;
            }

            return null;
        }

        public bool TryAssign(JobItem job, int amount = 1)
        {
            if (job == null || amount <= 0) return false;

            // Design: only one cook method (小火 / 中火 / 大火) at a time.
            // Switching transfers all workers from the previous cook station.
            if (job.JobType == JobType.Cook)
            {
                int fromOther = CountAssignedToOtherCooks(job);
                if (fromOther > 0)
                {
                    ClearOtherCookAssignments(job);
                    _assignments[job] = GetAssigned(job) + fromOther;
                    return true;
                }
            }

            if (amount > FreeCount) return false;

            int capacity = GetJobCapacity(job);
            if (capacity != int.MaxValue && GetAssigned(job) + amount > capacity)
                return false;

            _assignments[job] = GetAssigned(job) + amount;
            return true;
        }

        public bool TryUnassign(JobItem job, int amount = 1)
        {
            if (job == null || amount <= 0) return false;

            int current = GetAssigned(job);
            if (current <= 0) return false;

            int remove = Mathf.Min(amount, current);
            int next = current - remove;
            if (next <= 0)
                _assignments.Remove(job);
            else
                _assignments[job] = next;

            return true;
        }

        public void ClearAssignments()
        {
            _assignments.Clear();
        }

        public IReadOnlyDictionary<JobItem, int> GetAssignments()
        {
            return _assignments;
        }

        private void ClampAssignmentsToCapacity()
        {
            if (_assignments.Count == 0) return;

            var keys = new List<JobItem>(_assignments.Keys);
            int over = AssignedCount - _totalCount;
            if (over <= 0)
            {
                foreach (var job in keys)
                {
                    int capacity = GetJobCapacity(job);
                    if (capacity == int.MaxValue) continue;
                    int assigned = _assignments[job];
                    if (assigned > capacity)
                        _assignments[job] = capacity;
                }
                return;
            }

            // Trim overflow from the end of the assignment list when total shrinks.
            for (int i = keys.Count - 1; i >= 0 && over > 0; i--)
            {
                var job = keys[i];
                int assigned = _assignments[job];
                int cut = Mathf.Min(assigned, over);
                int next = assigned - cut;
                over -= cut;
                if (next <= 0)
                    _assignments.Remove(job);
                else
                    _assignments[job] = next;
            }
        }

        private int CountAssignedToOtherCooks(JobItem keep)
        {
            int sum = 0;
            foreach (var pair in _assignments)
            {
                if (pair.Key == null || pair.Key.JobType != JobType.Cook) continue;
                if (keep != null && ReferenceEquals(pair.Key, keep)) continue;
                sum += pair.Value;
            }
            return sum;
        }

        private void ClearOtherCookAssignments(JobItem keep)
        {
            if (_assignments.Count == 0) return;

            var keys = new List<JobItem>(_assignments.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var job = keys[i];
                if (job == null || job.JobType != JobType.Cook) continue;
                if (keep != null && ReferenceEquals(job, keep)) continue;
                _assignments.Remove(job);
            }
        }
    }
}
