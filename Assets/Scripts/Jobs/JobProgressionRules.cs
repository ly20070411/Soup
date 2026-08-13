namespace Soup.Jobs
{
    /// <summary>
    /// Design constants for job unlock / upgrade progression.
    /// </summary>
    public static class JobProgressionRules
    {
        public const int DefaultMaxWorkers = 5;
        public const int DefaultUpgradeWorkerBonus = 5;

        public const int GatherMaxStations = 4;
        public const int ProcessMaxStations = 4;

        public const int GatherMaxUpgradesPerJob = 2;
        public const int ProcessMaxUpgradesPerJob = 2;
        public const int CookMaxUpgradesPerJob = 1;

        /// <summary>采集开局固定岗位。</summary>
        public const string StartingGatherJobId = "mushroom";

        /// <summary>采集新增岗位时，从锁定池中抽出几个供 2 选 1。</summary>
        public const int GatherNewJobOfferCount = 2;

        public static int MaxStations(JobType type)
        {
            switch (type)
            {
                case JobType.Gather: return GatherMaxStations;
                case JobType.Process: return ProcessMaxStations;
                case JobType.Cook: return int.MaxValue;
                default: return 0;
            }
        }

        public static int MaxUpgradesPerJob(JobType type)
        {
            switch (type)
            {
                case JobType.Gather: return GatherMaxUpgradesPerJob;
                case JobType.Process: return ProcessMaxUpgradesPerJob;
                case JobType.Cook: return CookMaxUpgradesPerJob;
                default: return 0;
            }
        }

        public static bool UsesPopulationCap(JobType type) =>
            type == JobType.Gather || type == JobType.Process;
    }
}
