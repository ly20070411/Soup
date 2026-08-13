using System.Collections.Generic;
using Soup.Employees;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Compatibility facade over EmployeeManager for the default 小精灵 pool,
    /// plus occupying-slot helpers used by station UI / capacity checks.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class ElfManager : MonoBehaviour
    {
        public const string ResourcesConfigPath = "GameConfig";

        [SerializeField] private GameConfig config;
        [SerializeField] private bool dontDestroyOnLoad = true;

        public static ElfManager Instance { get; private set; }

        public GameConfig Config => config;

        private EmployeeManager Em => EmployeeManager.Instance;
        private EmployeeItem Elf => Em != null ? Em.ElfType : null;

        /// <summary>小精灵总数。</summary>
        public int TotalCount => Em != null ? Em.GetOwned(EmployeeManager.ElfId) : 0;

        /// <summary>已分配到岗位的小精灵数量。</summary>
        public int AssignedCount => Em != null && Elf != null ? Em.GetAssignedTotal(Elf) : 0;

        /// <summary>尚未分配的空闲小精灵数量。</summary>
        public int FreeCount => Em != null && Elf != null ? Em.GetFree(Elf) : 0;

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
            if (EmployeeManager.Instance != null)
                EmployeeManager.Instance.ResetFromConfig();
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
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ResetFromConfig()
        {
            Em?.ResetFromConfig();
        }

        public void SetTotalCount(int value)
        {
            Em?.SetOwned(EmployeeManager.ElfId, value);
        }

        public void AddElves(int amount)
        {
            Em?.Add(EmployeeManager.ElfId, amount);
        }

        /// <summary>Occupying workers on this job (elves + mushroom people, etc.).</summary>
        public int GetAssigned(JobItem job)
        {
            return Em != null ? Em.GetOccupyingOnJob(job) : 0;
        }

        public int GetJobCapacity(JobItem job)
        {
            return Em != null ? Em.GetJobCapacity(job) : (job != null && job.HasWorkerLimit ? job.MaxWorkers : int.MaxValue);
        }

        public int GetRemainingCapacity(JobItem job)
        {
            if (job == null) return 0;
            if (Em == null) return 0;

            int capacity = GetJobCapacity(job);
            if (capacity == int.MaxValue)
            {
                int free = FreeCount;
                if (job.JobType == JobType.Cook)
                    free += CountAssignedElvesToOtherCooks(job);
                return free;
            }

            return Em.GetRemainingOccupyingCapacity(job);
        }

        public JobItem GetActiveCookJob()
        {
            if (Em == null) return null;
            foreach (var pair in Em.GetLaborByJob())
            {
                if (pair.Key != null && pair.Key.JobType == JobType.Cook && pair.Value > 0f)
                    return pair.Key;
            }

            return null;
        }

        public bool TryAssign(JobItem job, int amount = 1)
        {
            if (Em == null || Elf == null) return false;
            return Em.TryAssign(Elf, job, amount);
        }

        public bool TryUnassign(JobItem job, int amount = 1)
        {
            if (Em == null || Elf == null) return false;
            return Em.TryUnassign(Elf, job, amount);
        }

        public void ClearAssignments()
        {
            Em?.ClearPlayerAssignments();
        }

        /// <summary>
        /// Occupying headcount by job (for UI). Prefer <see cref="EmployeeManager.GetLaborByJob"/> for production.
        /// </summary>
        public IReadOnlyDictionary<JobItem, int> GetAssignments()
        {
            var map = new Dictionary<JobItem, int>();
            if (Em == null) return map;
            foreach (var pair in Em.GetLaborByJob())
            {
                int occupying = Em.GetOccupyingOnJob(pair.Key);
                if (occupying > 0)
                    map[pair.Key] = occupying;
                else if (pair.Value > 0f)
                    map[pair.Key] = GameMath.CeilToInt(pair.Value);
            }

            return map;
        }

        public void ApplyState(int totalElves, IList<string> jobIds, IList<int> counts)
        {
            if (Em == null)
                return;

            // Preserve non-elf ownership; rebuild elf assignments from save.
            var ownedTypes = new List<string>();
            var ownedCounts = new List<int>();
            Em.CaptureOwned(ownedTypes, ownedCounts);

            // Force elf count from save.
            bool foundElf = false;
            for (int i = 0; i < ownedTypes.Count; i++)
            {
                if (ownedTypes[i] == EmployeeManager.ElfId)
                {
                    ownedCounts[i] = Mathf.Max(0, totalElves);
                    foundElf = true;
                    break;
                }
            }

            if (!foundElf && totalElves > 0)
            {
                ownedTypes.Add(EmployeeManager.ElfId);
                ownedCounts.Add(totalElves);
            }

            var assignTypes = new List<string>();
            var assignJobs = new List<string>();
            var assignCounts = new List<int>();

            // Keep non-elf player assignments (e.g. ghosts).
            var keepTypes = new List<string>();
            var keepJobs = new List<string>();
            var keepCounts = new List<int>();
            Em.CaptureAssignments(keepTypes, keepJobs, keepCounts);
            for (int i = 0; i < keepTypes.Count; i++)
            {
                if (keepTypes[i] == EmployeeManager.ElfId) continue;
                assignTypes.Add(keepTypes[i]);
                assignJobs.Add(keepJobs[i]);
                assignCounts.Add(keepCounts[i]);
            }

            if (jobIds != null && counts != null)
            {
                int n = Mathf.Min(jobIds.Count, counts.Count);
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(jobIds[i]) || counts[i] <= 0) continue;
                    assignTypes.Add(EmployeeManager.ElfId);
                    assignJobs.Add(jobIds[i]);
                    assignCounts.Add(counts[i]);
                }
            }

            Em.ApplyState(ownedTypes, ownedCounts, assignTypes, assignJobs, assignCounts);
        }

        private int CountAssignedElvesToOtherCooks(JobItem keep)
        {
            if (Em == null || Elf == null) return 0;
            int sum = 0;
            foreach (var pair in Em.GetLaborByJob())
            {
                if (pair.Key == null || pair.Key.JobType != JobType.Cook) continue;
                if (keep != null && ReferenceEquals(pair.Key, keep)) continue;
                sum += Em.GetAssigned(Elf, pair.Key);
            }

            return sum;
        }
    }
}
