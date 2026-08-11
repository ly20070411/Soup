using System.Collections.Generic;
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

        [SerializeField] private RelicDatabase database;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private readonly List<RelicItem> _owned = new List<RelicItem>();

        public static RelicManager Instance { get; private set; }

        public RelicDatabase Database => database;

        public IReadOnlyList<RelicItem> All =>
            database != null ? database.Relics : System.Array.Empty<RelicItem>();

        public IReadOnlyList<RelicItem> Owned => _owned;

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
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ResetRun() => _owned.Clear();

        public bool Has(RelicItem relic) => relic != null && _owned.Contains(relic);

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

        /// <summary>Acquire a relic for this run. Returns false if already owned or null.</summary>
        public bool Acquire(RelicItem relic)
        {
            if (relic == null || _owned.Contains(relic))
                return false;
            _owned.Add(relic);
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
    }
}
