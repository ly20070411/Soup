using System;

namespace Soup.Game
{
    /// <summary>
    /// Shared math helpers. Design rule: always round up.
    /// </summary>
    public static class GameMath
    {
        /// <summary>
        /// Ceil with a tiny epsilon so binary float noise just above an integer
        /// (e.g. 100 * 0.6f → 60.0000038) does not round up by an extra 1.
        /// </summary>
        public static int CeilToInt(float value)
        {
            return CeilToInt((double)value);
        }

        public static int CeilToInt(double value)
        {
            if (value <= 0d) return 0;
            // ~1e-4 covers float product noise like 100*0.6f → 60.0000038
            // without changing intentional fractional ceils (e.g. 14.4 → 15).
            return (int)Math.Ceiling(value - 1e-4d);
        }

        /// <summary>Ceil of amount × multiplier (avoids float product noise).</summary>
        public static int CeilMul(int amount, float multiplier)
        {
            if (amount <= 0 || multiplier <= 0f) return 0;
            return CeilToInt(amount * (double)multiplier);
        }

        public static int CeilDiv(int numerator, int denominator)
        {
            if (numerator <= 0 || denominator <= 0) return 0;
            return (numerator + denominator - 1) / denominator;
        }
    }
}
