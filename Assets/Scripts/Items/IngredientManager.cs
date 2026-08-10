using System.Collections.Generic;
using UnityEngine;

namespace Soup.Items
{
    /// <summary>
    /// Runtime facade / backend integrator for ingredient data.
    /// Attach to a bootstrap object or call IngredientManager.Initialize(database).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class IngredientManager : MonoBehaviour
    {
        public const string ResourcesDatabasePath = "IngredientDatabase";

        [SerializeField] private IngredientDatabase database;
        [SerializeField] private bool dontDestroyOnLoad = true;

        public static IngredientManager Instance { get; private set; }

        public IngredientDatabase Database => database;

        public IReadOnlyList<IngredientItem> All =>
            database != null ? database.Ingredients : System.Array.Empty<IngredientItem>();

        public static void Initialize(IngredientDatabase db)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(IngredientManager));
                Instance = go.AddComponent<IngredientManager>();
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
                database = Resources.Load<IngredientDatabase>(ResourcesDatabasePath);

            database?.RebuildIndex();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool TryGet(string id, out IngredientItem item)
        {
            item = null;
            return database != null && database.TryGet(id, out item);
        }

        public IngredientItem GetById(string id) => database != null ? database.GetById(id) : null;

        public IngredientItem FindByName(string displayName) =>
            database != null ? database.FindByName(displayName) : null;

        public List<IngredientItem> FindByTag(string tag) =>
            database != null ? database.FindByTag(tag) : new List<IngredientItem>();

        public List<IngredientItem> FindByCategory(IngredientCategory category) =>
            database != null ? database.FindByCategory(category) : new List<IngredientItem>();
    }
}
