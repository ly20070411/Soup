using System.Collections.Generic;
using Soup.Game;
using Soup.Items;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Relics
{
    /// <summary>
    /// Per-turn mutable state shared by relic condition/effect evaluation.
    /// </summary>
    public class RelicContext
    {
        private readonly Dictionary<IngredientCategory, int> _gatheredByCategory =
            new Dictionary<IngredientCategory, int>();

        public ResourceStore Store { get; }
        public TurnResult Result { get; }
        public int GatheredUnits { get; private set; }
        public float FinalMultiplier { get; set; } = 1f;
        public float IndependentMultiplier { get; set; } = 1f;
        public bool SpicyUncapped { get; set; }
        public float SpicyMultiplierCap { get; set; }
        /// <summary>Added to spicy bonus score mult (job advance + relics), e.g. 0.5 → ×1.5.</summary>
        public float SpicyScoreMultiplierBonus { get; set; }
        public int LevelTurnNumber { get; set; } = 1;
        public int PreviousUnusedWarehouse { get; set; }
        public int SolidProducedThisBatch { get; set; }
        public System.Action<IngredientYield> ApplyYield { get; set; }

        public RelicContext(ResourceStore store, TurnResult result)
        {
            Store = store;
            Result = result;
        }

        public void RecordGather(IngredientItem ingredient, int units)
        {
            if (units <= 0) return;
            GatheredUnits += units;
            if (ingredient == null) return;

            var category = ingredient.Category;
            if (_gatheredByCategory.TryGetValue(category, out int current))
                _gatheredByCategory[category] = current + units;
            else
                _gatheredByCategory[category] = units;
        }

        public void RecordGatherUnitsOnly(int units)
        {
            if (units > 0)
                GatheredUnits += units;
        }

        public int GetGathered(IngredientCategory category)
        {
            return _gatheredByCategory.TryGetValue(category, out int value) ? value : 0;
        }

        public int CountPresentFlavors()
        {
            if (Store == null) return 0;
            int count = 0;
            if (Store.Spicy > 0) count++;
            if (Store.Sour > 0) count++;
            if (Store.Cold > 0) count++;
            if (Store.Magic > 0) count++;
            return count;
        }
    }
}
