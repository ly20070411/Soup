using System.Collections.Generic;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// Backend aggregator for all job/station definitions.
    /// Runtime systems should query through JobManager, which wraps this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "JobDatabase", menuName = "Soup/Jobs/Job Database", order = 1)]
    public class JobDatabase : ScriptableObject
    {
        [SerializeField] private List<JobItem> jobs = new List<JobItem>();

        private Dictionary<string, JobItem> _byId;
        private bool _indexDirty = true;

        public IReadOnlyList<JobItem> Jobs => jobs;

        public int Count => jobs?.Count ?? 0;

        public void MarkDirty()
        {
            _indexDirty = true;
        }

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, JobItem>();
            if (jobs == null)
            {
                _indexDirty = false;
                return;
            }

            for (int i = 0; i < jobs.Count; i++)
            {
                var item = jobs[i];
                if (item == null) continue;
                item.EnsureDefaultIdFromName();
                var key = item.Id;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (_byId.ContainsKey(key))
                {
                    Debug.LogWarning($"[JobDatabase] Duplicate id '{key}' on {item.name}. Keeping first entry.", this);
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

        public bool Contains(JobItem item)
        {
            return item != null && jobs != null && jobs.Contains(item);
        }

        public bool TryGet(string id, out JobItem item)
        {
            EnsureIndex();
            item = null;
            if (string.IsNullOrWhiteSpace(id) || _byId == null) return false;
            return _byId.TryGetValue(id, out item);
        }

        public JobItem GetById(string id)
        {
            return TryGet(id, out var item) ? item : null;
        }

        public JobItem FindByName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) || jobs == null) return null;
            for (int i = 0; i < jobs.Count; i++)
            {
                var item = jobs[i];
                if (item != null && string.Equals(item.DisplayName, displayName, System.StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return null;
        }

        public List<JobItem> FindByType(JobType jobType)
        {
            var result = new List<JobItem>();
            if (jobs == null) return result;

            for (int i = 0; i < jobs.Count; i++)
            {
                var item = jobs[i];
                if (item != null && item.JobType == jobType)
                    result.Add(item);
            }

            return result;
        }

        public bool Add(JobItem item)
        {
            if (item == null) return false;
            if (jobs == null)
                jobs = new List<JobItem>();
            if (jobs.Contains(item)) return false;

            jobs.Add(item);
            MarkDirty();
            return true;
        }

        public bool Remove(JobItem item)
        {
            if (item == null || jobs == null) return false;
            bool removed = jobs.Remove(item);
            if (removed) MarkDirty();
            return removed;
        }

        public void SetJobs(List<JobItem> items)
        {
            jobs = items ?? new List<JobItem>();
            MarkDirty();
        }

        public void RemoveNullEntries()
        {
            if (jobs == null) return;
            int before = jobs.Count;
            jobs.RemoveAll(i => i == null);
            if (jobs.Count != before)
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
