using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// Shared math helpers. Design rule: always round up.
    /// </summary>
    public static class GameMath
    {
        public static int CeilToInt(float value)
        {
            if (value <= 0f) return 0;
            return Mathf.CeilToInt(value);
        }

        public static int CeilDiv(int numerator, int denominator)
        {
            if (numerator <= 0 || denominator <= 0) return 0;
            return (numerator + denominator - 1) / denominator;
        }
    }
}
