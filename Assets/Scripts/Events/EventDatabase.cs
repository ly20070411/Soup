using System.Collections.Generic;
using UnityEngine;

namespace Soup.Events
{
    /// <summary>
    /// Aggregator for all event definitions.
    /// </summary>
    [CreateAssetMenu(fileName = "EventDatabase", menuName = "Soup/Events/Event Database", order = 1)]
    public class EventDatabase : ScriptableObject
    {
        [SerializeField] private List<EventItem> events = new List<EventItem>();

        private Dictionary<string, EventItem> _byId;
        private bool _indexDirty = true;

        public IReadOnlyList<EventItem> Events => events;

        public int Count => events?.Count ?? 0;

        public void MarkDirty() => _indexDirty = true;

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, EventItem>();
            if (events == null)
            {
                _indexDirty = false;
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                var item = events[i];
                if (item == null) continue;
                item.EnsureDefaultIdFromName();
                var key = item.Id;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (_byId.ContainsKey(key))
                {
                    Debug.LogWarning($"[EventDatabase] Duplicate id '{key}' on {item.name}. Keeping first entry.", this);
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

        public bool Contains(EventItem item) =>
            item != null && events != null && events.Contains(item);

        public EventItem GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            EnsureIndex();
            return _byId != null && _byId.TryGetValue(id, out var item) ? item : null;
        }

        public bool TryGet(string id, out EventItem item)
        {
            item = GetById(id);
            return item != null;
        }

#if UNITY_EDITOR
        public void EditorAdd(EventItem item)
        {
            if (item == null || events == null || events.Contains(item)) return;
            events.Add(item);
            MarkDirty();
        }
#endif
    }
}
