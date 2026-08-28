using System.Collections.Generic;
using UnityEngine;

namespace Soup.Employees
{
    /// <summary>
    /// Aggregator for all employee unit definitions.
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
                    // 同名条目（displayName 相同 → SanitizeId 相同）会导致第二个永久查不到：
                    // 追加确定性后缀生成唯一 id，保证两个条目都可被 id 查询命中。
                    int suffix = 2;
                    string unique;
                    do
                    {
                        unique = key + "_" + suffix;
                        suffix++;
                    } while (_byId.ContainsKey(unique));

                    item.SetIdentity(unique, item.DisplayName);
                    Debug.LogWarning(
                        $"[EmployeeDatabase] Duplicate id '{key}' on {item.name}; " +
                        $"renamed to '{unique}'. Adjust display names if this was unintended.", this);
                    key = unique;
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

        public bool TryGet(string id, out EmployeeItem item)
        {
            EnsureIndex();
            item = null;
            if (string.IsNullOrWhiteSpace(id) || _byId == null) return false;
            return _byId.TryGetValue(id, out item);
        }

        public EmployeeItem GetById(string id) => TryGet(id, out var item) ? item : null;

        public EmployeeItem FindByName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) || employees == null) return null;
            for (int i = 0; i < employees.Count; i++)
            {
                var item = employees[i];
                if (item != null &&
                    string.Equals(item.DisplayName, displayName, System.StringComparison.OrdinalIgnoreCase))
                    return item;
            }

            return null;
        }

        public bool Add(EmployeeItem item)
        {
            if (item == null) return false;
            if (employees == null)
                employees = new List<EmployeeItem>();
            if (employees.Contains(item)) return false;
            employees.Add(item);
            MarkDirty();
            return true;
        }

        public bool Remove(EmployeeItem item)
        {
            if (item == null || employees == null) return false;
            bool removed = employees.Remove(item);
            if (removed) MarkDirty();
            return removed;
        }

        public void RemoveNullEntries()
        {
            if (employees == null) return;
            int before = employees.Count;
            employees.RemoveAll(i => i == null);
            if (employees.Count != before)
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
