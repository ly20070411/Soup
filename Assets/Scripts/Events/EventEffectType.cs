namespace Soup.Events
{
    /// <summary>
    /// Immediate effect applied when a player picks an event option.
    /// </summary>
    public enum EventEffectType
    {
        /// <summary>Add (or remove if negative) elves. intValue = delta.</summary>
        AddElves = 0,
        /// <summary>
        /// Grant N stacks of the 激励 relic (global +0.1 efficiency each).
        /// Kept as enum value 1 for existing event assets; formerly "族长的激励" counter.
        /// </summary>
        AddChiefIncentive = 1,
        /// <summary>Grant a relic. relicReference = target.</summary>
        GrantRelic = 2,
        /// <summary>Add employees. employeeReference + intValue = count.</summary>
        AddEmployee = 3,
        /// <summary>Change warehouse capacity (can be negative). intValue = delta.</summary>
        ModifyWarehouseCapacity = 4,
        /// <summary>Remove all owned stacks of the 疲倦 relic.</summary>
        RemoveAllFatigue = 5,
        /// <summary>Add floatValue to job gather yield (0.3 → ×1.3). jobReference required.</summary>
        ModifyJobYieldBonus = 6,
        /// <summary>Add intValue to job max workers. jobReference required.</summary>
        ModifyJobMaxWorkers = 7,
        /// <summary>
        /// Per gathered unit: intValue → each present raw material; secondaryInt → cold.
        /// jobReference required.
        /// </summary>
        ModifyJobRawAndColdPerUnit = 8,
        /// <summary>Magic-leaf style: random flavor becomes all four flavors. jobReference.</summary>
        EnableJobAllFourFlavors = 9,
        /// <summary>Permanently destroy / lock a gather job. jobReference.</summary>
        DestroyGatherJob = 10,
        /// <summary>
        /// 50% intValue elves (usually negative); else floatValue yield bonus on jobReference.
        /// </summary>
        ChanceElfDeltaOrJobYield = 11
    }
}
