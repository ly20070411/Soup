using System;
using Soup.Jobs;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Runtime resource panel: raw materials, flavors, processed, cooked.
    /// Raw materials may temporarily exceed warehouse capacity during gather;
    /// TurnManager enforces the cap after process.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class ResourceStore : MonoBehaviour
    {
        public const string ResourcesConfigPath = "GameConfig";

        [SerializeField] private GameConfig config;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private int _soft;
        private int _tough;
        private int _solid;
        private int _spicy;
        private int _sour;
        private int _cold;
        private int _magic;
        private int _processed;
        private int _cooked;
        private int _warehouseCapacityBonus;

        public static ResourceStore Instance { get; private set; }

        public GameConfig Config => config;

        public int Soft => _soft;
        public int Tough => _tough;
        public int Solid => _solid;
        public int Spicy => _spicy;
        public int Sour => _sour;
        public int Cold => _cold;
        public int Magic => _magic;
        public int Processed => _processed;
        public int Cooked => _cooked;

        /// <summary>未处理食材总数（三种材质之和）。</summary>
        public int TotalRaw => _soft + _tough + _solid;

        /// <summary>关卡奖励等带来的仓库上限加成。</summary>
        public int WarehouseCapacityBonus => _warehouseCapacityBonus;

        /// <summary>仓库容量 = 配置基础值 + 运行时加成。0 = 不限（仅当基础与加成都为 0）。</summary>
        public int WarehouseCapacity
        {
            get
            {
                int baseCap = config != null ? Mathf.Max(0, config.WarehouseCapacity) : 0;
                // Bonus may be negative (e.g. event penalties).
                return Mathf.Max(0, baseCap + _warehouseCapacityBonus);
            }
        }

        /// <summary>仓库剩余空位。不限容量时返回 int.MaxValue。</summary>
        public int WarehouseSpace
        {
            get
            {
                int cap = WarehouseCapacity;
                if (cap <= 0) return int.MaxValue;
                return Mathf.Max(0, cap - TotalRaw);
            }
        }

        public event Action Changed;

        public static void Initialize(GameConfig gameConfig)
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(ResourceStore));
                Instance = go.AddComponent<ResourceStore>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(go);
            }

            Instance.config = gameConfig;
            Instance.Clear();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            if (config == null)
                config = Resources.Load<GameConfig>(ResourcesConfigPath);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Clear()
        {
            _soft = _tough = _solid = 0;
            _spicy = _sour = _cold = _magic = 0;
            _processed = _cooked = 0;
            _warehouseCapacityBonus = 0;
            RaiseChanged();
        }

        public void ApplyState(
            int soft, int tough, int solid,
            int spicy, int sour, int cold, int magic,
            int processed, int cooked,
            int warehouseCapacityBonus = 0)
        {
            _soft = Mathf.Max(0, soft);
            _tough = Mathf.Max(0, tough);
            _solid = Mathf.Max(0, solid);
            _spicy = Mathf.Max(0, spicy);
            _sour = Mathf.Max(0, sour);
            _cold = Mathf.Max(0, cold);
            _magic = Mathf.Max(0, magic);
            _processed = Mathf.Max(0, processed);
            _cooked = Mathf.Max(0, cooked);
            _warehouseCapacityBonus = warehouseCapacityBonus;
            RaiseChanged();
        }

        public void AddWarehouseCapacityBonus(int amount)
        {
            if (amount == 0) return;
            _warehouseCapacityBonus += amount;
            RaiseChanged();
        }

        public void SetWarehouseCapacityBonus(int value)
        {
            _warehouseCapacityBonus = value;
            RaiseChanged();
        }

        public int GetRaw(IngredientMaterial material)
        {
            switch (material)
            {
                case IngredientMaterial.Soft: return _soft;
                case IngredientMaterial.Tough: return _tough;
                case IngredientMaterial.Solid: return _solid;
                default: return TotalRaw;
            }
        }

        public int GetFlavor(FlavorType flavor)
        {
            switch (flavor)
            {
                case FlavorType.Spicy: return _spicy;
                case FlavorType.Sour: return _sour;
                case FlavorType.Cold: return _cold;
                case FlavorType.Magic: return _magic;
                default: return 0;
            }
        }

        /// <summary>
        /// Add raw material without warehouse capacity check.
        /// Capacity is enforced after process (see TurnManager).
        /// </summary>
        public int AddRaw(IngredientMaterial material, int amount)
        {
            if (amount <= 0) return 0;
            if (material == IngredientMaterial.Any) return 0;

            switch (material)
            {
                case IngredientMaterial.Soft: _soft += amount; break;
                case IngredientMaterial.Tough: _tough += amount; break;
                case IngredientMaterial.Solid: _solid += amount; break;
                default: return 0;
            }

            RaiseChanged();
            return amount;
        }

        /// <summary>
        /// Add raw material, clamped by warehouse capacity.
        /// Prefer <see cref="AddRaw"/> during gather; use this for direct capped inserts.
        /// </summary>
        public int TryAddRaw(IngredientMaterial material, int amount)
        {
            if (amount <= 0) return 0;
            if (material == IngredientMaterial.Any) return 0;

            int space = WarehouseSpace;
            int accepted = space == int.MaxValue ? amount : Mathf.Min(amount, space);
            if (accepted <= 0) return 0;
            return AddRaw(material, accepted);
        }

        public bool TryConsumeRaw(IngredientMaterial material, int amount)
        {
            if (amount <= 0) return true;
            if (GetRaw(material) < amount) return false;

            switch (material)
            {
                case IngredientMaterial.Soft: _soft -= amount; break;
                case IngredientMaterial.Tough: _tough -= amount; break;
                case IngredientMaterial.Solid: _solid -= amount; break;
                default: return false;
            }

            RaiseChanged();
            return true;
        }

        /// <summary>Consume up to <paramref name="amount"/> and return how many were taken.</summary>
        public int ConsumeRawUpTo(IngredientMaterial material, int amount)
        {
            if (amount <= 0) return 0;
            int take = Mathf.Min(amount, GetRaw(material));
            if (take <= 0) return 0;
            TryConsumeRaw(material, take);
            return take;
        }

        public void AddFlavor(FlavorType flavor, int amount)
        {
            if (amount == 0) return;
            SetFlavor(flavor, GetFlavor(flavor) + amount);
        }

        public bool TryConsumeFlavor(FlavorType flavor, int amount)
        {
            if (amount <= 0) return true;
            int current = GetFlavor(flavor);
            if (current < amount) return false;
            SetFlavor(flavor, current - amount);
            return true;
        }

        public int ConsumeFlavorUpTo(FlavorType flavor, int amount)
        {
            if (amount <= 0) return 0;
            int take = Mathf.Min(amount, GetFlavor(flavor));
            if (take <= 0) return 0;
            SetFlavor(flavor, GetFlavor(flavor) - take);
            return take;
        }

        public void AddProcessed(int amount)
        {
            if (amount == 0) return;
            _processed = Mathf.Max(0, _processed + amount);
            RaiseChanged();
        }

        public bool TryConsumeProcessed(int amount)
        {
            if (amount <= 0) return true;
            if (_processed < amount) return false;
            _processed -= amount;
            RaiseChanged();
            return true;
        }

        public int ConsumeProcessedUpTo(int amount)
        {
            if (amount <= 0) return 0;
            int take = Mathf.Min(amount, _processed);
            if (take <= 0) return 0;
            _processed -= take;
            RaiseChanged();
            return take;
        }

        public void AddCooked(int amount)
        {
            if (amount == 0) return;
            _cooked = Mathf.Max(0, _cooked + amount);
            RaiseChanged();
        }

        public void SetCooked(int value)
        {
            _cooked = Mathf.Max(0, value);
            RaiseChanged();
        }

        private void SetFlavor(FlavorType flavor, int value)
        {
            value = Mathf.Max(0, value);
            switch (flavor)
            {
                case FlavorType.Spicy: _spicy = value; break;
                case FlavorType.Sour: _sour = value; break;
                case FlavorType.Cold: _cold = value; break;
                case FlavorType.Magic: _magic = value; break;
                default: return;
            }

            RaiseChanged();
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
