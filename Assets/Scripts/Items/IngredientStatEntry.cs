using System;
using UnityEngine;

namespace Soup.Items
{
    /// <summary>
    /// Extensible named numeric value on an ingredient.
    /// </summary>
    [Serializable]
    public class IngredientStatEntry
    {
        [Tooltip("Stat id used by gameplay code, e.g. salty, sweet, cookTime.")]
        public string key = string.Empty;

        public float value;

        public IngredientStatEntry()
        {
        }

        public IngredientStatEntry(string key, float value)
        {
            this.key = key;
            this.value = value;
        }
    }
}
