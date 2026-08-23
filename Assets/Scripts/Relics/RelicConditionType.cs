namespace Soup.Relics
{
    /// <summary>
    /// Gate that must pass before a relic effect applies.
    /// </summary>
    public enum RelicConditionType
    {
        Always = 0,
        /// <summary>
        /// 当前没有未摧毁的采集岗产出该分类食材（如素食主义：无肉类采集岗）。
        /// </summary>
        NoCategoryGathered = 1,
        /// <summary>At least N distinct flavors currently have stock &gt; 0.</summary>
        HasFlavorCountAtLeast = 2,
        /// <summary>At most N distinct flavors currently have stock &gt; 0.</summary>
        HasFlavorCountAtMost = 3,
        /// <summary>
        /// Current level turn number (1-based) is in [conditionInt, conditionIntMax].
        /// </summary>
        TurnIndexInRange = 4,
        /// <summary>
        /// 空闲仓库量 &lt; 仓库总量的一半（仓库已较满）。
        /// </summary>
        WarehouseSpaceBelowHalf = 5,
        /// <summary>
        /// 本关回合处于最后 conditionInt 个回合内（含当前；如 5 → 末 5 回合）。
        /// </summary>
        LastNLevelTurns = 6,
        /// <summary>所有已拥有员工均分配在烹饪岗（无空闲、无非烹饪分配）。</summary>
        AllEmployeesOnCook = 7
    }
}
