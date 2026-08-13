using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Tunable game constants, including starting elf count.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Soup/Game/Game Config", order = 0)]
    public class GameConfig : ScriptableObject
    {
        [Header("小精灵")]
        [Tooltip("开局拥有的小精灵总数。")]
        [SerializeField, Min(0)] private int startingElfCount = 10;

        [Header("仓库")]
        [Tooltip("未处理食材仓库上限。0 = 不限。")]
        [SerializeField, Min(0)] private int warehouseCapacity = 2000;

        [Header("热辣")]
        [Tooltip("热辣对烹饪分的倍率上限。遗物可解除上限。")]
        [SerializeField, Min(0f)] private float spicyMultiplierCap = 3f;

        [Header("事件")]
        [Tooltip("关卡通关后是否弹出事件（每关固定抽取数量）。")]
        [SerializeField] private bool enableStageEndEvents = true;
        [Tooltip("每过一关抽取的事件数量（至多一个进阶专属）。")]
        [SerializeField, Min(0)] private int stageEndEventCount = 2;
        [Tooltip("回合结束后是否按概率随机弹出事件（旧逻辑，默认关闭）。")]
        [SerializeField] private bool enableTurnEndEvents = false;
        [Tooltip("回合结束时触发随机事件的概率（0~1）。")]
        [SerializeField, Range(0f, 1f)] private float turnEndEventChance = 0.45f;
        [Tooltip("一定回合数内最多出现一次事件：两次随机事件至少间隔这么多回合。1 = 每回合都可判定。")]
        [SerializeField, Min(1)] private int eventCooldownTurns = 3;

        public int StartingElfCount => startingElfCount;
        public int WarehouseCapacity => warehouseCapacity;
        public float SpicyMultiplierCap => spicyMultiplierCap;
        public bool EnableStageEndEvents => enableStageEndEvents;
        public int StageEndEventCount => stageEndEventCount;
        public bool EnableTurnEndEvents => enableTurnEndEvents;
        public float TurnEndEventChance => turnEndEventChance;
        public int EventCooldownTurns => eventCooldownTurns;

        public void SetStartingElfCount(int value) => startingElfCount = Mathf.Max(0, value);

        public void SetWarehouseCapacity(int value) => warehouseCapacity = Mathf.Max(0, value);

        public void SetSpicyMultiplierCap(float value) => spicyMultiplierCap = Mathf.Max(0f, value);

        public void SetEnableStageEndEvents(bool value) => enableStageEndEvents = value;

        public void SetStageEndEventCount(int value) => stageEndEventCount = Mathf.Max(0, value);

        public void SetEnableTurnEndEvents(bool value) => enableTurnEndEvents = value;

        public void SetTurnEndEventChance(float value) => turnEndEventChance = Mathf.Clamp01(value);

        public void SetEventCooldownTurns(int value) => eventCooldownTurns = Mathf.Max(1, value);

#if UNITY_EDITOR
        private void OnValidate()
        {
            startingElfCount = Mathf.Max(0, startingElfCount);
            warehouseCapacity = Mathf.Max(0, warehouseCapacity);
            spicyMultiplierCap = Mathf.Max(0f, spicyMultiplierCap);
            stageEndEventCount = Mathf.Max(0, stageEndEventCount);
            turnEndEventChance = Mathf.Clamp01(turnEndEventChance);
            eventCooldownTurns = Mathf.Max(1, eventCooldownTurns);
        }
#endif
    }
}
