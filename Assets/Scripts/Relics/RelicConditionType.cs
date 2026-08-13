namespace Soup.Relics
{
    /// <summary>
    /// Gate that must pass before a relic effect applies.
    /// </summary>
    public enum RelicConditionType
    {
        Always = 0,
        /// <summary>No gather units this turn produced the given ingredient category.</summary>
        NoCategoryGathered = 1,
        /// <summary>At least N distinct flavors currently have stock &gt; 0.</summary>
        HasFlavorCountAtLeast = 2,
        /// <summary>At most N distinct flavors currently have stock &gt; 0.</summary>
        HasFlavorCountAtMost = 3,
        /// <summary>
        /// Current level turn number (1-based) is in [conditionInt, conditionIntMax].
        /// </summary>
        TurnIndexInRange = 4
    }
}
