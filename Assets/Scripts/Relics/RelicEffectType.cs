namespace Soup.Relics
{
    /// <summary>
    /// Numeric / rule change applied when a relic rule fires.
    /// </summary>
    public enum RelicEffectType
    {
        /// <summary>Add floatValue to final score multiplier (starts at 1).</summary>
        AddFinalMultiplier = 0,
        /// <summary>Add floatValue per present flavor (stock &gt; 0).</summary>
        AddFinalMultiplierPerPresentFlavor = 1,
        /// <summary>Ignore GameConfig spicy multiplier cap this turn.</summary>
        DisableSpicyCap = 2,
        /// <summary>
        /// Every intValue gathered units → grant amount of ingredient (via yield resolver).
        /// </summary>
        GrantIngredientPerGather = 3
    }
}
