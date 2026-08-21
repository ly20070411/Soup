using System.Collections.Generic;
using UnityEngine;

namespace Soup.Relics
{
    /// <summary>
    /// Aggregator for all relic definitions.
    /// </summary>
    [CreateAssetMenu(fileName = "RelicDatabase", menuName = "Soup/Relics/Relic Database", order = 1)]
    public class RelicDatabase : ScriptableObject
    {
        [SerializeField] private List<RelicItem> relics = new List<RelicItem>();

        private Dictionary<string, RelicItem> _byId;
        private bool _indexDirty = true;

        public IReadOnlyList<RelicItem> Relics => relics;

        public int Count => relics?.Count ?? 0;

        public void MarkDirty() => _indexDirty = true;

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, RelicItem>();
            if (relics == null)
            {
                _indexDirty = false;
                return;
            }

            for (int i = 0; i < relics.Count; i++)
            {
                var item = relics[i];
                if (item == null) continue;
                item.EnsureDefaultIdFromName();
                var key = item.Id;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (_byId.ContainsKey(key))
                {
                    Debug.LogWarning($"[RelicDatabase] Duplicate id '{key}' on {item.name}. Keeping first entry.", this);
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

        public bool Contains(RelicItem item) =>
            item != null && relics != null && relics.Contains(item);

        public bool TryGet(string id, out RelicItem item)
        {
            EnsureIndex();
            item = null;
            if (string.IsNullOrWhiteSpace(id) || _byId == null) return false;
            return _byId.TryGetValue(id, out item);
        }

        public RelicItem GetById(string id) => TryGet(id, out var item) ? item : null;

        public RelicItem FindByName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) || relics == null) return null;
            for (int i = 0; i < relics.Count; i++)
            {
                var item = relics[i];
                if (item != null &&
                    string.Equals(item.DisplayName, displayName, System.StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }

        public List<RelicItem> FindByStage(RelicAcquireStage stage)
        {
            var result = new List<RelicItem>();
            if (relics == null) return result;

            for (int i = 0; i < relics.Count; i++)
            {
                var item = relics[i];
                if (item != null && RelicAcquireStageUtil.MatchesStageFilter(item.AcquireStage, stage))
                    result.Add(item);
            }

            return result;
        }

        public bool Add(RelicItem item)
        {
            if (item == null) return false;
            if (relics == null)
                relics = new List<RelicItem>();
            if (relics.Contains(item)) return false;

            relics.Add(item);
            MarkDirty();
            return true;
        }

        public bool Remove(RelicItem item)
        {
            if (item == null || relics == null) return false;
            bool removed = relics.Remove(item);
            if (removed) MarkDirty();
            return removed;
        }

        public void SetRelics(List<RelicItem> items)
        {
            relics = items ?? new List<RelicItem>();
            MarkDirty();
        }

        public void RemoveNullEntries()
        {
            if (relics == null) return;
            int before = relics.Count;
            relics.RemoveAll(i => i == null);
            if (relics.Count != before)
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
