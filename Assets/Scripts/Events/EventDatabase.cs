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

        public bool TryGet(string id, out EventItem item)
        {
            EnsureIndex();
            item = null;
            if (string.IsNullOrWhiteSpace(id) || _byId == null) return false;
            return _byId.TryGetValue(id, out item);
        }

        public EventItem GetById(string id) => TryGet(id, out var item) ? item : null;

        public List<EventItem> FindByTrigger(EventTriggerMoment moment)
        {
            var result = new List<EventItem>();
            if (events == null) return result;

            for (int i = 0; i < events.Count; i++)
            {
                var item = events[i];
                if (item != null && item.TriggerMoment == moment)
                    result.Add(item);
            }

            return result;
        }

        public bool Add(EventItem item)
        {
            if (item == null) return false;
            if (events == null)
                events = new List<EventItem>();
            if (events.Contains(item)) return false;

            events.Add(item);
            MarkDirty();
            return true;
        }

        public bool Remove(EventItem item)
        {
            if (item == null || events == null) return false;
            bool removed = events.Remove(item);
            if (removed) MarkDirty();
            return removed;
        }

        public void SetEvents(List<EventItem> items)
        {
            events = items ?? new List<EventItem>();
            MarkDirty();
        }

        public void RemoveNullEntries()
        {
            if (events == null) return;
            int before = events.Count;
            events.RemoveAll(i => i == null);
            if (events.Count != before)
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
