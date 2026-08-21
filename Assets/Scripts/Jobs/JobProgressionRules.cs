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
        public const int ProcessMaxStations = 2;

        /// <summary>所有岗位进阶深度上限（树状：一阶 + 二阶）。</summary>
        public const int MaxAdvanceDepth = JobAdvancePath.MaxDepth;

        public const int GatherMaxUpgradesPerJob = MaxAdvanceDepth;
        public const int ProcessMaxUpgradesPerJob = MaxAdvanceDepth;
        /// <summary>烹饪目前只有一阶有效效果（进阶1），深度限制为 1。</summary>
        public const int CookMaxUpgradesPerJob = 1;

        /// <summary>采集 / 处理：每通关 1 次进阶机会。</summary>
        public const int GatherAdvanceChargesPerClear = 1;
        public const int ProcessAdvanceChargesPerClear = 1;

        /// <summary>烹饪：每通关 2 关才给 1 次进阶机会（第 2、4、6… 关通关后）。</summary>
        public const int CookAdvanceEveryClears = 2;

        /// <summary>采集开局固定岗位。</summary>
        public const string StartingGatherJobId = "mushroom";

        /// <summary>采集新增岗位时，从锁定池中抽出几个供选择（开局 / 换岗 offer）。</summary>
        public const int GatherNewJobOfferCount = 2;

        /// <summary>进阶巡视点空位解锁：从锁定池随机抽出几个供三选一；未选中的下次仍可出现。</summary>
        public const int AdvancementUnlockOfferCount = 3;

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

        /// <summary>
        /// 通关后（levelsCleared 为已通关数，从 1 起）各区应发放的进阶次数。
        /// 采集/处理每关 1；烹饪每 <see cref="CookAdvanceEveryClears"/> 关 1。
        /// </summary>
        public static void AdvanceChargesForClear(
            int levelsClearedIncludingThis,
            out int gather,
            out int process,
            out int cook)
        {
            int cleared = levelsClearedIncludingThis < 1 ? 1 : levelsClearedIncludingThis;
            gather = GatherAdvanceChargesPerClear;
            process = ProcessAdvanceChargesPerClear;
            cook = (cleared % CookAdvanceEveryClears == 0) ? 1 : 0;
        }

        public static bool GrantsCookAdvanceOnClear(int levelsClearedIncludingThis)
        {
            AdvanceChargesForClear(levelsClearedIncludingThis, out _, out _, out int cook);
            return cook > 0;
        }
    }
}
