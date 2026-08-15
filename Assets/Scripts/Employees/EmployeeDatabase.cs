using System.Collections.Generic;
using UnityEngine;

namespace Soup.Employees
{
    /// <summary>
    /// Aggregator for all employee type definitions.
    /// </summary>
    [CreateAssetMenu(fileName = "EmployeeDatabase", menuName = "Soup/Employees/Employee Database", order = 1)]
    public class EmployeeDatabase : ScriptableObject
    {
        [SerializeField] private List<EmployeeItem> employees = new List<EmployeeItem>();

        private Dictionary<string, EmployeeItem> _byId;
        private bool _indexDirty = true;

        public IReadOnlyList<EmployeeItem> Employees => employees;

        public int Count => employees?.Count ?? 0;

        public void MarkDirty() => _indexDirty = true;

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, EmployeeItem>();
            if (employees == null)
            {
                _indexDirty = false;
                return;
            }

            for (int i = 0; i < employees.Count; i++)
            {
                var item = employees[i];
                if (item == null) continue;
                item.EnsureDefaultIdFromName();
                var key = item.Id;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (_byId.ContainsKey(key))
                {
                    Debug.LogWarning($"[EmployeeDatabase] Duplicate id '{key}' on {item.name}. Keeping first entry.", this);
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

        public bool Contains(EmployeeItem item) =>
            item != null && employees != null && employees.Contains(item);

        public EmployeeItem GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            EnsureIndex();
            return _byId != null && _byId.TryGetValue(id, out var item) ? item : null;
        }

        public bool TryGet(string id, out EmployeeItem item)
        {
            item = GetById(id);
            return item != null;
        }

#if UNITY_EDITOR
        public void EditorAdd(EmployeeItem item)
        {
            if (item == null || employees == null || employees.Contains(item)) return;
            employees.Add(item);
            MarkDirty();
        }
#endif
    }
}
