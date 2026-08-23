using Soup.Jobs;
using Soup.Levels;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// 关卡间 → 玩法地图进阶巡视。静态 IsActive 跨场景保留（与 player build 一致）。
    /// </summary>
    public static class AdvancementVisit
    {
        public static bool IsActive { get; private set; }
        public static MapZoneType Zone { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsActive = false;
            Zone = MapZoneType.Gather;
        }

        public static bool CanEnter(MapZoneType zone, LevelClearRewardSession session)
        {
            if (session == null || !session.IsActive || session.AdvancementClaimed)
                return false;
            return ChargeFor(zone, session) > 0;
        }

        public static int ChargeFor(MapZoneType zone, LevelClearRewardSession session)
        {
            if (session == null) return 0;
            switch (zone)
            {
                case MapZoneType.Gather: return session.GatherCharges;
                case MapZoneType.Process: return session.ProcessCharges;
                case MapZoneType.Cook: return session.CookCharges;
                default: return 0;
            }
        }

        public static JobType JobTypeFor(MapZoneType zone)
        {
            switch (zone)
            {
                case MapZoneType.Gather: return JobType.Gather;
                case MapZoneType.Process: return JobType.Process;
                default: return JobType.Cook;
            }
        }

        public static string ZoneDisplayName(MapZoneType zone)
        {
            switch (zone)
            {
                case MapZoneType.Gather: return "采集区";
                case MapZoneType.Process: return "处理区";
                case MapZoneType.Cook: return "烹饪区";
                default: return zone.ToString();
            }
        }

        public static void Begin(MapZoneType zone)
        {
            IsActive = true;
            Zone = zone;
            var session = LevelManager.Instance?.ClearRewards;
            session?.EnsureUnlockOffers(JobTypeFor(zone));
        }

        public static void Clear()
        {
            IsActive = false;
            Zone = MapZoneType.Gather;
        }

        public static bool MatchesZone(JobItem job)
        {
            return job != null && job.JobType == JobTypeFor(Zone);
        }
    }
}
