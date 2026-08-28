using System;
using System.Collections.Generic;
using Soup.Game;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Employees
{
    /// <summary>
    /// Runtime employee catalog, ownership, and per-job assignments.
    /// </summary>
    [DefaultExecutionOrder(-92)]
    public class EmployeeManager : MonoBehaviour
    {
        public const string ResourcesDatabasePath = "EmployeeDatabase";
        public const string ResourcesConfigPath = "GameConfig";
        public const string ElfId = "elf";
        public const string MushroomPersonId = "mushroom_person";
        public const string GhostId = "ghost";

        [SerializeField] private EmployeeDatabase database;
        [SerializeField] private GameConfig config;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly Dictionary<string, int> _owned = new Dictionary<string, int>();
        /// <summary>jobId → (employeeId → count). Locked types are mirrored here too.</summary>
        private readonly Dictionary<string, Dictionary<string, int>> _assignments =
            new Dictionary<string, Dictionary<string, int>>();

        public static EmployeeManager Instance { get; private set; }

        public EmployeeDatabase Database => database;
        public GameConfig Config => config;

        public IReadOnlyList<EmployeeItem> All =>
            database != null ? database.Employees : Array.Empty<EmployeeItem>();

        public EmployeeItem ElfType => GetById(ElfId);
        public EmployeeItem MushroomPersonType => GetById(MushroomPersonId);
        public EmployeeItem GhostType => GetById(GhostId);

        public event Action Changed;

        public static void Initialize(EmployeeDatabase db, GameConfig gameConfig = null)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(EmployeeManager));
                Instance = go.AddComponent<EmployeeManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            Instance.database = db;
            if (gameConfig != null)
                Instance.config = gameConfig;
            Instance.database?.RebuildIndex();
            Instance.ResetFromConfig();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var db = Resources.Load<EmployeeDatabase>(ResourcesDatabasePath);
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
                database = Resources.Load<EmployeeDatabase>(ResourcesDatabasePath);
            if (config == null)
                config = Resources.Load<GameConfig>(ResourcesConfigPath);

            database?.RebuildIndex();
            if (_owned.Count == 0)
                ResetFromConfig();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public EmployeeItem GetById(string id) =>
            database != null ? database.GetById(id) : null;

        public void ResetFromConfig()
        {
            _owned.Clear();
            _assignments.Clear();
            int starting = config != null ? Mathf.Max(0, config.StartingElfCount) : 0;
            if (starting > 0)
                _owned[ElfId] = starting;
            RaiseChanged();
        }

        public void ResetRun() => ResetFromConfig();

        public int GetOwned(EmployeeItem type) =>
            type != null ? GetOwned(type.Id) : 0;

        public int GetOwned(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return 0;
            return _owned.TryGetValue(typeId, out var n) ? n : 0;
        }

        public int GetAssigned(EmployeeItem type, JobItem job)
        {
            if (type == null || job == null || string.IsNullOrEmpty(job.Id)) return 0;
            SyncLockedAssignments();
            if (!_assignments.TryGetValue(job.Id, out var map)) return 0;
            return map.TryGetValue(type.Id, out var n) ? n : 0;
        }

        public int GetAssignedTotal(EmployeeItem type)
        {
            if (type == null) return 0;
            SyncLockedAssignments();
            int sum = 0;
            foreach (var pair in _assignments)
            {
                if (pair.Value != null && pair.Value.TryGetValue(type.Id, out var n))
                    sum += n;
            }

            return sum;
        }

        public int GetFree(EmployeeItem type)
        {
            if (type == null) return 0;
            if (type.HasLockedJob)
                return 0;
            return Mathf.Max(0, GetOwned(type) - GetAssignedTotal(type));
        }

        /// <summary>Workers that count against job capacity on this station.</summary>
        public int GetOccupyingOnJob(JobItem job)
        {
            if (job == null) return 0;
            SyncLockedAssignments();
            if (!_assignments.TryGetValue(job.Id, out var map) || map == null) return 0;

            int sum = 0;
            foreach (var pair in map)
            {
                var type = GetById(pair.Key);
                if (type == null || !type.OccupiesJobSlot) continue;
                sum += pair.Value;
            }

            return sum;
        }

        /// <summary>Assigned headcount on a job (all types), ignoring work efficiency.</summary>
        public int GetAssignedCountOnJob(JobItem job)
        {
            if (job == null) return 0;
            SyncLockedAssignments();
            if (!_assignments.TryGetValue(job.Id, out var map) || map == null) return 0;

            int sum = 0;
            foreach (var pair in map)
            {
                if (pair.Value > 0)
                    sum += pair.Value;
            }

            return sum;
        }

        /// <summary>
        /// 篝火晚会等：所有已拥有员工都在烹饪岗（无空闲、无采集/处理分配）。
        /// </summary>
        public bool AreAllEmployeesOnCookJobs()
        {
            SyncLockedAssignments();
            var jobs = JobManager.Instance;
            if (jobs == null) return false;

            int totalOwned = 0;
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                var type = all[i];
                if (type == null) continue;
                int owned = GetOwned(type);
                if (owned <= 0) continue;
                totalOwned += owned;
                if (!type.HasLockedJob && GetFree(type) > 0)
                    return false;
            }

            if (totalOwned <= 0) return false;

            int onCook = 0;
            foreach (var jobPair in _assignments)
            {
                if (jobPair.Value == null || string.IsNullOrEmpty(jobPair.Key)) continue;
                var job = jobs.GetById(jobPair.Key);
                if (job == null) continue;

                foreach (var typePair in jobPair.Value)
                {
                    if (typePair.Value <= 0) continue;
                    if (job.JobType != JobType.Cook)
                        return false;
                    onCook += typePair.Value;
                }
            }

            return onCook >= totalOwned;
        }

        /// <summary>
        /// 纯劳动力：人数 × 员工工作效率（含员工类型遗物加成），不含全局遗物/快乐坨坨效率层。
        /// </summary>
        public float GetPureLaborOnJob(JobItem job)
        {
            if (job == null) return 0f;
            SyncLockedAssignments();
            if (!_assignments.TryGetValue(job.Id, out var map) || map == null) return 0f;

            float labor = 0f;
            foreach (var pair in map)
            {
                if (pair.Value <= 0) continue;
                var type = GetById(pair.Key);
                float eff = type != null ? type.WorkEfficiency : 1f;
                float typeMod = RelicManager.Instance != null && type != null
                    ? RelicManager.Instance.GetEmployeeLaborModifier(type.Id)
                    : 0f;
                labor += pair.Value * Mathf.Max(0f, eff + typeMod);
            }

            return labor;
        }

        /// <summary>Effective labor on a job (count × efficiency), used by turn resolution.</summary>
        public float GetLaborOnJob(JobItem job) => GetPureLaborOnJob(job);

        /// <summary>Labor contributed by a specific employee type on a job.</summary>
        public float GetLaborOnJob(JobItem job, EmployeeItem type)
        {
            if (job == null || type == null) return 0f;
            int count = GetAssigned(type, job);
            if (count <= 0) return 0f;
            float typeMod = RelicManager.Instance != null
                ? RelicManager.Instance.GetEmployeeLaborModifier(type.Id)
                : 0f;
            return count * Mathf.Max(0f, type.WorkEfficiency + typeMod);
        }

        /// <summary>
        /// Processed units eaten by workers whose <see cref="EmployeeItem.ConsumeOwnProcessedFraction"/> &gt; 0,
        /// proportional to their labor share of <paramref name="produced"/>.
        /// </summary>
        public int ComputeOwnProcessedConsumed(JobItem job, int produced)
        {
            if (job == null || produced <= 0) return 0;
            float totalLabor = GetLaborOnJob(job);
            if (totalLabor <= 0f) return 0;
            if (!_assignments.TryGetValue(job.Id, out var map) || map == null) return 0;

            // 快照迭代：GetLaborOnJob 内部会 SyncLockedAssignments → EnforceLockedFill，
            // 可能修改 _assignments 字典（例如锁定员工配到本岗时），迭代期间修改会抛异常。
            var snapshot = new List<KeyValuePair<string, int>>(map);
            float eatenFloat = 0f;
            foreach (var pair in snapshot)
            {
                if (pair.Value <= 0) continue;
                var type = GetById(pair.Key);
                if (type == null) continue;
                float fraction = type.ConsumeOwnProcessedFraction;
                if (fraction <= 0f) continue;

                float typeLabor = GetLaborOnJob(job, type);
                if (typeLabor <= 0f) continue;

                float ownShare = produced * (typeLabor / totalLabor);
                eatenFloat += ownShare * fraction;
            }

            // 按总份额一次性取整，避免逐类型向上取整导致小产出被全额吃掉
            // （例如产出 1 份、吃 10% 时应留 1 份，而不是吃掉 100%）。
            int eaten = Mathf.FloorToInt(eatenFloat);
            return Mathf.Clamp(eaten, 0, produced);
        }

        /// <summary>Jobs that currently have any assigned workers (any type).</summary>
        public Dictionary<JobItem, float> GetLaborByJob()
        {
            SyncLockedAssignments();
            var result = new Dictionary<JobItem, float>();
            var jobs = JobManager.Instance;
            if (jobs == null) return result;

            foreach (var jobPair in _assignments)
            {
                if (string.IsNullOrEmpty(jobPair.Key) || jobPair.Value == null) continue;
                var job = jobs.GetById(jobPair.Key);
                if (job == null) continue;
                float labor = GetLaborOnJob(job);
                if (labor > 0f)
                    result[job] = labor;
            }

            return result;
        }

        public int GetJobCapacity(JobItem job)
        {
            if (job == null) return 0;
            var progression = JobProgressionManager.Instance;
            if (progression != null)
            {
                int capacity = progression.GetEffectiveMaxWorkers(job);
                return capacity == int.MaxValue ? int.MaxValue : capacity;
            }

            return job.HasWorkerLimit ? job.MaxWorkers : int.MaxValue;
        }

        public int GetRemainingOccupyingCapacity(JobItem job)
        {
            if (job == null) return 0;
            int capacity = GetJobCapacity(job);
            if (capacity == int.MaxValue)
                return int.MaxValue;
            return Mathf.Max(0, capacity - GetOccupyingOnJob(job));
        }

        public void SetOwned(EmployeeItem type, int count)
        {
            if (type == null) return;
            SetOwned(type.Id, count);
        }

        public void SetOwned(string typeId, int count) =>
            SetOwned(typeId, count, notifyElfLoss: true);

        public void SetOwned(string typeId, int count, bool notifyElfLoss)
        {
            if (string.IsNullOrEmpty(typeId)) return;
            int previous = GetOwned(typeId);
            count = Mathf.Max(0, count);
            if (count == 0)
                _owned.Remove(typeId);
            else
                _owned[typeId] = count;

            if (notifyElfLoss && typeId == ElfId && count < previous)
                RelicManager.Instance?.NotifyElvesLost(previous - count);

            EnforceLockedFill(typeId);
            ClampAssignmentsForType(typeId);
            RaiseChanged();
        }

        /// <summary>
        /// 将小精灵升华为幽灵：精灵不足则跳过；不触发「损失小精灵」类遗物。
        /// </summary>
        public bool TryConvertElvesToGhosts(int elvesCost, int ghostsGain)
        {
            elvesCost = Mathf.Max(0, elvesCost);
            ghostsGain = Mathf.Max(0, ghostsGain);
            if (elvesCost <= 0 || ghostsGain <= 0) return false;
            if (GetOwned(ElfId) < elvesCost) return false;

            SetOwned(ElfId, GetOwned(ElfId) - elvesCost, notifyElfLoss: false);
            Add(GhostId, ghostsGain);
            return true;
        }

        public void Add(EmployeeItem type, int amount)
        {
            if (type == null || amount == 0) return;
            Add(type.Id, amount);
        }

        public void Add(string typeId, int amount)
        {
            if (string.IsNullOrEmpty(typeId) || amount == 0) return;
            SetOwned(typeId, GetOwned(typeId) + amount);
            if (amount > 0)
                TryGrantAdvanceIncentives(amount);
        }

        private static void TryGrantAdvanceIncentives(int employeesGained)
        {
            int per = JobAdvanceGatherMods.SumIncentivePerEmployeeGained();
            if (per <= 0 || employeesGained <= 0) return;

            var relics = RelicManager.Instance;
            if (relics == null) return;
            var incentive = relics.GetById(RelicManager.IncentiveId);
            if (incentive == null) return;

            int stacks = per * employeesGained;
            for (int i = 0; i < stacks; i++)
                relics.Acquire(incentive);
        }

        public bool TryAssign(EmployeeItem type, JobItem job, int amount = 1)
        {
            if (type == null || job == null || amount <= 0) return false;
            if (!type.CanPlayerAssign) return false;
            if (type.HasLockedJob && !type.IsLockedTo(job)) return false;
            if (!type.CanWorkJob(job)) return false;

            var progression = JobProgressionManager.Instance;
            if (progression != null && !progression.IsUnlocked(job))
                return false;

            // Cooking is exclusive: picking a cook station moves every player-assignable
            // worker from the other cook stations over, so no employee type is silently
            // dropped (previously only the selected type was transferred while the rest
            // were cleared).
            if (job.JobType == JobType.Cook)
            {
                var moved = CollectOtherCookAssignments(job);
                if (moved.Count > 0)
                {
                    ClearPlayerAssignmentsOnOtherCooks(job);
                    foreach (var kv in moved)
                    {
                        var t = GetById(kv.Key);
                        if (t == null || kv.Value <= 0) continue;
                        SetAssignedRaw(t, job, GetAssigned(t, job) + kv.Value);
                    }

                    // 本次请求的分配量照常尝试（受空闲数约束）。
                    int add = Mathf.Min(amount, Mathf.Max(0, GetFree(type)));
                    if (add > 0)
                        SetAssignedRaw(type, job, GetAssigned(type, job) + add);
                    RaiseChanged();
                    return true;
                }
            }

            if (GetFree(type) < amount) return false;

            if (type.OccupiesJobSlot)
            {
                int remain = GetRemainingOccupyingCapacity(job);
                if (remain != int.MaxValue && amount > remain)
                    return false;
            }

            if (job.JobType == JobType.Cook)
                ClearPlayerAssignmentsOnOtherCooks(job);

            SetAssignedRaw(type, job, GetAssigned(type, job) + amount);
            RaiseChanged();
            return true;
        }

        public bool TryUnassign(EmployeeItem type, JobItem job, int amount = 1)
        {
            if (type == null || job == null || amount <= 0) return false;
            if (!type.CanPlayerAssign) return false;
            if (type.HasLockedJob) return false;

            int current = GetAssigned(type, job);
            if (current <= 0) return false;

            int next = Mathf.Max(0, current - amount);
            SetAssignedRaw(type, job, next);
            RaiseChanged();
            return true;
        }

        /// <summary>移除此岗位上所有可手动分配的员工（锁定岗如蘑菇人不受影响）。</summary>
        public bool TryClearJobAssignments(JobItem job)
        {
            if (job == null || string.IsNullOrEmpty(job.Id)) return false;
            SyncLockedAssignments();
            if (!_assignments.TryGetValue(job.Id, out var map) || map == null || map.Count == 0)
                return false;

            bool changed = false;
            var typeIds = new List<string>(map.Keys);
            for (int i = 0; i < typeIds.Count; i++)
            {
                var type = GetById(typeIds[i]);
                if (type == null || type.HasLockedJob || !type.CanPlayerAssign)
                    continue;
                if (map[typeIds[i]] <= 0) continue;
                map.Remove(typeIds[i]);
                changed = true;
            }

            if (map.Count == 0)
                _assignments.Remove(job.Id);

            if (!changed) return false;
            RaiseChanged();
            return true;
        }

        public void ClearPlayerAssignments()
        {
            var jobIds = new List<string>(_assignments.Keys);
            for (int i = 0; i < jobIds.Count; i++)
            {
                var map = _assignments[jobIds[i]];
                if (map == null) continue;
                var typeIds = new List<string>(map.Keys);
                for (int t = 0; t < typeIds.Count; t++)
                {
                    var type = GetById(typeIds[t]);
                    if (type == null || type.HasLockedJob || !type.CanPlayerAssign)
                        continue;
                    map.Remove(typeIds[t]);
                }

                if (map.Count == 0)
                    _assignments.Remove(jobIds[i]);
            }

            SyncLockedAssignments();
            RaiseChanged();
        }

        public void ApplyState(
            IList<string> ownedTypeIds,
            IList<int> ownedCounts,
            IList<string> assignTypeIds,
            IList<string> assignJobIds,
            IList<int> assignCounts)
        {
            _owned.Clear();
            _assignments.Clear();

            if (ownedTypeIds != null && ownedCounts != null)
            {
                int n = Mathf.Min(ownedTypeIds.Count, ownedCounts.Count);
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(ownedTypeIds[i]) || ownedCounts[i] <= 0) continue;
                    _owned[ownedTypeIds[i]] = ownedCounts[i];
                }
            }

            if (assignTypeIds != null && assignJobIds != null && assignCounts != null)
            {
                int n = Mathf.Min(assignTypeIds.Count, Mathf.Min(assignJobIds.Count, assignCounts.Count));
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(assignTypeIds[i]) || string.IsNullOrEmpty(assignJobIds[i]))
                        continue;
                    if (assignCounts[i] <= 0) continue;
                    var type = GetById(assignTypeIds[i]);
                    if (type != null && type.HasLockedJob)
                        continue;
                    if (!_assignments.TryGetValue(assignJobIds[i], out var map) || map == null)
                    {
                        map = new Dictionary<string, int>();
                        _assignments[assignJobIds[i]] = map;
                    }

                    map[assignTypeIds[i]] = assignCounts[i];
                }
            }

            SyncLockedAssignments();
            ClampAllAssignments();
            RaiseChanged();
        }

        public void CaptureOwned(List<string> typeIds, List<int> counts)
        {
            if (typeIds == null || counts == null) return;
            foreach (var pair in _owned)
            {
                if (pair.Value <= 0 || string.IsNullOrEmpty(pair.Key)) continue;
                typeIds.Add(pair.Key);
                counts.Add(pair.Value);
            }
        }

        public void CaptureAssignments(List<string> typeIds, List<string> jobIds, List<int> counts)
        {
            SyncLockedAssignments();
            if (typeIds == null || jobIds == null || counts == null) return;
            foreach (var jobPair in _assignments)
            {
                if (jobPair.Value == null) continue;
                foreach (var typePair in jobPair.Value)
                {
                    var type = GetById(typePair.Key);
                    if (type != null && type.HasLockedJob)
                        continue;
                    if (typePair.Value <= 0) continue;
                    typeIds.Add(typePair.Key);
                    jobIds.Add(jobPair.Key);
                    counts.Add(typePair.Value);
                }
            }
        }

        private void SyncLockedAssignments()
        {
            if (database == null) return;
            var list = database.Employees;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var type = list[i];
                if (type == null || !type.HasLockedJob || type.LockedJob == null) continue;
                EnforceLockedFill(type.Id);
            }
        }

        private void EnforceLockedFill(string typeId)
        {
            var type = GetById(typeId);
            if (type == null || !type.HasLockedJob || type.LockedJob == null) return;

            // Remove this type from every job first.
            var jobIds = new List<string>(_assignments.Keys);
            for (int i = 0; i < jobIds.Count; i++)
            {
                if (_assignments.TryGetValue(jobIds[i], out var map) && map != null)
                {
                    map.Remove(typeId);
                    if (map.Count == 0)
                        _assignments.Remove(jobIds[i]);
                }
            }

            int owned = GetOwned(typeId);
            if (owned <= 0) return;

            string jobId = type.LockedJob.Id;
            if (string.IsNullOrEmpty(jobId)) return;
            if (!_assignments.TryGetValue(jobId, out var target) || target == null)
            {
                target = new Dictionary<string, int>();
                _assignments[jobId] = target;
            }

            target[typeId] = owned;
        }

        private void ClampAssignmentsForType(string typeId)
        {
            var type = GetById(typeId);
            if (type == null || type.HasLockedJob) return;

            // Drop assignments that violate job-type restriction (e.g. 吱吱 only Process).
            if (type.RestrictToJobType)
            {
                var jobs = JobManager.Instance;
                var jobIds = new List<string>(_assignments.Keys);
                for (int i = 0; i < jobIds.Count; i++)
                {
                    if (!_assignments.TryGetValue(jobIds[i], out var map) || map == null) continue;
                    if (!map.ContainsKey(typeId)) continue;
                    var job = jobs != null ? jobs.GetById(jobIds[i]) : null;
                    if (job != null && type.CanWorkJob(job)) continue;
                    map.Remove(typeId);
                    if (map.Count == 0)
                        _assignments.Remove(jobIds[i]);
                }
            }

            int owned = GetOwned(typeId);
            int assigned = GetAssignedTotal(type);
            int over = assigned - owned;
            if (over <= 0) return;

            var overJobIds = new List<string>(_assignments.Keys);
            for (int i = overJobIds.Count - 1; i >= 0 && over > 0; i--)
            {
                if (!_assignments.TryGetValue(overJobIds[i], out var map) || map == null) continue;
                if (!map.TryGetValue(typeId, out var n) || n <= 0) continue;
                int cut = Mathf.Min(n, over);
                int next = n - cut;
                over -= cut;
                if (next <= 0) map.Remove(typeId);
                else map[typeId] = next;
                if (map.Count == 0) _assignments.Remove(overJobIds[i]);
            }
        }

        public void ClampAssignmentsToCapacity() => ClampAllAssignments();

        private void ClampAllAssignments()
        {
            foreach (var key in new List<string>(_owned.Keys))
                ClampAssignmentsForType(key);

            // Clamp occupying workers to job capacity.
            var jobs = JobManager.Instance;
            if (jobs == null) return;
            foreach (var jobId in new List<string>(_assignments.Keys))
            {
                var job = jobs.GetById(jobId);
                if (job == null) continue;
                int capacity = GetJobCapacity(job);
                if (capacity == int.MaxValue) continue;
                int occupying = GetOccupyingOnJob(job);
                int over = occupying - capacity;
                if (over <= 0) continue;

                if (!_assignments.TryGetValue(jobId, out var map) || map == null) continue;
                var typeIds = new List<string>(map.Keys);
                for (int i = typeIds.Count - 1; i >= 0 && over > 0; i--)
                {
                    var type = GetById(typeIds[i]);
                    if (type == null || !type.OccupiesJobSlot || type.HasLockedJob) continue;
                    int n = map[typeIds[i]];
                    int cut = Mathf.Min(n, over);
                    int next = n - cut;
                    over -= cut;
                    if (next <= 0) map.Remove(typeIds[i]);
                    else map[typeIds[i]] = next;
                }
            }
        }

        private void SetAssignedRaw(EmployeeItem type, JobItem job, int count)
        {
            if (type == null || job == null || string.IsNullOrEmpty(job.Id)) return;
            if (!_assignments.TryGetValue(job.Id, out var map) || map == null)
            {
                map = new Dictionary<string, int>();
                _assignments[job.Id] = map;
            }

            if (count <= 0)
            {
                map.Remove(type.Id);
                if (map.Count == 0)
                    _assignments.Remove(job.Id);
            }
            else
            {
                map[type.Id] = count;
            }
        }

        private int CountAssignedToOtherCooks(EmployeeItem type, JobItem keep)
        {
            int sum = 0;
            var jobs = JobManager.Instance;
            if (jobs == null || type == null) return 0;
            foreach (var pair in _assignments)
            {
                var job = jobs.GetById(pair.Key);
                if (job == null || job.JobType != JobType.Cook) continue;
                if (keep != null && ReferenceEquals(job, keep)) continue;
                if (pair.Value != null && pair.Value.TryGetValue(type.Id, out var n))
                    sum += n;
            }

            return sum;
        }

        /// <summary>
        /// 收集其他烹饪岗上所有可手动分配的员工（typeId → count）。
        /// 烹饪互斥转移时用它，保证混合类型（精灵 + 幽灵等）切换火力不丢分配。
        /// </summary>
        private Dictionary<string, int> CollectOtherCookAssignments(JobItem keep)
        {
            var result = new Dictionary<string, int>();
            var jobs = JobManager.Instance;
            if (jobs == null) return result;
            foreach (var pair in _assignments)
            {
                var job = jobs.GetById(pair.Key);
                if (job == null || job.JobType != JobType.Cook) continue;
                if (keep != null && ReferenceEquals(job, keep)) continue;
                if (pair.Value == null) continue;

                foreach (var typePair in pair.Value)
                {
                    if (typePair.Value <= 0) continue;
                    var type = GetById(typePair.Key);
                    if (type == null || type.HasLockedJob || !type.CanPlayerAssign)
                        continue;
                    result.TryGetValue(typePair.Key, out int existing);
                    result[typePair.Key] = existing + typePair.Value;
                }
            }

            return result;
        }

        private void ClearPlayerAssignmentsOnOtherCooks(JobItem keep)
        {
            var jobs = JobManager.Instance;
            if (jobs == null) return;
            var jobIds = new List<string>(_assignments.Keys);
            for (int i = 0; i < jobIds.Count; i++)
            {
                var job = jobs.GetById(jobIds[i]);
                if (job == null || job.JobType != JobType.Cook) continue;
                if (keep != null && ReferenceEquals(job, keep)) continue;
                if (!_assignments.TryGetValue(jobIds[i], out var map) || map == null) continue;

                var typeIds = new List<string>(map.Keys);
                for (int t = 0; t < typeIds.Count; t++)
                {
                    var type = GetById(typeIds[t]);
                    if (type == null || type.HasLockedJob || !type.CanPlayerAssign)
                        continue;
                    map.Remove(typeIds[t]);
                }

                if (map.Count == 0)
                    _assignments.Remove(jobIds[i]);
            }
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
