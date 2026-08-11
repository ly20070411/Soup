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
        [SerializeField, Min(0)] private int warehouseCapacity = 100;

        [Header("热辣")]
        [Tooltip("热辣对烹饪分的倍率上限。遗物可解除上限。")]
        [SerializeField, Min(0f)] private float spicyMultiplierCap = 3f;

        public int StartingElfCount => startingElfCount;
        public int WarehouseCapacity => warehouseCapacity;
        public float SpicyMultiplierCap => spicyMultiplierCap;

        public void SetStartingElfCount(int value) => startingElfCount = Mathf.Max(0, value);

        public void SetWarehouseCapacity(int value) => warehouseCapacity = Mathf.Max(0, value);

        public void SetSpicyMultiplierCap(float value) => spicyMultiplierCap = Mathf.Max(0f, value);

#if UNITY_EDITOR
        private void OnValidate()
        {
            startingElfCount = Mathf.Max(0, startingElfCount);
            warehouseCapacity = Mathf.Max(0, warehouseCapacity);
            spicyMultiplierCap = Mathf.Max(0f, spicyMultiplierCap);
        }
#endif
    }
}
