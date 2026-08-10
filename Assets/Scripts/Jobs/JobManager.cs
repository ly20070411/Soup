using System.Collections.Generic;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// Runtime facade / backend integrator for job data.
    /// Attach to a bootstrap object or call JobManager.Initialize(database).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class JobManager : MonoBehaviour
    {
        public const string ResourcesDatabasePath = "JobDatabase";

        [SerializeField] private JobDatabase database;
        [SerializeField] private bool dontDestroyOnLoad = true;

        public static JobManager Instance { get; private set; }

        public JobDatabase Database => database;

        public IReadOnlyList<JobItem> All =>
            database != null ? database.Jobs : System.Array.Empty<JobItem>();

        public static void Initialize(JobDatabase db)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(JobManager));
                Instance = go.AddComponent<JobManager>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

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
                database = Resources.Load<JobDatabase>(ResourcesDatabasePath);

            database?.RebuildIndex();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool TryGet(string id, out JobItem item)
        {
            item = null;
            return database != null && database.TryGet(id, out item);
        }

        public JobItem GetById(string id) => database != null ? database.GetById(id) : null;

        public JobItem FindByName(string displayName) =>
            database != null ? database.FindByName(displayName) : null;

        public List<JobItem> FindByType(JobType jobType) =>
            database != null ? database.FindByType(jobType) : new List<JobItem>();
    }
}
