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
        HasFlavorCountAtLeast = 2
    }
}
