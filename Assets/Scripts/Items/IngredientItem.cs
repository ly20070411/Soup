using System.Collections.Generic;
using UnityEngine;

namespace Soup.Items
{
    /// <summary>
    /// Single ingredient definition: identity, visual, tags, and numeric values.
    /// </summary>
    [CreateAssetMenu(fileName = "Ingredient_", menuName = "Soup/Items/Ingredient", order = 0)]
    public class IngredientItem : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = "New Ingredient";
        [TextArea(2, 5)]
        [SerializeField] private string description = string.Empty;

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color tint = Color.white;

        [Header("Classification")]
        [SerializeField] private IngredientCategory category = IngredientCategory.Other;
        [SerializeField] private List<string> tags = new List<string>();

        [Header("Core Values")]
        [SerializeField, Min(0)] private int price;
        [SerializeField, Range(0, 5)] private int rarity;
        [SerializeField, Min(1)] private int maxStack = 99;
        [SerializeField, Min(0f)] private float weight = 1f;

        [Header("Gameplay Stats")]
        [SerializeField] private List<IngredientStatEntry> stats = new List<IngredientStatEntry>
        {
            new IngredientStatEntry("salty", 0f),
            new IngredientStatEntry("sweet", 0f),
            new IngredientStatEntry("sour", 0f),
            new IngredientStatEntry("bitter", 0f),
            new IngredientStatEntry("umami", 0f),
            new IngredientStatEntry("heat", 0f),
            new IngredientStatEntry("cookTime", 1f)
        };

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public Color Tint => tint;
        public IngredientCategory Category => category;
        public IReadOnlyList<string> Tags => tags;
        public int Price => price;
        public int Rarity => rarity;
        public int MaxStack => maxStack;
        public float Weight => weight;
        public IReadOnlyList<IngredientStatEntry> Stats => stats;

        public void SetIdentity(string newId, string newDisplayName)
        {
            id = newId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "New Ingredient" : newDisplayName.Trim();
        }

        public void SetDescription(string value) => description = value ?? string.Empty;

        public void SetIcon(Sprite value) => icon = value;

        public void SetTint(Color value) => tint = value;

        public void SetCategory(IngredientCategory value) => category = value;

        public void SetCoreValues(int newPrice, int newRarity, int newMaxStack, float newWeight)
        {
            price = Mathf.Max(0, newPrice);
            rarity = Mathf.Clamp(newRarity, 0, 5);
            maxStack = Mathf.Max(1, newMaxStack);
            weight = Mathf.Max(0f, newWeight);
        }

        public void SetTags(IEnumerable<string> newTags)
        {
            tags = new List<string>();
            if (newTags == null) return;

            var seen = new HashSet<string>();
            foreach (var raw in newTags)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var tag = raw.Trim();
                if (seen.Add(tag))
                    tags.Add(tag);
            }
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null) return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public float GetStat(string key, float defaultValue = 0f)
        {
            if (string.IsNullOrWhiteSpace(key) || stats == null) return defaultValue;
            for (int i = 0; i < stats.Count; i++)
            {
                var entry = stats[i];
                if (entry != null && string.Equals(entry.key, key, System.StringComparison.OrdinalIgnoreCase))
                    return entry.value;
            }
            return defaultValue;
        }

        public void SetStat(string key, float value)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            key = key.Trim();

            if (stats == null)
                stats = new List<IngredientStatEntry>();

            for (int i = 0; i < stats.Count; i++)
            {
                if (stats[i] != null && string.Equals(stats[i].key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    stats[i].value = value;
                    return;
                }
            }

            stats.Add(new IngredientStatEntry(key, value));
        }

        public void EnsureDefaultIdFromName()
        {
            if (!string.IsNullOrWhiteSpace(id)) return;
            id = SanitizeId(displayName);
        }

        public static string SanitizeId(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "ingredient_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            var chars = source.Trim().ToLowerInvariant().ToCharArray();
            var builder = new System.Text.StringBuilder(chars.Length);
            bool lastWasSeparator = false;
            foreach (char c in chars)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            var result = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(result)
                ? "ingredient_" + System.Guid.NewGuid().ToString("N").Substring(0, 8)
                : result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = name;

            EnsureDefaultIdFromName();
            maxStack = Mathf.Max(1, maxStack);
            rarity = Mathf.Clamp(rarity, 0, 5);
            price = Mathf.Max(0, price);
            weight = Mathf.Max(0f, weight);
        }
#endif
    }
}
