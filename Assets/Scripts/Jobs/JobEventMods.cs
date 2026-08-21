using System;

namespace Soup.Jobs
{
    /// <summary>Run-long modifiers applied by 进阶专属 events.</summary>
    [Serializable]
    public sealed class JobEventMods
    {
        /// <summary>Additive yield, e.g. 0.3 → ×1.3 gathered units.</summary>
        public float YieldBonus;
        public int MaxWorkersDelta;
        /// <summary>Added to each present raw material (soft/tough/solid) per gathered unit.</summary>
        public int RawPerUnitDelta;
        public int ColdPerUnitDelta;
        public int SpicyPerUnitDelta;
        public int SourPerUnitDelta;
        public int MagicPerUnitDelta;
        /// <summary>Convert random flavor into all four flavors at the same amount.</summary>
        public bool ProduceAllFourFlavors;
    }
}
