using System.Collections.Generic;
using Soup.Employees;
using Soup.Game;
using Soup.Levels;
using UnityEngine;

namespace Soup.Relics
{
    /// <summary>
    /// Runtime catalog + owned relics for the current run.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RelicManager : MonoBehaviour
    {
        public const string ResourcesDatabasePath = "RelicDatabase";
        public const string IncentiveId = "incentive";
        public const string FatigueId = "fatigue";

        [SerializeField] private RelicDatabase database;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly List<RelicItem> _owned = new List<RelicItem>();
        private int _previousUnusedWarehouse;
        private bool _grantingAcquireEffects;
        private bool _subscribedLevel;

        public static RelicManager Instance { get; private set; }

        public RelicDatabase Database => database;

        public IReadOnlyList<RelicItem> All =>
            database != null ? database.Relics : System.Array.Empty<RelicItem>();

        public IReadOnlyList<RelicItem> Owned => _owned;

        public int PreviousUnusedWarehouse => _previousUnusedWarehouse;

        public static void Initialize(RelicDatabase db)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(RelicManager));
                Instance = go.AddComponent<RelicManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            Instance.database = db;
            Instance.database?.RebuildIndex();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            var db = Resources.Load<RelicDatabase>(ResourcesDatabasePath);
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
                database = Resources.Load<RelicDatabase>(ResourcesDatabasePath);

            database?.RebuildIndex();
            TrySubscribeLevel();
        }

        private void OnEnable() => TrySubscribeLevel();

        private void OnDisable()
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.LevelStarted -= OnLevelStarted;
            _subscribedLevel = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void TrySubscribeLevel()
        {
            if (_subscribedLevel) return;
            if (LevelManager.Instance == null) return;
            LevelManager.Instance.LevelStarted -= OnLevelStarted;
            LevelManager.Instance.LevelStarted += OnLevelStarted;
            _subscribedLevel = true;
        }

        private void OnLevelStarted(LevelItem _)
        {
            var ctx = BuildImmediateContext();
            ctx.LevelTurnNumber = 1;
            RelicEffectRunner.Run(RelicTrigger.LevelStart, ctx);
        }

        public void ResetRun()
        {
            _owned.Clear();
            _previousUnusedWarehouse = 0;
            // New Game no longer grants a starting relic; shop / events supply them.
        }

        /// <summary>Debug helper: grant every Starting-stage relic (not used by normal New Game).</summary>
        public void GrantStartingRelics()
        {
            if (database == null) return;
            var starting = database.FindByStage(RelicAcquireStage.Starting);
            for (int i = 0; i < starting.Count; i++)
            {
                var relic = starting[i];
                if (relic == null) continue;
                if (Has(relic)) continue;
                Acquire(relic);
            }
        }

        public void ApplyOwnedIds(IList<string> relicIds)
        {
            _owned.Clear();
            if (relicIds == null) return;

            // Restore without re-firing OnAcquire (save already applied resources).
            _grantingAcquireEffects = true;
            try
            {
                for (int i = 0; i < relicIds.Count; i++)
                {
                    var relic = GetById(relicIds[i]);
                    if (relic != null)
                        _owned.Add(relic);
                }
            }
            finally
            {
                _grantingAcquireEffects = false;
            }
        }

        public void RememberUnusedWarehouse(int space)
        {
            _previousUnusedWarehouse = Mathf.Max(0, space);
        }

        public bool Has(RelicItem relic) => relic != null && _owned.Contains(relic);

        public int CountOwned(RelicItem relic)
        {
            if (relic == null) return 0;
            int n = 0;
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] == relic)
                    n++;
            }

            return n;
        }

        public int CountOwnedId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            int n = 0;
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null && _owned[i].Id == id)
                    n++;
            }

            return n;
        }

        public bool HasId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null && _owned[i].Id == id)
                    return true;
            }

            return false;
        }

        /// <summary>Acquire a relic for this run. Allows stacks (e.g. 疲倦 ×2).</summary>
        public bool Acquire(RelicItem relic)
        {
            if (relic == null)
                return false;
            _owned.Add(relic);
            if (!_grantingAcquireEffects)
                FireOnAcquire(relic);
            return true;
        }

        public bool AcquireById(string id)
        {
            var relic = GetById(id);
            return Acquire(relic);
        }

        public bool RemoveOwned(RelicItem relic)
        {
            return relic != null && _owned.Remove(relic);
        }

        public float GetGlobalLaborModifier()
        {
            return RelicEffectRunner.SumGlobalLaborEfficiency(GetLevelTurnNumber());
        }

        public float GetEmployeeLaborModifier(string employeeTypeId)
        {
            return RelicEffectRunner.SumEmployeeLaborEfficiency(employeeTypeId, GetLevelTurnNumber());
        }

        /// <summary>Called when elf count decreases (positive lost count).</summary>
        public void NotifyElvesLost(int lostCount)
        {
            if (lostCount <= 0) return;
            int per = RelicEffectRunner.SumGhostsPerElfLoss();
            if (per <= 0) return;
            EmployeeManager.Instance?.Add(EmployeeManager.GhostId, lostCount * per);
        }

        public bool TryGet(string id, out RelicItem item)
        {
            item = null;
            return database != null && database.TryGet(id, out item);
        }

        public RelicItem GetById(string id) => database != null ? database.GetById(id) : null;

        public RelicItem FindByName(string displayName) =>
            database != null ? database.FindByName(displayName) : null;

        public List<RelicItem> GetRelicsForStage(RelicAcquireStage stage) =>
            database != null ? database.FindByStage(stage) : new List<RelicItem>();

        /// <summary>
        /// Build a random offer of relics for <paramref name="preferredStage"/>.
        /// Unique relics already owned are skipped; <see cref="RelicItem.AllowMultiple"/>
        /// relics (e.g. 激励) can appear again and stack.
        /// When <paramref name="fillFromOtherStages"/> is true and the preferred pool
        /// is short, remaining slots are filled from other stages (used by event rewards).
        /// Shop should pass false so only shop relics appear.
        /// </summary>
        public List<RelicItem> CreateOffer(
            int count,
            RelicAcquireStage preferredStage,
            bool fillFromOtherStages = true)
        {
            var result = new List<RelicItem>(Mathf.Max(0, count));
            if (count <= 0 || database == null) return result;

            var preferred = new List<RelicItem>();
            var others = new List<RelicItem>();
            var all = database.Relics;
            for (int i = 0; i < all.Count; i++)
            {
                var relic = all[i];
                if (relic == null) continue;
                if (!relic.AllowMultiple && _owned.Contains(relic)) continue;
                if (RelicAcquireStageUtil.MatchesStageFilter(relic.AcquireStage, preferredStage))
                    preferred.Add(relic);
                else if (fillFromOtherStages)
                    others.Add(relic);
            }

            // 开局必须严格保持三选一的开局池，不用事件遗物补位。
            if (preferredStage == RelicAcquireStage.Starting)
                others.Clear();

            Shuffle(preferred);
            for (int i = 0; i < preferred.Count && result.Count < count; i++)
                result.Add(preferred[i]);

            if (fillFromOtherStages && result.Count < count)
            {
                Shuffle(others);
                for (int i = 0; i < others.Count && result.Count < count; i++)
                    result.Add(others[i]);
            }

            return result;
        }

        public static int GetLevelTurnNumber()
        {
            var levels = LevelManager.Instance;
            if (levels != null && levels.HasLevels && levels.Current != null)
                return Mathf.Max(1, levels.LevelTurnIndex);

            var turns = TurnManager.Instance;
            if (turns != null)
                return Mathf.Max(1, turns.TurnIndex + 1);
            return 1;
        }

        private void FireOnAcquire(RelicItem relic)
        {
            if (relic == null) return;
            bool nested = _grantingAcquireEffects;
            _grantingAcquireEffects = true;
            try
            {
                var ctx = BuildImmediateContext();
                RelicEffectRunner.RunRelic(relic, RelicTrigger.OnAcquire, ctx);
            }
            finally
            {
                _grantingAcquireEffects = nested;
            }
        }

        private RelicContext BuildImmediateContext()
        {
            return new RelicContext(ResourceStore.Instance, null)
            {
                LevelTurnNumber = GetLevelTurnNumber(),
                PreviousUnusedWarehouse = _previousUnusedWarehouse
            };
        }

        private static void Shuffle(List<RelicItem> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
