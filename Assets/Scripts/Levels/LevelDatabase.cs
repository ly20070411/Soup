using System.Collections.Generic;
using UnityEngine;

namespace Soup.Levels
{
    /// <summary>
    /// Aggregator for all campaign level definitions, ordered by OrderIndex.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Soup/Levels/Level Database", order = 1)]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<LevelItem> levels = new List<LevelItem>();

        private Dictionary<string, LevelItem> _byId;
        private List<LevelItem> _ordered;
        private bool _indexDirty = true;

        public IReadOnlyList<LevelItem> Levels => levels;
        public int Count => levels?.Count ?? 0;

        public void MarkDirty() => _indexDirty = true;

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, LevelItem>();
            _ordered = new List<LevelItem>();
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
                    Debug.LogWarning($"[LevelDatabase] Duplicate id '{key}' on {item.name}. Keeping first.", this);
                    continue;
                }

                _byId.Add(key, item);
                _ordered.Add(item);
            }

            _ordered.Sort((a, b) =>
            {
                int cmp = a.OrderIndex.CompareTo(b.OrderIndex);
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.Id, b.Id);
            });

            _indexDirty = false;
        }

        private void EnsureIndex()
        {
            if (_indexDirty || _byId == null || _ordered == null)
                RebuildIndex();
        }

        /// <summary>Levels sorted by OrderIndex ascending.</summary>
        public IReadOnlyList<LevelItem> GetOrdered()
        {
            EnsureIndex();
            return _ordered ?? (IReadOnlyList<LevelItem>)System.Array.Empty<LevelItem>();
        }

        public LevelItem GetByOrderIndex(int orderIndex)
        {
            EnsureIndex();
            if (_ordered == null) return null;
            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_ordered[i] != null && _ordered[i].OrderIndex == orderIndex)
                    return _ordered[i];
            }

            return null;
        }

        public LevelItem GetAtOrderedIndex(int index)
        {
            EnsureIndex();
            if (_ordered == null || index < 0 || index >= _ordered.Count)
                return null;
            return _ordered[index];
        }

        public int IndexOfOrdered(LevelItem item)
        {
            EnsureIndex();
            if (item == null || _ordered == null) return -1;
            return _ordered.IndexOf(item);
        }

        public bool Contains(LevelItem item) =>
            item != null && levels != null && levels.Contains(item);

        public bool TryGet(string id, out LevelItem item)
        {
            EnsureIndex();
            item = null;
            if (string.IsNullOrWhiteSpace(id) || _byId == null) return false;
            return _byId.TryGetValue(id, out item);
        }

        public LevelItem GetById(string id) => TryGet(id, out var item) ? item : null;

        public bool Add(LevelItem item)
        {
            if (item == null) return false;
            if (levels == null)
                levels = new List<LevelItem>();
            if (levels.Contains(item)) return false;
            levels.Add(item);
            MarkDirty();
            return true;
        }

        public bool Remove(LevelItem item)
        {
            if (item == null || levels == null) return false;
            bool removed = levels.Remove(item);
            if (removed) MarkDirty();
            return removed;
        }

        public void RemoveNullEntries()
        {
            if (levels == null) return;
            int before = levels.Count;
            levels.RemoveAll(i => i == null);
            if (levels.Count != before)
                MarkDirty();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RemoveNullEntries();
            MarkDirty();
        }
#endif
    }
}
