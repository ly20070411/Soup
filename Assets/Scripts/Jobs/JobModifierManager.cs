using System.Collections.Generic;
using Soup.Employees;
using Soup.Game;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// Runtime per-job modifiers granted by 进阶专属事件 options:
    /// yield multipliers, capacity bonuses, extra per-unit flavors, and job disabling.
    /// </summary>
    [DefaultExecutionOrder(-65)]
    public class JobModifierManager : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly Dictionary<string, float> _yieldMultipliers = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _capacityBonuses = new Dictionary<string, int>();
        private readonly Dictionary<string, FlavorType> _bonusFlavors = new Dictionary<string, FlavorType>();
        private readonly Dictionary<string, int> _bonusFlavorPerUnit = new Dictionary<string, int>();
        private readonly HashSet<string> _disabledJobs = new HashSet<string>();

        public static JobModifierManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(JobModifierManager));
            Instance = go.AddComponent<JobModifierManager>();
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
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
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public float GetYieldMultiplier(JobItem job)
        {
            return job != null
                   && _yieldMultipliers.TryGetValue(job.Id, out float mult)
                   && mult > 0f
                ? mult
                : 1f;
        }

        /// <summary>Extra capacity from event effects (can be negative, e.g. 孢子感染 -5).</summary>
        public int GetCapacityBonus(JobItem job)
        {
            return job != null && _capacityBonuses.TryGetValue(job.Id, out int bonus) ? bonus : 0;
        }

        /// <summary>True when an event permanently removed this station (神说，要有魔法叶).</summary>
        public bool IsDisabled(JobItem job) => job != null && _disabledJobs.Contains(job.Id);

        public bool TryGetBonusFlavor(JobItem job, out FlavorType flavor, out int perUnit)
        {
            flavor = FlavorType.Spicy;
            perUnit = 0;
            if (job == null) return false;
            if (!_bonusFlavors.TryGetValue(job.Id, out flavor)) return false;
            return _bonusFlavorPerUnit.TryGetValue(job.Id, out perUnit) && perUnit > 0;
        }

        public void SetYieldMultiplier(JobItem job, float multiplier)
        {
            if (job == null) return;
            _yieldMultipliers[job.Id] = Mathf.Max(0f, multiplier);
        }

        public void AddCapacityBonus(JobItem job, int delta)
        {
            if (job == null || delta == 0) return;
            _capacityBonuses.TryGetValue(job.Id, out int current);
            _capacityBonuses[job.Id] = current + delta;
        }

        public void SetBonusFlavor(JobItem job, FlavorType flavor, int perUnit)
        {
            if (job == null || perUnit <= 0) return;
            _bonusFlavors[job.Id] = flavor;
            _bonusFlavorPerUnit[job.Id] = perUnit;
        }

        public void SetDisabled(JobItem job, bool disabled)
        {
            if (job == null) return;
            if (disabled)
            {
                _disabledJobs.Add(job.Id);
                // 永久失去岗位时，把上面干活的员工全部释放。
                EmployeeManager.Instance?.ClearJobAssignments(job);
            }
            else
            {
                _disabledJobs.Remove(job.Id);
            }
        }

        public void ResetRun()
        {
            _yieldMultipliers.Clear();
            _capacityBonuses.Clear();
            _bonusFlavors.Clear();
            _bonusFlavorPerUnit.Clear();
            _disabledJobs.Clear();
        }

        // ------------------------------------------------------------------ save

        public void CaptureState(
            System.Collections.Generic.List<string> yieldJobIds,
            System.Collections.Generic.List<float> yieldValues,
            System.Collections.Generic.List<string> capacityJobIds,
            System.Collections.Generic.List<int> capacityValues,
            System.Collections.Generic.List<string> flavorJobIds,
            System.Collections.Generic.List<int> flavorKinds,
            System.Collections.Generic.List<int> flavorPerUnits,
            System.Collections.Generic.List<string> disabledJobIds)
        {
            yieldJobIds.Clear();
            yieldValues.Clear();
            foreach (var pair in _yieldMultipliers)
            {
                yieldJobIds.Add(pair.Key);
                yieldValues.Add(pair.Value);
            }

            capacityJobIds.Clear();
            capacityValues.Clear();
            foreach (var pair in _capacityBonuses)
            {
                capacityJobIds.Add(pair.Key);
                capacityValues.Add(pair.Value);
            }

            flavorJobIds.Clear();
            flavorKinds.Clear();
            flavorPerUnits.Clear();
            foreach (var pair in _bonusFlavors)
            {
                if (!_bonusFlavorPerUnit.TryGetValue(pair.Key, out int perUnit) || perUnit <= 0)
                    continue;
                flavorJobIds.Add(pair.Key);
                flavorKinds.Add((int)pair.Value);
                flavorPerUnits.Add(perUnit);
            }

            disabledJobIds.Clear();
            disabledJobIds.AddRange(_disabledJobs);
        }

        public void ApplyState(
            System.Collections.Generic.IList<string> yieldJobIds,
            System.Collections.Generic.IList<float> yieldValues,
            System.Collections.Generic.IList<string> capacityJobIds,
            System.Collections.Generic.IList<int> capacityValues,
            System.Collections.Generic.IList<string> flavorJobIds,
            System.Collections.Generic.IList<int> flavorKinds,
            System.Collections.Generic.IList<int> flavorPerUnits,
            System.Collections.Generic.IList<string> disabledJobIds)
        {
            ResetRun();

            if (yieldJobIds != null && yieldValues != null)
            {
                int n = Mathf.Min(yieldJobIds.Count, yieldValues.Count);
                for (int i = 0; i < n; i++)
                    _yieldMultipliers[yieldJobIds[i]] = Mathf.Max(0f, yieldValues[i]);
            }

            if (capacityJobIds != null && capacityValues != null)
            {
                int n = Mathf.Min(capacityJobIds.Count, capacityValues.Count);
                for (int i = 0; i < n; i++)
                    _capacityBonuses[capacityJobIds[i]] = capacityValues[i];
            }

            if (flavorJobIds != null && flavorKinds != null && flavorPerUnits != null)
            {
                int n = Mathf.Min(Mathf.Min(flavorJobIds.Count, flavorKinds.Count), flavorPerUnits.Count);
                for (int i = 0; i < n; i++)
                {
                    if (string.IsNullOrEmpty(flavorJobIds[i]) || flavorPerUnits[i] <= 0) continue;
                    _bonusFlavors[flavorJobIds[i]] = (FlavorType)flavorKinds[i];
                    _bonusFlavorPerUnit[flavorJobIds[i]] = flavorPerUnits[i];
                }
            }

            if (disabledJobIds != null)
            {
                for (int i = 0; i < disabledJobIds.Count; i++)
                {
                    if (!string.IsNullOrEmpty(disabledJobIds[i]))
                        _disabledJobs.Add(disabledJobIds[i]);
                }
            }
        }
    }
}
