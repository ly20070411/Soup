using System.Text;
using UnityEngine;

namespace Soup.Items
{
    /// <summary>
    /// Materials / flavors granted by one gathered ingredient unit.
    /// </summary>
    public struct IngredientYield
    {
        public int Soft;
        public int Tough;
        public int Solid;
        public int Spicy;
        public int Sour;
        public int Cold;
        public int Magic;
        /// <summary>Count of Soft/Tough/Solid rolls resolved at apply time.</summary>
        public int RandomMaterial;
        /// <summary>Count of Spicy/Sour/Cold/Magic rolls resolved at apply time.</summary>
        public int RandomFlavor;

        public int TotalFixedRaw => Soft + Tough + Solid;
        public int TotalFixedFlavor => Spicy + Sour + Cold + Magic;
        public int TotalFlavor => TotalFixedFlavor + RandomFlavor;
        public bool IsEmpty =>
            TotalFixedRaw == 0 && TotalFixedFlavor == 0 && RandomMaterial <= 0 && RandomFlavor <= 0;

        public IngredientYield ScaledBy(int units)
        {
            if (units <= 1) return this;
            return new IngredientYield
            {
                Soft = Soft * units,
                Tough = Tough * units,
                Solid = Solid * units,
                Spicy = Spicy * units,
                Sour = Sour * units,
                Cold = Cold * units,
                Magic = Magic * units,
                RandomMaterial = RandomMaterial * units,
                RandomFlavor = RandomFlavor * units
            };
        }

        public string ToSummary()
        {
            if (IsEmpty) return "无产出";
            var sb = new StringBuilder(48);
            Append(sb, "柔软", Soft);
            Append(sb, "强韧", Tough);
            Append(sb, "坚固", Solid);
            Append(sb, "热辣", Spicy);
            Append(sb, "酸涩", Sour);
            Append(sb, "寒冷", Cold);
            Append(sb, "鲜美", Magic);
            Append(sb, "随机食材", RandomMaterial);
            Append(sb, "随机口味", RandomFlavor);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string label, int value)
        {
            if (value <= 0) return;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(label).Append(value);
        }
    }

    /// <summary>
    /// Converts IngredientItem gameplay stats into raw materials and flavors.
    /// </summary>
    public static class IngredientYieldResolver
    {
        public static IngredientYield FromIngredient(IngredientItem item)
        {
            var yield = new IngredientYield();
            if (item == null || item.Stats == null) return yield;

            for (int i = 0; i < item.Stats.Count; i++)
            {
                var entry = item.Stats[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;

                int amount = CeilPositive(entry.value);
                if (amount <= 0) continue;

                switch (NormalizeKey(entry.key))
                {
                    case "soft":
                    case "柔软":
                    case "柔软食材":
                        yield.Soft += amount;
                        break;
                    case "tough":
                    case "强韧":
                    case "强韧食材":
                        yield.Tough += amount;
                        break;
                    case "solid":
                    case "坚固":
                    case "坚固食材":
                        yield.Solid += amount;
                        break;
                    case "spicy":
                    case "热辣":
                    case "辣":
                        yield.Spicy += amount;
                        break;
                    case "sour":
                    case "酸涩":
                    case "酸":
                        yield.Sour += amount;
                        break;
                    case "cold":
                    case "寒冷":
                    case "冰":
                        yield.Cold += amount;
                        break;
                    case "magic":
                    case "鲜美":
                    case "魔法":
                        yield.Magic += amount;
                        break;
                    case "随机效果":
                    case "随机口味":
                    case "random_flavor":
                        yield.RandomFlavor += amount;
                        break;
                    case "random":
                    case "随机食材":
                    case "随机材质":
                        yield.RandomMaterial += amount;
                        break;
                }
            }

            return yield;
        }

        public static IngredientYield FromIngredient(IngredientItem item, int units)
        {
            return FromIngredient(item).ScaledBy(Mathf.Max(0, units));
        }

        private static int CeilPositive(float value)
        {
            if (value <= 0f) return 0;
            return Mathf.CeilToInt(value);
        }

        private static string NormalizeKey(string key)
        {
            return key.Trim();
        }
    }
}
