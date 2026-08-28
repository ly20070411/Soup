using System.Collections.Generic;
using UnityEngine;

namespace Soup.Items
{
    /// <summary>
    /// Backend aggregator for all ingredient definitions.
    /// Runtime systems should query through IngredientManager, which wraps this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "IngredientDatabase", menuName = "Soup/Items/Ingredient Database", order = 1)]
    public class IngredientDatabase : ScriptableObject
    {
        [SerializeField] private List<IngredientItem> ingredients = new List<IngredientItem>();

        private Dictionary<string, IngredientItem> _byId;
        private bool _indexDirty = true;

        public IReadOnlyList<IngredientItem> Ingredients => ingredients;

        public int Count => ingredients?.Count ?? 0;

        public void MarkDirty()
        {
            _indexDirty = true;
        }

        public void RebuildIndex()
        {
            _byId = new Dictionary<string, IngredientItem>();
            if (ingredients == null)
            {
                _indexDirty = false;
                return;
            }

            for (int i = 0; i < ingredients.Count; i++)
            {
                var item = ingredients[i];
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
                        $"[IngredientDatabase] Duplicate id '{key}' on {item.name}; " +
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

        public bool Contains(IngredientItem item)
        {
            return item != null && ingredients != null && ingredients.Contains(item);
        }

        public bool TryGet(string id, out IngredientItem item)
        {
            EnsureIndex();
            item = null;
            if (string.IsNullOrWhiteSpace(id) || _byId == null) return false;
            return _byId.TryGetValue(id, out item);
        }

        public IngredientItem GetById(string id)
        {
            return TryGet(id, out var item) ? item : null;
        }

        public IngredientItem FindByName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) || ingredients == null) return null;
            for (int i = 0; i < ingredients.Count; i++)
            {
                var item = ingredients[i];
                if (item != null && string.Equals(item.DisplayName, displayName, System.StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return null;
        }

        public List<IngredientItem> FindByTag(string tag)
        {
            var result = new List<IngredientItem>();
            if (string.IsNullOrWhiteSpace(tag) || ingredients == null) return result;

            for (int i = 0; i < ingredients.Count; i++)
            {
                var item = ingredients[i];
                if (item != null && item.HasTag(tag))
                    result.Add(item);
            }

            return result;
        }

        public List<IngredientItem> FindByCategory(IngredientCategory category)
        {
            var result = new List<IngredientItem>();
            if (ingredients == null) return result;

            for (int i = 0; i < ingredients.Count; i++)
            {
                var item = ingredients[i];
                if (item != null && item.Category == category)
                    result.Add(item);
            }

            return result;
        }

        public bool Add(IngredientItem item)
        {
            if (item == null) return false;
            if (ingredients == null)
                ingredients = new List<IngredientItem>();
            if (ingredients.Contains(item)) return false;

            ingredients.Add(item);
            MarkDirty();
            return true;
        }

        public bool Remove(IngredientItem item)
        {
            if (item == null || ingredients == null) return false;
            bool removed = ingredients.Remove(item);
            if (removed) MarkDirty();
            return removed;
        }

        public void SetIngredients(List<IngredientItem> items)
        {
            ingredients = items ?? new List<IngredientItem>();
            MarkDirty();
        }

        public void RemoveNullEntries()
        {
            if (ingredients == null) return;
            int before = ingredients.Count;
            ingredients.RemoveAll(i => i == null);
            if (ingredients.Count != before)
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
