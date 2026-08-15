using System.Collections.Generic;
using Soup.Game;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Employees
{
    /// <summary>
    /// Owns employee counts per type and their job assignments.
    /// Labor = assigned × type efficiency × relic modifiers; occupying = slot usage.
    /// Locked types (mushroom persons) always occupy their locked job.
    /// </summary>
    [DefaultExecutionOrder(-95)]
    public class EmployeeManager : MonoBehaviour
    {
        public const string ElfId = "elf";
        public const string MushroomPersonId = "mushroom_person";
        public const string GhostId = "ghost";
        public const string OtherworldHeroId = "otherworld_hero";
        public const string ZhizhiId = "zhizhi";
        public const string ResourcesDatabasePath = "EmployeeDatabase";
        public const string ResourcesConfigPath = "GameConfig";

        [SerializeField] private EmployeeDatabase database;
        [SerializeField] private bool dontDestroyOnLoad = true;

        public static EmployeeManager Instance { get; private set; }

        // typeId -> owned count
        private readonly Dictionary<string, int> _owned = new Dictionary<string, int>();
        // typeId -> (jobId -> assigned count); locked types are derived, not stored
        private readonly Dictionary<string, Dictionary<string, int>> _assigned =
            new Dictionary<string, Dictionary<string, int>>();

        private readonly List<EmployeeItem> _catalog = new List<EmployeeItem>();

        public IReadOnlyList<EmployeeItem> All => _catalog;

        public EmployeeItem ElfType => GetById(ElfId);
        public EmployeeItem GhostType => GetById(GhostId);
        public EmployeeItem MushroomPersonType => GetById(MushroomPersonId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            Initialize();
        }

        public static void Initialize(EmployeeDatabase db = null)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(EmployeeManager));
                Instance = go.AddComponent<EmployeeManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            if (db != null)
                Instance.database = db;
            Instance.RebuildCatalog();
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
                database = Resources.Load<EmployeeDatabase>(ResourcesDatabasePath);

            RebuildCatalog();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void RebuildCatalog()
        {
            _catalog.Clear();

            if (database != null)
            {
                database.RebuildIndex();
                var list = database.Employees;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null)
                        _catalog.Add(list[i]);
                }
            }

            if (_catalog.Count == 0)
                BuildDefaultCatalog();
        }

        /// <summary>
        /// Fallback catalog so the game runs without a persisted EmployeeDatabase asset.
        /// 数据来自设计文档「员工一览」；头像从 Resources/UI/Generated/employee_* 加载
        /// （由 ArtIconLinker 部署，缺失时无图标，不影响逻辑）。
        /// </summary>
        private void BuildDefaultCatalog()
        {
            _catalog.Add(CreateDefault(ElfId, "小精灵", 1f, true, true, string.Empty, null, 0f,
                "勤劳的基础员工，可以分配到任意岗位。"));
            _catalog.Add(CreateDefault(MushroomPersonId, "蘑菇人", 1.5f, true, false, "mushroom", null, 0f,
                "占用蘑菇岗位人口，一直生产蘑菇，玩家无法变更岗位。"));
            _catalog.Add(CreateDefault(GhostId, "幽灵", 0.8f, false, true, string.Empty, null, 0f,
                "不占用工作岗位容量。"));
            _catalog.Add(CreateDefault(OtherworldHeroId, "异世界勇者", 3f, true, true, string.Empty, null, 0f,
                "来自异世界的勇者，工作效率极高。"));
            _catalog.Add(CreateDefault(ZhizhiId, "吱吱", 2.5f, true, true, string.Empty, JobType.Process, 0.1f,
                "只能用于处理工作，会吃掉自身产出处理食材的 10%。"));

            for (int i = 0; i < _catalog.Count; i++)
                ApplyGeneratedIcon(_catalog[i]);
        }

        /// <summary>运行时头像：Resources/UI/Generated/employee_{id}（Sprite 导入）。</summary>
        private static void ApplyGeneratedIcon(EmployeeItem item)
        {
            if (item == null || item.Icon != null) return;
            var sprite = Resources.Load<Sprite>($"UI/Generated/employee_{item.Id}");
            if (sprite != null)
                item.SetIcon(sprite);
        }

        private static EmployeeItem CreateDefault(
            string id,
            string displayName,
            float efficiency,
            bool occupiesSlot,
            bool playerAssignable,
            string lockedJob,
            JobType? allowedType,
            float eatShare,
            string description)
        {
            var item = ScriptableObject.CreateInstance<EmployeeItem>();
            item.name = id;
            item.hideFlags = HideFlags.HideAndDontSave;
            item.SetIdentity(id, displayName);
            item.SetDescription(description);
            item.SetLaborEfficiency(efficiency);
            item.SetAssignmentRules(occupiesSlot, playerAssignable, lockedJob, allowedType);
            item.SetEatProcessedShare(eatShare);
            return item;
        }

        public EmployeeItem GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < _catalog.Count; i++)
            {
                if (_catalog[i] != null && _catalog[i].Id == id)
                    return _catalog[i];
            }

            return null;
        }

        public bool TryGet(string id, out EmployeeItem item)
        {
            item = GetById(id);
            return item != null;
        }

        // ---------------------------------------------------------------- owned

        public int GetOwned(string typeId)
        {
            return typeId != null && _owned.TryGetValue(typeId, out int count) ? count : 0;
        }

        public int GetOwned(EmployeeItem type) => type != null ? GetOwned(type.Id) : 0;

        public void SetOwned(string typeId, int value)
        {
            if (string.IsNullOrWhiteSpace(typeId)) return;
            _owned[typeId] = Mathf.Max(0, value);
        }

        public void Add(string typeId, int amount)
        {
            if (string.IsNullOrWhiteSpace(typeId)) return;
            SetOwned(typeId, GetOwned(typeId) + amount);
        }

        // ------------------------------------------------------------ assigned

        /// <summary>Assigned count of a type on a job. Locked types report full ownership on their job.</summary>
        public int GetAssigned(EmployeeItem type, JobItem job)
        {
            if (type == null || job == null) return 0;

            if (type.HasLockedJob)
                return type.LockedJobId == job.Id ? GetOwned(type) : 0;

            if (!_assigned.TryGetValue(type.Id, out var byJob)) return 0;
            return byJob.TryGetValue(job.Id, out int count) ? count : 0;
        }

        public int GetAssignedTotal(EmployeeItem type)
        {
            if (type == null) return 0;

            if (type.HasLockedJob)
                return GetOwned(type);

            if (!_assigned.TryGetValue(type.Id, out var byJob)) return 0;
            int sum = 0;
            foreach (var pair in byJob)
                sum += pair.Value;
            return sum;
        }

        public int GetFree(EmployeeItem type)
        {
            if (type == null) return 0;
            return Mathf.Max(0, GetOwned(type) - GetAssignedTotal(type));
        }

        public bool TryAssign(EmployeeItem type, JobItem job, int amount = 1)
        {
            if (type == null || job == null || amount <= 0) return false;
            if (type.HasLockedJob || !type.CanPlayerAssign) return false;
            if (type.AllowedJobType != null && type.AllowedJobType != job.JobType) return false;
            if (GetFree(type) < amount) return false;

            if (type.OccupiesJobSlot && GetRemainingOccupyingCapacity(job) < amount)
                return false;

            if (!_assigned.TryGetValue(type.Id, out var byJob))
            {
                byJob = new Dictionary<string, int>();
                _assigned[type.Id] = byJob;
            }

            byJob[job.Id] = (byJob.TryGetValue(job.Id, out int current) ? current : 0) + amount;
            return true;
        }

        public bool TryUnassign(EmployeeItem type, JobItem job, int amount = 1)
        {
            if (type == null || job == null || amount <= 0) return false;
            if (type.HasLockedJob) return false;

            if (!_assigned.TryGetValue(type.Id, out var byJob)) return false;
            if (!byJob.TryGetValue(job.Id, out int current) || current <= 0) return false;

            int take = Mathf.Min(current, amount);
            int left = current - take;
            if (left > 0)
                byJob[job.Id] = left;
            else
                byJob.Remove(job.Id);

            return true;
        }

        public void ClearPlayerAssignments()
        {
            _assigned.Clear();
        }

        /// <summary>Unassign every player-assignable type from one job (岗位被事件移除时).</summary>
        public void ClearJobAssignments(JobItem job)
        {
            if (job == null) return;
            foreach (var byJob in _assigned.Values)
                byJob.Remove(job.Id);
        }

        // -------------------------------------------------------------- capacity

        /// <summary>Workers occupying slots on this job (elves + mushroom people; ghosts excluded).</summary>
        public int GetOccupyingOnJob(JobItem job)
        {
            if (job == null) return 0;
            int sum = 0;
            for (int i = 0; i < _catalog.Count; i++)
            {
                var type = _catalog[i];
                if (type == null || !type.OccupiesJobSlot) continue;
                sum += GetAssigned(type, job);
            }

            return sum;
        }

        public int GetJobCapacity(JobItem job)
        {
            if (job == null) return 0;

            var progression = JobProgressionManager.Instance;
            int capacity = progression != null
                ? progression.GetEffectiveMaxWorkers(job)
                : (job.HasWorkerLimit ? job.MaxWorkers : int.MaxValue);

            // 进阶专属事件可调整岗位容量（如 孢子感染：蘑菇岗采集上限减五）。
            if (capacity != int.MaxValue)
            {
                int bonus = JobModifierManager.Instance != null
                    ? JobModifierManager.Instance.GetCapacityBonus(job)
                    : 0;
                capacity = Mathf.Max(0, capacity + bonus);
            }

            return capacity;
        }

        public int GetRemainingOccupyingCapacity(JobItem job)
        {
            if (job == null) return 0;
            int capacity = GetJobCapacity(job);
            if (capacity == int.MaxValue) return int.MaxValue;
            return Mathf.Max(0, capacity - GetOccupyingOnJob(job));
        }

        // ----------------------------------------------------------------- labor

        /// <summary>Labor contributed to a job, applying relic modifiers.</summary>
        public float GetLaborOnJob(JobItem job)
        {
            if (job == null) return 0f;
            float labor = 0f;
            for (int i = 0; i < _catalog.Count; i++)
            {
                var type = _catalog[i];
                if (type == null) continue;
                int count = GetAssigned(type, job);
                if (count <= 0) continue;
                labor += count * GetEffectiveEfficiency(type);
            }

            return labor;
        }

        public Dictionary<JobItem, float> GetLaborByJob()
        {
            var map = new Dictionary<JobItem, float>();
            var jobs = JobManager.Instance;
            if (jobs == null) return map;

            var all = jobs.All;
            for (int i = 0; i < all.Count; i++)
            {
                var job = all[i];
                if (job == null) continue;
                float labor = GetLaborOnJob(job);
                if (labor > 0f)
                    map[job] = labor;
            }

            return map;
        }

        private float GetEffectiveEfficiency(EmployeeItem type)
        {
            float efficiency = type.LaborEfficiency;
            var relics = RelicManager.Instance;
            if (relics != null)
            {
                efficiency *= 1f + relics.GetGlobalLaborModifier();
                efficiency *= 1f + relics.GetEmployeeLaborModifier(type.Id);
            }

            return Mathf.Max(0f, efficiency);
        }

        // ----------------------------------------------------------------- upkeep

        /// <summary>
        /// Processed units eaten by workers assigned to this process job:
        /// each type with EatProcessedShare consumes that share of the job's output (吱吱 10%).
        /// </summary>
        public int ComputeOwnProcessedConsumed(JobItem job, int produced)
        {
            if (job == null || produced <= 0) return 0;
            int eaten = 0;
            for (int i = 0; i < _catalog.Count; i++)
            {
                var type = _catalog[i];
                if (type == null || type.EatProcessedShare <= 0f) continue;
                if (GetAssigned(type, job) <= 0) continue;
                eaten += GameMath.CeilToInt(produced * type.EatProcessedShare);
            }

            return Mathf.Min(eaten, produced);
        }

        // ------------------------------------------------------------- lifecycle

        public void ResetFromConfig()
        {
            _owned.Clear();
            ClearPlayerAssignments();

            int starting = 0;
            var config = Resources.Load<GameConfig>(ResourcesConfigPath);
            if (config != null)
                starting = config.StartingElfCount;

            if (starting > 0)
                _owned[ElfId] = starting;
        }

        public void ResetRun()
        {
            ResetFromConfig();
        }

        // ------------------------------------------------------------------ save

        public void CaptureOwned(List<string> types, List<int> counts)
        {
            types.Clear();
            counts.Clear();
            foreach (var pair in _owned)
            {
                if (pair.Value <= 0) continue;
                types.Add(pair.Key);
                counts.Add(pair.Value);
            }
        }

        public void CaptureAssignments(
            List<string> types,
            List<string> jobIds,
            List<int> counts)
        {
            types.Clear();
            jobIds.Clear();
            counts.Clear();

            var jobs = JobManager.Instance;
            if (jobs == null) return;

            var all = jobs.All;
            for (int j = 0; j < all.Count; j++)
            {
                var job = all[j];
                if (job == null) continue;

                for (int i = 0; i < _catalog.Count; i++)
                {
                    var type = _catalog[i];
                    if (type == null || type.HasLockedJob) continue;
                    int count = GetAssigned(type, job);
                    if (count <= 0) continue;

                    types.Add(type.Id);
                    jobIds.Add(job.Id);
                    counts.Add(count);
                }
            }
        }

        public void ApplyState(
            IList<string> ownedTypes,
            IList<int> ownedCounts,
            IList<string> assignTypes,
            IList<string> assignJobIds,
            IList<int> assignCounts)
        {
            _owned.Clear();
            ClearPlayerAssignments();

            if (ownedTypes != null && ownedCounts != null)
            {
                int n = Mathf.Min(ownedTypes.Count, ownedCounts.Count);
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(ownedTypes[i]) || ownedCounts[i] <= 0) continue;
                    _owned[ownedTypes[i]] = ownedCounts[i];
                }
            }

            if (assignTypes != null && assignJobIds != null && assignCounts != null)
            {
                var jobs = JobManager.Instance;
                int n = Mathf.Min(Mathf.Min(assignTypes.Count, assignJobIds.Count), assignCounts.Count);
                for (int i = 0; i < n; i++)
                {
                    var type = GetById(assignTypes[i]);
                    if (type == null || type.HasLockedJob || jobs == null) continue;
                    var job = jobs.GetById(assignJobIds[i]);
                    if (job == null) continue;

                    if (!_assigned.TryGetValue(type.Id, out var byJob))
                    {
                        byJob = new Dictionary<string, int>();
                        _assigned[type.Id] = byJob;
                    }

                    byJob[job.Id] = (byJob.TryGetValue(job.Id, out int current) ? current : 0)
                                    + Mathf.Max(0, assignCounts[i]);
                }
            }
        }
    }
}
