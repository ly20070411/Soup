using System;
using Soup.Items;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// 进阶树单个节点：人口加成 + 采集数值效果 + 进阶即时奖励 + 效果文案。
    /// </summary>
    [Serializable]
    public class JobAdvanceNode
    {
        [Tooltip("节点显示名，如「扩编」；可留空，UI 会用路径标签代替。")]
        [SerializeField] private string displayName = string.Empty;

        [Tooltip("人口上限增量。采集/处理默认 +5；烹饪无人口上限时保持 0。")]
        [SerializeField, Min(0)] private int maxWorkersBonus = 5;

        [Tooltip("绝对人口上限；>0 时覆盖「基础+累加」，用于「岗位上限永久为 N」。沿路径取最深非零值。")]
        [SerializeField, Min(0)] private int maxWorkersOverride;

        [Tooltip("覆盖每精灵产出份数；0 = 不覆盖（沿用岗位基础值或上级覆盖）。")]
        [SerializeField, Min(0)] private int gatherAmountOverride;

        [Tooltip("每份采集物额外柔软食材（可为负）。沿路径累加。")]
        [SerializeField] private int softPerUnitBonus;

        [Tooltip("每份采集物额外坚固食材（可为负）。沿路径累加。")]
        [SerializeField] private int solidPerUnitBonus;

        [Tooltip("每份采集物额外强韧食材（可为负）。沿路径累加。")]
        [SerializeField] private int toughPerUnitBonus;

        [Tooltip("每份采集物额外寒冷（可为负）。沿路径累加。")]
        [SerializeField] private int coldPerUnitBonus;

        [Tooltip("每份采集物额外热辣（可为负）。沿路径累加。")]
        [SerializeField] private int spicyPerUnitBonus;

        [Tooltip("每份采集物额外酸涩（可为负）。沿路径累加。")]
        [SerializeField] private int sourPerUnitBonus;

        [Tooltip("每份采集物额外鲜美（可为负）。沿路径累加。")]
        [SerializeField] private int magicPerUnitBonus;

        [Tooltip("每份采集物额外随机风味。沿路径累加。")]
        [SerializeField, Min(0)] private int randomFlavorPerUnitBonus;

        [Tooltip("寒冷分数额外倍率（如 0.5 表示寒冷分 ×1.5）。沿路径累加。")]
        [SerializeField, Min(0f)] private float coldScoreMultiplierBonus;

        [Tooltip("热辣分数额外倍率（如 0.5 表示热辣贡献分 ×1.5）。沿路径累加。")]
        [SerializeField, Min(0f)] private float spicyScoreMultiplierBonus;

        [Tooltip("酸涩分数额外倍率（如 0.5 表示酸涩分 ×1.5）。沿路径累加。")]
        [SerializeField, Min(0f)] private float sourScoreMultiplierBonus;

        [Tooltip("鲜美分数额外倍率（如 0.5 表示鲜美分 ×1.5）。沿路径累加。")]
        [SerializeField, Min(0f)] private float magicScoreMultiplierBonus;

        [Tooltip("风味总分额外倍率（如 0.2 表示各风味分再 ×1.2）。沿路径累加。")]
        [SerializeField, Min(0f)] private float flavorScoreMultiplierBonus;

        [Tooltip("其他采集岗每份产出额外寒冷。沿路径累加。")]
        [SerializeField, Min(0)] private int otherGatherColdPerUnit;

        [Tooltip("其他采集岗每份产出额外热辣。沿路径累加。")]
        [SerializeField, Min(0)] private int otherGatherSpicyPerUnit;

        [Tooltip("其他采集岗每份产出额外酸涩。沿路径累加。")]
        [SerializeField, Min(0)] private int otherGatherSourPerUnit;

        [Tooltip("其他采集岗每份产出额外鲜美。沿路径累加。")]
        [SerializeField, Min(0)] private int otherGatherMagicPerUnit;

        [Tooltip("其他采集岗每份产出额外随机风味。沿路径累加。")]
        [SerializeField, Min(0)] private int otherGatherRandomFlavorPerUnit;

        [Tooltip("每个在岗员工提供的采集效率加成（如 0.08 = +8%）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float efficiencyPerWorker;

        [Tooltip("岗位满员时，每份采集物额外柔软。沿路径累加。")]
        [SerializeField, Min(0)] private int softPerUnitWhenFull;

        [Tooltip("本岗每次采集结算额外柔软（固定份数，不随产出份数倍增）。沿路径累加。")]
        [SerializeField] private int flatSoftBonus;

        [Tooltip("本岗每次采集结算额外强韧（固定份数，不随产出份数倍增）。沿路径累加。")]
        [SerializeField] private int flatToughBonus;

        [Tooltip("本岗每次采集结算额外坚固（固定份数，不随产出份数倍增）。沿路径累加。")]
        [SerializeField] private int flatSolidBonus;

        [Tooltip("关卡通关领取小精灵时，永久额外数量。沿路径累加。")]
        [SerializeField, Min(0)] private int permanentElfBonus;

        [Tooltip("本岗每名在岗员工为所有采集岗提供的效率（如 0.08）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float allGatherEfficiencyPerWorker;

        [Tooltip("全采集效率光环上限（如 0.4 = 40%）。与 allGatherEfficiencyPerWorker 成对。")]
        [SerializeField, Min(0f)] private float allGatherEfficiencyCap;

        [Tooltip("本岗每名在岗员工为指定采集岗提供的效率。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float designatedGatherEfficiencyPerWorker;

        [Tooltip("指定采集岗效率光环上限（如 1 = 100%）。")]
        [SerializeField, Min(0f)] private float designatedGatherEfficiencyCap;

        [Tooltip("本岗每名在岗员工为所有处理岗提供的效率（如 0.1）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float allProcessEfficiencyPerWorker;

        [Tooltip("全处理效率光环上限（如 0.5 = 50%）。与 allProcessEfficiencyPerWorker 成对。")]
        [SerializeField, Min(0f)] private float allProcessEfficiencyCap;

        [Tooltip("本岗每名在岗员工为所有烹饪岗提供的效率（如 0.1）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float allCookEfficiencyPerWorker;

        [Tooltip("全烹饪效率光环上限（如 0.5 = 50%）。与 allCookEfficiencyPerWorker 成对。")]
        [SerializeField, Min(0f)] private float allCookEfficiencyCap;

        [Tooltip("其他采集岗产量惩罚比例（如 0.25 = 减产 25%）。沿路径取最深非零值。")]
        [SerializeField, Range(0f, 1f)] private float otherGatherOutputPenalty;

        [Tooltip("烹饪结束后浪费仓库未处理食材的比例（如 0.25）。沿路径取最深非零值。")]
        [SerializeField, Range(0f, 1f)] private float endTurnRawWasteFraction;

        [Tooltip("每获得 1 名员工时额外获得的激励层数。沿路径累加。")]
        [SerializeField, Min(0)] private int incentivePerEmployeeGained;

        [Tooltip("采集结算时额外产出「当前最多」风味份数。沿路径取最深非零值。")]
        [SerializeField, Min(0)] private int topFlavorBonus;

        [Tooltip("多种风味并列最多时改用的产出份数。0 表示与 topFlavorBonus 相同。")]
        [SerializeField, Min(0)] private int topFlavorTieBonus;

        [Tooltip("采集结算时额外产出「当前最多」食材材质份数。沿路径累加。")]
        [SerializeField, Min(0)] private int topMaterialBonus;

        [Tooltip("每精灵产出倍率（如 3 = 三倍）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float gatherAmountMultiplier;

        [Tooltip("选中本节点时随机摧毁另一个已解锁采集岗。")]
        [SerializeField] private bool destroyOtherGatherOnTake;

        [Tooltip("每个已被摧毁的采集岗每回合额外产出的本岗食材份数（如小白花 10）。沿路径取最深非零值。")]
        [SerializeField, Min(0)] private int destroyedJobsOutputPerTurn;

        [Tooltip("采集时按概率替换为基础产出物的变体食材（如变异蘑菇）。沿路径取最深有效项。")]
        [SerializeField] private IngredientItem variantIngredient;

        [Tooltip("每份产出替换为变体的概率（0~1）。")]
        [SerializeField, Range(0f, 1f)] private float variantChance;

        [Tooltip("每次采集结算额外产出的食材（如大团球）。沿路径取最深有效项。")]
        [SerializeField] private IngredientItem bonusIngredient;

        [Tooltip("额外产出食材的固定份数（不随本岗产出份数倍增）。")]
        [SerializeField, Min(0)] private int bonusIngredientAmount;

        [Tooltip("每份采集物尝试消耗的柔软或坚固数量（二选一，不足则本份不触发转化）。沿路径取最深非零包。")]
        [SerializeField, Min(0)] private int convertConsumeSoftOrSolidPerUnit;

        [Tooltip("转化成功时每份额外获得的强韧。")]
        [SerializeField, Min(0)] private int convertGainToughPerUnit;

        [Tooltip("转化成功时每份额外获得的「当前最多」风味。")]
        [SerializeField, Min(0)] private int convertGainTopFlavorPerUnit;

        [Tooltip("强制将本岗采集物的全部食材产出转为单一材质。")]
        [SerializeField] private bool forceOutputMaterial;

        [Tooltip("强制产出的材质（需 forceOutputMaterial）。")]
        [SerializeField] private IngredientMaterial forcedOutputMaterial = IngredientMaterial.Tough;

        [Tooltip("每次采集结算额外产出的随机食材份数。沿路径取最深非零值。")]
        [SerializeField, Min(0)] private int flatRandomMaterialBonus;

        [Tooltip("采集效率固定减成（1 = 减 100%）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float gatherEfficiencyFlatPenalty;

        [Tooltip("用在岗员工抵消效率减成：每员加成。与 flatPenalty 成对。")]
        [SerializeField, Min(0f)] private float gatherEfficiencyRecoverPerWorker;

        [Tooltip("员工抵消效率减成的上限（1 = 最多加回 100%）。")]
        [SerializeField, Min(0f)] private float gatherEfficiencyRecoverCap;

        [Tooltip("本岗本回合有产出后，下一回合施加的效率减成（1 = 减 100%）。")]
        [SerializeField, Min(0f)] private float nextTurnGatherEfficiencyPenalty;

        [Tooltip("每名在岗员工使「激励」遗物效果放大的比例（0.05 = +5%）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float incentiveEffectAmplifyPerWorker;

        [Tooltip("每名在岗员工使「疲惫」遗物效果减弱的比例（0.1 = -10%）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float fatigueEffectReducePerWorker;

        [Tooltip("每名在岗员工提供的全局工作效率（如 0.025）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float allGlobalLaborEfficiencyPerWorker;

        [Tooltip("全局工作效率光环上限（如 0.6 = 60%）。0 = 不限制。")]
        [SerializeField, Min(0f)] private float allGlobalLaborEfficiencyCap;

        [Tooltip("回合结束时，每名在岗员工产出激励的概率。沿路径取最深非零值。")]
        [SerializeField, Range(0f, 1f)] private float endTurnIncentiveChancePerWorker;

        [Tooltip("上述激励产出每关上限。与 endTurnIncentiveChancePerWorker 成对。")]
        [SerializeField, Min(0)] private int endTurnIncentiveMaxPerLevel;

        [Tooltip("采集时不产出柔软/强韧/坚固/随机食材。")]
        [SerializeField] private bool suppressRawMaterialOutput;

        [Tooltip("本岗每次采集结算额外寒冷（固定份数）。沿路径累加。")]
        [SerializeField] private int flatColdBonus;

        [Tooltip("本岗每次采集结算额外热辣（固定份数）。沿路径累加。")]
        [SerializeField] private int flatSpicyBonus;

        [Tooltip("指定采集岗与自己的风味产量加成（0.5 = +50%）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float designatedPairFlavorYieldBonus;

        [Tooltip("指定采集岗与自己的全部产量加成（0.5 = +50%）。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float designatedPairAllYieldBonus;

        [Tooltip("未使用仓库每多少份触发一次额外坚固（如 300）。沿路径取最深非零包；与仓库容量坚固互斥。")]
        [SerializeField, Min(0)] private int solidPerUnusedWarehouseThreshold;

        [Tooltip("每次未使用仓库阈值触发额外产出的坚固份数。")]
        [SerializeField, Min(0)] private int solidPerUnusedWarehouseAmount;

        [Tooltip("仓库容量每多少份触发一次额外坚固（如 300）。沿路径取最深非零包；与未使用仓库坚固互斥。")]
        [SerializeField, Min(0)] private int solidPerWarehouseCapacityThreshold;

        [Tooltip("每次仓库容量阈值触发额外产出的坚固份数。")]
        [SerializeField, Min(0)] private int solidPerWarehouseCapacityAmount;

        [Tooltip("仓库中坚固存量每多少份触发一次额外坚固（如 200）。沿路径取最深非零包。")]
        [SerializeField, Min(0)] private int solidPerWarehouseSolidThreshold;

        [Tooltip("每次仓库坚固存量阈值触发额外产出的坚固份数。")]
        [SerializeField, Min(0)] private int solidPerWarehouseSolidAmount;

        [Tooltip("处理食材每多少份额外增加采集份数（如 200）。沿路径取最深非零包。")]
        [SerializeField, Min(0)] private int gatherUnitsPerProcessedThreshold;

        [Tooltip("每次处理食材阈值额外增加的采集份数。")]
        [SerializeField, Min(0)] private int gatherUnitsPerProcessedAmount;

        [Tooltip("覆盖每精灵处理量；0 = 不覆盖。沿路径取最深非零值。")]
        [SerializeField, Min(0)] private int processAmountOverride;

        [Tooltip("覆盖每精灵烹饪量；0 = 不覆盖。沿路径取最深非零值。")]
        [SerializeField, Min(0)] private int cookAmountOverride;

        [Tooltip("覆盖烹饪分数倍率；0 = 不覆盖。沿路径取最深非零值。")]
        [SerializeField, Min(0f)] private float scoreMultiplierOverride;

        [Tooltip("覆盖其他材质处理效率（如 0.25）；0 = 不覆盖。沿路径取最深非零值。")]
        [SerializeField, Range(0f, 1f)] private float otherMaterialEfficiencyOverride;

        [Tooltip("处理返还材质（柔软/强韧/坚固）。与阈值、份数成包，沿路径取最深非零包。")]
        [SerializeField] private IngredientMaterial materialRefundMaterial = IngredientMaterial.Soft;

        [Tooltip("每处理多少份任意食材触发一次材质返还（如 10）。沿路径取最深非零包。")]
        [SerializeField, Min(0)] private int materialRefundPerProcessedThreshold;

        [Tooltip("每次处理阈值返还的食材份数。")]
        [SerializeField, Min(0)] private int materialRefundPerProcessedAmount;

        [Tooltip("每处理多少份任意食材触发一次处理食材返还（如 10）。沿路径取最深非零包。")]
        [SerializeField, Min(0)] private int processedRefundPerProcessedThreshold;

        [Tooltip("每次处理阈值返还的处理食材份数。")]
        [SerializeField, Min(0)] private int processedRefundPerProcessedAmount;

        [Tooltip("本岗处理产出损耗比例（0.1 = 损耗 10% 处理食材）。沿路径取最深非零值。")]
        [SerializeField, Range(0f, 1f)] private float processedOutputWasteFraction;

        [Tooltip("选中本节点时一次性发放的员工类型 Id（如 mushroom_person）。")]
        [SerializeField] private string grantEmployeeId = string.Empty;

        [Tooltip("选中本节点时一次性发放的员工数量。")]
        [SerializeField, Min(0)] private int grantEmployeeCount;

        [Tooltip("该节点效果说明（展示用）。")]
        [TextArea(2, 4)]
        [SerializeField] private string effectDescription = string.Empty;

        public string DisplayName => displayName ?? string.Empty;
        public int MaxWorkersBonus => maxWorkersBonus;
        public int MaxWorkersOverride => maxWorkersOverride;
        public int GatherAmountOverride => gatherAmountOverride;
        public int SoftPerUnitBonus => softPerUnitBonus;
        public int SolidPerUnitBonus => solidPerUnitBonus;
        public int ToughPerUnitBonus => toughPerUnitBonus;
        public int ColdPerUnitBonus => coldPerUnitBonus;
        public int SpicyPerUnitBonus => spicyPerUnitBonus;
        public int SourPerUnitBonus => sourPerUnitBonus;
        public int MagicPerUnitBonus => magicPerUnitBonus;
        public int RandomFlavorPerUnitBonus => randomFlavorPerUnitBonus;
        public float ColdScoreMultiplierBonus => coldScoreMultiplierBonus;
        public float SpicyScoreMultiplierBonus => spicyScoreMultiplierBonus;
        public float SourScoreMultiplierBonus => sourScoreMultiplierBonus;
        public float MagicScoreMultiplierBonus => magicScoreMultiplierBonus;
        public float FlavorScoreMultiplierBonus => flavorScoreMultiplierBonus;
        public int OtherGatherColdPerUnit => otherGatherColdPerUnit;
        public int OtherGatherSpicyPerUnit => otherGatherSpicyPerUnit;
        public int OtherGatherSourPerUnit => otherGatherSourPerUnit;
        public int OtherGatherMagicPerUnit => otherGatherMagicPerUnit;
        public int OtherGatherRandomFlavorPerUnit => otherGatherRandomFlavorPerUnit;
        public float EfficiencyPerWorker => efficiencyPerWorker;
        public int SoftPerUnitWhenFull => softPerUnitWhenFull;
        public int FlatSoftBonus => flatSoftBonus;
        public int FlatToughBonus => flatToughBonus;
        public int FlatSolidBonus => flatSolidBonus;
        public int PermanentElfBonus => permanentElfBonus;
        public float AllGatherEfficiencyPerWorker => allGatherEfficiencyPerWorker;
        public float AllGatherEfficiencyCap => allGatherEfficiencyCap;
        public float DesignatedGatherEfficiencyPerWorker => designatedGatherEfficiencyPerWorker;
        public float DesignatedGatherEfficiencyCap => designatedGatherEfficiencyCap;
        public float AllProcessEfficiencyPerWorker => allProcessEfficiencyPerWorker;
        public float AllProcessEfficiencyCap => allProcessEfficiencyCap;
        public float AllCookEfficiencyPerWorker => allCookEfficiencyPerWorker;
        public float AllCookEfficiencyCap => allCookEfficiencyCap;
        public float OtherGatherOutputPenalty => otherGatherOutputPenalty;
        public float EndTurnRawWasteFraction => endTurnRawWasteFraction;
        public int IncentivePerEmployeeGained => incentivePerEmployeeGained;
        public int TopFlavorBonus => topFlavorBonus;
        public int TopFlavorTieBonus => topFlavorTieBonus;
        public int TopMaterialBonus => topMaterialBonus;
        public float GatherAmountMultiplier => gatherAmountMultiplier;
        public bool DestroyOtherGatherOnTake => destroyOtherGatherOnTake;
        public int DestroyedJobsOutputPerTurn => destroyedJobsOutputPerTurn;
        public IngredientItem VariantIngredient => variantIngredient;
        public float VariantChance => variantChance;
        public IngredientItem BonusIngredient => bonusIngredient;
        public int BonusIngredientAmount => bonusIngredientAmount;
        public int ConvertConsumeSoftOrSolidPerUnit => convertConsumeSoftOrSolidPerUnit;
        public int ConvertGainToughPerUnit => convertGainToughPerUnit;
        public int ConvertGainTopFlavorPerUnit => convertGainTopFlavorPerUnit;
        public bool ForceOutputMaterial => forceOutputMaterial;
        public IngredientMaterial ForcedOutputMaterial => forcedOutputMaterial;
        public int FlatRandomMaterialBonus => flatRandomMaterialBonus;
        public float GatherEfficiencyFlatPenalty => gatherEfficiencyFlatPenalty;
        public float GatherEfficiencyRecoverPerWorker => gatherEfficiencyRecoverPerWorker;
        public float GatherEfficiencyRecoverCap => gatherEfficiencyRecoverCap;
        public float NextTurnGatherEfficiencyPenalty => nextTurnGatherEfficiencyPenalty;
        public float IncentiveEffectAmplifyPerWorker => incentiveEffectAmplifyPerWorker;
        public float FatigueEffectReducePerWorker => fatigueEffectReducePerWorker;
        public float AllGlobalLaborEfficiencyPerWorker => allGlobalLaborEfficiencyPerWorker;
        public float AllGlobalLaborEfficiencyCap => allGlobalLaborEfficiencyCap;
        public float EndTurnIncentiveChancePerWorker => endTurnIncentiveChancePerWorker;
        public int EndTurnIncentiveMaxPerLevel => endTurnIncentiveMaxPerLevel;
        public bool SuppressRawMaterialOutput => suppressRawMaterialOutput;
        public int FlatColdBonus => flatColdBonus;
        public int FlatSpicyBonus => flatSpicyBonus;
        public float DesignatedPairFlavorYieldBonus => designatedPairFlavorYieldBonus;
        public float DesignatedPairAllYieldBonus => designatedPairAllYieldBonus;
        public int SolidPerUnusedWarehouseThreshold => solidPerUnusedWarehouseThreshold;
        public int SolidPerUnusedWarehouseAmount => solidPerUnusedWarehouseAmount;
        public int SolidPerWarehouseCapacityThreshold => solidPerWarehouseCapacityThreshold;
        public int SolidPerWarehouseCapacityAmount => solidPerWarehouseCapacityAmount;
        public int SolidPerWarehouseSolidThreshold => solidPerWarehouseSolidThreshold;
        public int SolidPerWarehouseSolidAmount => solidPerWarehouseSolidAmount;
        public int GatherUnitsPerProcessedThreshold => gatherUnitsPerProcessedThreshold;
        public int GatherUnitsPerProcessedAmount => gatherUnitsPerProcessedAmount;
        public int ProcessAmountOverride => processAmountOverride;
        public int CookAmountOverride => cookAmountOverride;
        public float ScoreMultiplierOverride => scoreMultiplierOverride;
        public float OtherMaterialEfficiencyOverride => otherMaterialEfficiencyOverride;
        public IngredientMaterial MaterialRefundMaterial => materialRefundMaterial;
        public int MaterialRefundPerProcessedThreshold => materialRefundPerProcessedThreshold;
        public int MaterialRefundPerProcessedAmount => materialRefundPerProcessedAmount;
        public int ProcessedRefundPerProcessedThreshold => processedRefundPerProcessedThreshold;
        public int ProcessedRefundPerProcessedAmount => processedRefundPerProcessedAmount;
        public float ProcessedOutputWasteFraction => processedOutputWasteFraction;
        public string GrantEmployeeId => grantEmployeeId ?? string.Empty;
        public int GrantEmployeeCount => grantEmployeeCount;
        public string EffectDescription => effectDescription ?? string.Empty;

        /// <summary>占位分支「无」：不可被选为进阶目标。</summary>
        public bool IsNoneAdvanceNode()
        {
            return IsNoneLabel(displayName) || IsNoneLabel(effectDescription);
        }

        private static bool IsNoneLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return text.Trim() == "无";
        }

        public bool NeedsDesignatedGatherTarget =>
            designatedGatherEfficiencyPerWorker > 0f
            || designatedPairFlavorYieldBonus > 0f
            || designatedPairAllYieldBonus > 0f;

        public void SetDisplayName(string value) => displayName = value ?? string.Empty;

        public void SetMaxWorkersBonus(int value) => maxWorkersBonus = Mathf.Max(0, value);

        public void SetMaxWorkersOverride(int value) => maxWorkersOverride = Mathf.Max(0, value);

        public void SetGatherAmountOverride(int value) => gatherAmountOverride = Mathf.Max(0, value);

        public void SetSoftPerUnitBonus(int value) => softPerUnitBonus = value;

        public void SetSolidPerUnitBonus(int value) => solidPerUnitBonus = value;

        public void SetToughPerUnitBonus(int value) => toughPerUnitBonus = value;

        public void SetColdPerUnitBonus(int value) => coldPerUnitBonus = value;

        public void SetSpicyPerUnitBonus(int value) => spicyPerUnitBonus = value;

        public void SetSourPerUnitBonus(int value) => sourPerUnitBonus = value;

        public void SetMagicPerUnitBonus(int value) => magicPerUnitBonus = value;

        public void SetRandomFlavorPerUnitBonus(int value) =>
            randomFlavorPerUnitBonus = Mathf.Max(0, value);

        public void SetColdScoreMultiplierBonus(float value) =>
            coldScoreMultiplierBonus = Mathf.Max(0f, value);

        public void SetSpicyScoreMultiplierBonus(float value) =>
            spicyScoreMultiplierBonus = Mathf.Max(0f, value);

        public void SetSourScoreMultiplierBonus(float value) =>
            sourScoreMultiplierBonus = Mathf.Max(0f, value);

        public void SetMagicScoreMultiplierBonus(float value) =>
            magicScoreMultiplierBonus = Mathf.Max(0f, value);

        public void SetFlavorScoreMultiplierBonus(float value) =>
            flavorScoreMultiplierBonus = Mathf.Max(0f, value);

        public void SetOtherGatherColdPerUnit(int value) =>
            otherGatherColdPerUnit = Mathf.Max(0, value);

        public void SetOtherGatherSpicyPerUnit(int value) =>
            otherGatherSpicyPerUnit = Mathf.Max(0, value);

        public void SetOtherGatherSourPerUnit(int value) =>
            otherGatherSourPerUnit = Mathf.Max(0, value);

        public void SetOtherGatherMagicPerUnit(int value) =>
            otherGatherMagicPerUnit = Mathf.Max(0, value);

        public void SetOtherGatherRandomFlavorPerUnit(int value) =>
            otherGatherRandomFlavorPerUnit = Mathf.Max(0, value);

        public void SetEfficiencyPerWorker(float value) => efficiencyPerWorker = Mathf.Max(0f, value);

        public void SetSoftPerUnitWhenFull(int value) => softPerUnitWhenFull = Mathf.Max(0, value);

        public void SetFlatSoftBonus(int value) => flatSoftBonus = value;

        public void SetFlatToughBonus(int value) => flatToughBonus = value;

        public void SetFlatSolidBonus(int value) => flatSolidBonus = value;

        public void SetPermanentElfBonus(int value) => permanentElfBonus = Mathf.Max(0, value);

        public void SetAllGatherEfficiency(float perWorker, float cap)
        {
            allGatherEfficiencyPerWorker = Mathf.Max(0f, perWorker);
            allGatherEfficiencyCap = Mathf.Max(0f, cap);
        }

        public void SetDesignatedGatherEfficiency(float perWorker, float cap)
        {
            designatedGatherEfficiencyPerWorker = Mathf.Max(0f, perWorker);
            designatedGatherEfficiencyCap = Mathf.Max(0f, cap);
        }

        public void SetAllProcessEfficiency(float perWorker, float cap)
        {
            allProcessEfficiencyPerWorker = Mathf.Max(0f, perWorker);
            allProcessEfficiencyCap = Mathf.Max(0f, cap);
        }

        public void SetAllCookEfficiency(float perWorker, float cap)
        {
            allCookEfficiencyPerWorker = Mathf.Max(0f, perWorker);
            allCookEfficiencyCap = Mathf.Max(0f, cap);
        }

        public void SetOtherGatherOutputPenalty(float value) =>
            otherGatherOutputPenalty = Mathf.Clamp01(value);

        public void SetEndTurnRawWasteFraction(float value) =>
            endTurnRawWasteFraction = Mathf.Clamp01(value);

        public void SetIncentivePerEmployeeGained(int value) =>
            incentivePerEmployeeGained = Mathf.Max(0, value);

        public void SetTopFlavorBonus(int value) => SetTopFlavorBonus(value, 0);

        public void SetTopFlavorBonus(int value, int tieValue)
        {
            topFlavorBonus = Mathf.Max(0, value);
            topFlavorTieBonus = Mathf.Max(0, tieValue);
        }

        public void SetTopMaterialBonus(int value) => topMaterialBonus = Mathf.Max(0, value);

        public void SetGatherAmountMultiplier(float value) =>
            gatherAmountMultiplier = Mathf.Max(0f, value);

        public void SetDestroyOtherGatherOnTake(bool value) => destroyOtherGatherOnTake = value;

        public void SetDestroyedJobsOutputPerTurn(int value) =>
            destroyedJobsOutputPerTurn = Mathf.Max(0, value);

        public void SetVariant(IngredientItem ingredient, float chance)
        {
            variantIngredient = ingredient;
            variantChance = Mathf.Clamp01(chance);
        }

        public void SetBonusIngredient(IngredientItem ingredient, int amount)
        {
            bonusIngredient = ingredient;
            bonusIngredientAmount = Mathf.Max(0, amount);
        }

        public void SetConvertSoftOrSolidToTough(int consumePerUnit, int gainToughPerUnit, int gainTopFlavorPerUnit = 0)
        {
            convertConsumeSoftOrSolidPerUnit = Mathf.Max(0, consumePerUnit);
            convertGainToughPerUnit = Mathf.Max(0, gainToughPerUnit);
            convertGainTopFlavorPerUnit = Mathf.Max(0, gainTopFlavorPerUnit);
        }

        public void SetForceOutputMaterial(IngredientMaterial material)
        {
            forceOutputMaterial = true;
            forcedOutputMaterial = material == IngredientMaterial.Any
                ? IngredientMaterial.Tough
                : material;
        }

        public void SetFlatRandomMaterialBonus(int value) =>
            flatRandomMaterialBonus = Mathf.Max(0, value);

        public void SetGatherEfficiencyDebt(float flatPenalty, float recoverPerWorker, float recoverCap)
        {
            gatherEfficiencyFlatPenalty = Mathf.Max(0f, flatPenalty);
            gatherEfficiencyRecoverPerWorker = Mathf.Max(0f, recoverPerWorker);
            gatherEfficiencyRecoverCap = Mathf.Max(0f, recoverCap);
        }

        public void SetNextTurnGatherEfficiencyPenalty(float value) =>
            nextTurnGatherEfficiencyPenalty = Mathf.Max(0f, value);

        public void SetIncentiveFatigueAura(float incentiveAmplifyPerWorker, float fatigueReducePerWorker)
        {
            incentiveEffectAmplifyPerWorker = Mathf.Max(0f, incentiveAmplifyPerWorker);
            fatigueEffectReducePerWorker = Mathf.Max(0f, fatigueReducePerWorker);
        }

        public void SetAllGlobalLaborEfficiency(float perWorker, float cap = 0f)
        {
            allGlobalLaborEfficiencyPerWorker = Mathf.Max(0f, perWorker);
            allGlobalLaborEfficiencyCap = Mathf.Max(0f, cap);
        }

        public void SetEndTurnIncentiveChance(float chancePerWorker, int maxPerLevel)
        {
            endTurnIncentiveChancePerWorker = Mathf.Clamp01(chancePerWorker);
            endTurnIncentiveMaxPerLevel = Mathf.Max(0, maxPerLevel);
        }

        public void SetSuppressRawMaterialOutput(bool value) => suppressRawMaterialOutput = value;

        public void SetFlatColdBonus(int value) => flatColdBonus = value;

        public void SetFlatSpicyBonus(int value) => flatSpicyBonus = value;

        public void SetDesignatedPairFlavorYieldBonus(float value) =>
            designatedPairFlavorYieldBonus = Mathf.Max(0f, value);

        public void SetDesignatedPairAllYieldBonus(float value) =>
            designatedPairAllYieldBonus = Mathf.Max(0f, value);

        public void SetSolidPerUnusedWarehouse(int threshold, int amount)
        {
            solidPerUnusedWarehouseThreshold = Mathf.Max(0, threshold);
            solidPerUnusedWarehouseAmount = Mathf.Max(0, amount);
            solidPerWarehouseCapacityThreshold = 0;
            solidPerWarehouseCapacityAmount = 0;
        }

        public void SetSolidPerWarehouseCapacity(int threshold, int amount)
        {
            solidPerWarehouseCapacityThreshold = Mathf.Max(0, threshold);
            solidPerWarehouseCapacityAmount = Mathf.Max(0, amount);
            solidPerUnusedWarehouseThreshold = 0;
            solidPerUnusedWarehouseAmount = 0;
        }

        public void SetSolidPerWarehouseSolid(int threshold, int amount)
        {
            solidPerWarehouseSolidThreshold = Mathf.Max(0, threshold);
            solidPerWarehouseSolidAmount = Mathf.Max(0, amount);
        }

        public void SetGatherUnitsPerProcessed(int threshold, int amount)
        {
            gatherUnitsPerProcessedThreshold = Mathf.Max(0, threshold);
            gatherUnitsPerProcessedAmount = Mathf.Max(0, amount);
        }

        public void SetProcessAmountOverride(int value) =>
            processAmountOverride = Mathf.Max(0, value);

        public void SetCookAmountOverride(int value) =>
            cookAmountOverride = Mathf.Max(0, value);

        public void SetScoreMultiplierOverride(float value) =>
            scoreMultiplierOverride = Mathf.Max(0f, value);

        public void SetOtherMaterialEfficiencyOverride(float value) =>
            otherMaterialEfficiencyOverride = Mathf.Clamp01(value);

        public void SetMaterialRefundPerProcessed(IngredientMaterial material, int threshold, int amount)
        {
            materialRefundMaterial = material == IngredientMaterial.Any ? IngredientMaterial.Soft : material;
            materialRefundPerProcessedThreshold = Mathf.Max(0, threshold);
            materialRefundPerProcessedAmount = Mathf.Max(0, amount);
        }

        public void SetProcessedRefundPerProcessed(int threshold, int amount)
        {
            processedRefundPerProcessedThreshold = Mathf.Max(0, threshold);
            processedRefundPerProcessedAmount = Mathf.Max(0, amount);
        }

        /// <summary>兼容旧调用：返还柔软。</summary>
        public void SetSoftRefundPerProcessed(int threshold, int amount) =>
            SetMaterialRefundPerProcessed(IngredientMaterial.Soft, threshold, amount);

        public void SetProcessedOutputWasteFraction(float value) =>
            processedOutputWasteFraction = Mathf.Clamp01(value);

        public void SetGrantEmployee(string employeeId, int count)
        {
            grantEmployeeId = employeeId ?? string.Empty;
            grantEmployeeCount = Mathf.Max(0, count);
        }

        public void SetEffectDescription(string value) => effectDescription = value ?? string.Empty;

        public string ToSummary(JobAdvanceNodeId id)
        {
            string path = JobAdvancePath.ToLabel(id);
            string title = string.IsNullOrWhiteSpace(displayName) ? $"路径 {path}" : displayName.Trim();
            string pop = maxWorkersOverride > 0
                ? $"人口上限={maxWorkersOverride}"
                : (maxWorkersBonus > 0 ? $"+{maxWorkersBonus} 人口" : "无人口加成");
            if (string.IsNullOrWhiteSpace(effectDescription))
                return $"[{path}] {title}：{pop}";
            return $"[{path}] {title}：{pop}；{effectDescription.Trim()}";
        }

        public string ToShortLabel(JobAdvanceNodeId id)
        {
            string path = JobAdvancePath.ToLabel(id);
            if (!string.IsNullOrWhiteSpace(displayName))
                return $"{path} {displayName.Trim()}";
            return path;
        }
    }
}
