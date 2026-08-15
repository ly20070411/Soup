using System.Collections.Generic;
using UnityEngine;

namespace Soup.Levels
{
    /// <summary>
    /// Ordered campaign level list.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Soup/Levels/Level Database", order = 1)]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<LevelItem> levels = new List<LevelItem>();

        private Dictionary<string, LevelItem> _byId;
        private bool _indexDirty = true;

        public IReadOnlyList<LevelItem> Levels => levels;

        public int Count => levels?.Count ?? 0;

        public void MarkDirty() => _indexDirty = true;

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, LevelItem>();
            if (levels == null)
            {
                _indexDirty = false;
                return;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                var item = levels[i];
                if (item == null) continue;
                item.EnsureDefaultIdFromName();
                var key = item.Id;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (_byId.ContainsKey(key))
                {
                    Debug.LogWarning($"[LevelDatabase] Duplicate id '{key}' on {item.name}. Keeping first entry.", this);
                    continue;
                }

                _byId.Add(key, item);
            }

            _indexDirty = false;
        }

        private void EnsureIndex()
        {
            if (_indexDirty || _byId == null)
                RebuildIndex();
        }

        public bool Contains(LevelItem item) =>
            item != null && levels != null && levels.Contains(item);

        public LevelItem GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            EnsureIndex();
            return _byId != null && _byId.TryGetValue(id, out var item) ? item : null;
        }

        public bool TryGet(string id, out LevelItem item)
        {
            item = GetById(id);
            return item != null;
        }

        public int IndexOf(LevelItem item) => item != null && levels != null ? levels.IndexOf(item) : -1;

#if UNITY_EDITOR
        public void EditorAdd(LevelItem item)
        {
            if (item == null || levels == null || levels.Contains(item)) return;
            levels.Add(item);
            MarkDirty();
        }
#endif
    }
}
