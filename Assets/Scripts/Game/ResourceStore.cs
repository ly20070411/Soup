using System;
using Soup.Jobs;
using Soup.Relics;
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
        private int _suppressChanged;
        private bool _relicWarehouseBonusNormalized;

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

        /// <summary>关卡奖励 / 事件等非遗物带来的仓库上限加成。</summary>
        public int WarehouseCapacityBonus => _warehouseCapacityBonus;

        /// <summary>
        /// 仓库容量 = 配置基础值 + 非遗物加成 + 持有遗物加成（如大仓库）。
        /// 0 = 不限（仅当基础与加成都为 0）。
        /// </summary>
        public int WarehouseCapacity
        {
            get
            {
                EnsureRelicWarehouseBonusNormalized();
                int baseCap = config != null ? Mathf.Max(0, config.WarehouseCapacity) : 0;
                int relicBonus = RelicEffectRunner.SumOwnedWarehouseCapacityBonus();
                // External bonus may be negative (event penalties).
                return Mathf.Max(0, baseCap + _warehouseCapacityBonus + relicBonus);
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
            _relicWarehouseBonusNormalized = false;
            RaiseChanged();
        }

        /// <summary>Clear only flavor stocks (spicy / sour / cold / magic).</summary>
        public void ClearFlavors()
        {
            if (_spicy == 0 && _sour == 0 && _cold == 0 && _magic == 0)
                return;
            _spicy = _sour = _cold = _magic = 0;
            RaiseChanged();
        }

        /// <summary>
        /// Per-level stocks: raw materials, processed, cooked, and flavors.
        /// Keeps warehouse capacity bonus from relics / rewards.
        /// </summary>
        public void ClearLevelStocks()
        {
            if (_soft == 0 && _tough == 0 && _solid == 0
                && _processed == 0 && _cooked == 0
                && _spicy == 0 && _sour == 0 && _cold == 0 && _magic == 0)
                return;

            _soft = _tough = _solid = 0;
            _processed = _cooked = 0;
            _spicy = _sour = _cold = _magic = 0;
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
            _relicWarehouseBonusNormalized = false;
            RaiseChanged();
        }

        public readonly struct Snapshot
        {
            public readonly int Soft;
            public readonly int Tough;
            public readonly int Solid;
            public readonly int Spicy;
            public readonly int Sour;
            public readonly int Cold;
            public readonly int Magic;
            public readonly int Processed;
            public readonly int Cooked;
            public readonly int WarehouseCapacityBonus;

            public Snapshot(
                int soft, int tough, int solid,
                int spicy, int sour, int cold, int magic,
                int processed, int cooked,
                int warehouseCapacityBonus)
            {
                Soft = soft;
                Tough = tough;
                Solid = solid;
                Spicy = spicy;
                Sour = sour;
                Cold = cold;
                Magic = magic;
                Processed = processed;
                Cooked = cooked;
                WarehouseCapacityBonus = warehouseCapacityBonus;
            }
        }

        public Snapshot CaptureSnapshot() =>
            new Snapshot(
                _soft, _tough, _solid,
                _spicy, _sour, _cold, _magic,
                _processed, _cooked,
                _warehouseCapacityBonus);

        public void RestoreSnapshot(Snapshot snapshot)
        {
            _soft = snapshot.Soft;
            _tough = snapshot.Tough;
            _solid = snapshot.Solid;
            _spicy = snapshot.Spicy;
            _sour = snapshot.Sour;
            _cold = snapshot.Cold;
            _magic = snapshot.Magic;
            _processed = snapshot.Processed;
            _cooked = snapshot.Cooked;
            _warehouseCapacityBonus = snapshot.WarehouseCapacityBonus;
        }

        public void PushSuppressChanged() => _suppressChanged++;

        public void PopSuppressChanged()
        {
            if (_suppressChanged > 0)
                _suppressChanged--;
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

        /// <summary>UI / 遗物获得后刷新显示。</summary>
        public void NotifyChanged() => RaiseChanged();

        /// <summary>
        /// 旧存档 / 旧逻辑把「大仓库」一次性加进了 bonus 字段；剥掉遗物部分，改为实时按持有遗物计算。
        /// </summary>
        public void StripBakedRelicWarehouseBonus()
        {
            int relic = RelicEffectRunner.SumOwnedWarehouseCapacityBonus();
            if (relic > 0 && _warehouseCapacityBonus > 0)
                _warehouseCapacityBonus = Mathf.Max(0, _warehouseCapacityBonus - relic);
            _relicWarehouseBonusNormalized = true;
            RaiseChanged();
        }

        private void EnsureRelicWarehouseBonusNormalized()
        {
            if (_relicWarehouseBonusNormalized) return;
            // Relics may not be ready on very first access during boot; retry next read.
            if (RelicManager.Instance == null) return;
            StripBakedRelicWarehouseBonus();
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

        private void RaiseChanged()
        {
            if (_suppressChanged > 0) return;
            Changed?.Invoke();
        }
    }
}
