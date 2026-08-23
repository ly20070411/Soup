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
        GrantIngredientPerGather = 3,
        /// <summary>Add amount to warehouse capacity bonus.</summary>
        ModifyWarehouseCapacity = 4,
        /// <summary>Add amount of raw material (Material field).</summary>
        AddRawMaterial = 5,
        /// <summary>Add amount processed food.</summary>
        AddProcessed = 6,
        /// <summary>Add floatValue to global work efficiency (Passive / TurnStart query).</summary>
        AddGlobalLaborEfficiency = 7,
        /// <summary>Add floatValue to a specific employee type efficiency (EmployeeTypeId).</summary>
        AddEmployeeTypeLaborEfficiency = 8,
        /// <summary>Multiply independent score zone by floatValue (starts at 1).</summary>
        MultiplyIndependentScore = 9,
        /// <summary>Add amount elves (negative removes).</summary>
        ModifyElfCount = 10,
        /// <summary>Acquire LinkedRelic once (e.g. 仪式 → 激励).</summary>
        GrantLinkedRelic = 11,
        /// <summary>
        /// Every intValue units of Material produced this batch → grant amount more of same.
        /// </summary>
        GrantRawPerRawProduced = 12,
        /// <summary>
        /// Grant Soft = floor(previous unused warehouse × floatValue).
        /// </summary>
        GrantSoftFromUnusedWarehousePercent = 13,
        /// <summary>
        /// With probability floatValue, grant amount of a random raw material.
        /// </summary>
        ChanceGrantRandomRaw = 14,
        /// <summary>
        /// Passive: each lost elf grants amount ghosts (EmployeeTypeId defaults to ghost).
        /// </summary>
        GrantEmployeeOnElfLoss = 15,
        /// <summary>
        /// Every intValue gathered units → grant amount of Material (raw).
        /// </summary>
        GrantRawPerGather = 16,
        /// <summary>
        /// Before spicy: add floatValue to spicy bonus score multiplier
        /// (e.g. 0.5 → 热辣加成分 ×1.5).
        /// </summary>
        AddSpicyScoreMultiplier = 17,
        /// <summary>
        /// Passive: add amount to every gather job's amount-per-worker
        /// (每种采集物产出份数 +N).
        /// </summary>
        AddGatherAmountPerWorker = 18,
        /// <summary>
        /// Passive: reduce warehouse overflow waste by floatValue (0.75 = 浪费减少75%).
        /// </summary>
        ReduceWarehouseWaste = 19,
        /// <summary>
        /// Passive: wasted ingredients convert into processed food × amount
        /// (amount ≤ 0 treated as 1; 回收器 = 2).
        /// </summary>
        ConvertWasteToEqualGain = 20,
        /// <summary>
        /// Passive: each cold-cooked unit grants +amount score
        /// (default 2/份；冰点 amount=2 → 每份 4 分).
        /// </summary>
        AddColdScorePerUnit = 21,
        /// <summary>
        /// Passive: absolute reduction to magic consume rate (0.2 = 50%→30%).
        /// </summary>
        ReduceMagicConsumePercent = 22,
        /// <summary>
        /// Passive: override sour best-tier cooked percent (intValue, e.g. 20 = 前 20%).
        /// </summary>
        OverrideSourTopTierPercent = 23,
        /// <summary>
        /// Passive: override sour second-tier cooked percent ceiling (intValue, e.g. 70 = 至 70%).
        /// </summary>
        OverrideSourSecondTierPercent = 34,
        /// <summary>
        /// Passive: add floatValue to process-station work efficiency
        /// (e.g. 0.5 = 处理岗效率 +50%).
        /// </summary>
        AddProcessLaborEfficiency = 24,
        /// <summary>
        /// OnAcquire: grant amount of EmployeeTypeId (defaults to ghost).
        /// </summary>
        GrantEmployee = 25,
        /// <summary>
        /// Passive: add floatValue to cook-station work efficiency
        /// (e.g. 0.5 = 烹饪岗效率 +50%).
        /// </summary>
        AddCookLaborEfficiency = 26,
        /// <summary>
        /// Passive: waste floatValue of cook output (0.2 = 烹饪产出浪费 20%).
        /// </summary>
        AddCookOutputWasteFraction = 27,
        /// <summary>
        /// OnAcquire: all unlocked gather jobs act as 快乐坨坨; advance path ids are kept.
        /// </summary>
        ConvertAllGatherToHappyTuotuo = 28,
        /// <summary>
        /// Passive / OnAcquire: add amount advance charges to gather, process, and cook each
        /// (施工队 amount=1 → 三区各 +1 次进阶机会).
        /// </summary>
        AddAdvanceChargesAllZones = 29,
        /// <summary>
        /// TurnEnd: convert floatValue of Tough and Solid into Soft
        /// (搅拌机 0.75 = 消耗 3/4 强韧与坚固，生成等量柔软).
        /// </summary>
        ConvertToughSolidFractionToSoft = 30,
        /// <summary>
        /// OnAcquire: present amount AfterStage events immediately (三个问号按钮 amount=3).
        /// </summary>
        PresentBonusStageEvents = 31,
        /// <summary>
        /// LevelEnd: if owning ≥ intValue elves, remove that many (not counted as loss) and
        /// grant amount of EmployeeTypeId (default ghost). 升华: intValue=3, amount=4.
        /// </summary>
        ConvertElvesToGhosts = 32,
        /// <summary>
        /// Passive: add amount to every job that has a worker cap (人满为患 amount=5).
        /// Jobs with no cap (烹饪) stay unlimited.
        /// </summary>
        AddAllJobMaxWorkers = 33
    }
}
