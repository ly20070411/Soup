using System;
using System.Collections.Generic;
using Soup.Jobs;

namespace Soup.Game
{
    [Serializable]
    public class GameSaveData
    {
        public int version = 1;
        public long savedAtUnix;

        public int turnIndex;
        public int score;
        public int lastTurnCooked;
        public int lastTurnScore;
        public int stageIndex = 1;
        public int stageCooked;

        public int scoreFromCook;
        public int scoreFromSpicy;
        public int scoreFromCold;
        public int scoreFromSour;
        public int scoreFromMagic;

        public int soft;
        public int tough;
        public int solid;
        public int spicy;
        public int sour;
        public int cold;
        public int magic;
        public int processed;
        public int cooked;
        public int warehouseCapacityBonus;
        public int pendingRelicProcessedGrant;

        public int totalElves;
        public List<JobAssignmentSave> assignments = new List<JobAssignmentSave>();

        public List<EmployeeOwnedSave> employees = new List<EmployeeOwnedSave>();
        public List<EmployeeAssignmentSave> employeeAssignments = new List<EmployeeAssignmentSave>();

        public List<string> unlockedJobIds = new List<string>();
        public List<JobUpgradeSave> jobUpgrades = new List<JobUpgradeSave>();
        public List<string> gatherOfferIds = new List<string>();
        public bool gatherStarterPicked;
        public bool processStarterPicked;
        public List<string> gatherAuraSourceJobIds = new List<string>();
        public List<string> gatherAuraTargetJobIds = new List<string>();
        public List<string> destroyedGatherJobIds = new List<string>();
        public List<string> pendingGatherEfficiencyPenaltyJobIds = new List<string>();
        public List<float> pendingGatherEfficiencyPenaltyValues = new List<float>();
        public int endTurnIncentivesGrantedThisLevel;
        public bool gatherJobsActAsHappyTuotuo;

        public List<string> ownedRelicIds = new List<string>();

        /// <summary>Legacy: migrated into ownedRelicIds as 激励 stacks on load.</summary>
        public int chiefIncentive;
        public List<string> seenEventIds = new List<string>();
        public int lastRandomEventTurn;

        public string levelId = string.Empty;
        public int levelListIndex;
        public int levelTurnIndex;
        public int levelStartScore;
        public int levelFinishedScore;
        public int levelOutcome;
        public bool levelAwaitingSettle;
        public int levelsClearedCount;
        public List<bool> levelChallengeReachedFlags = new List<bool>();
        public bool levelRewardElvesClaimed;
        public bool levelRewardWarehouseClaimed;
        public bool levelRewardRelicClaimed;
        public bool levelRewardShopClaimed;
        public bool levelRewardAdvanceClaimed;
        public bool levelRewardEventsClaimed;
        public bool levelRewardStandardStageEventsStarted;
        public int levelRewardGatherCharges;
        public int levelRewardProcessCharges;
        public int levelRewardCookCharges;
        public List<string> levelRewardRelicOfferIds = new List<string>();
        public List<string> levelRewardShopOfferIds = new List<string>();
        public List<string> levelRewardGatherUnlockOfferIds = new List<string>();
        public List<string> levelRewardProcessUnlockOfferIds = new List<string>();

        public List<JobEventModSave> jobEventMods = new List<JobEventModSave>();

        public int currentZone;
    }

    [Serializable]
    public class JobAssignmentSave
    {
        public string jobId;
        public int count;
    }

    [Serializable]
    public class JobUpgradeSave
    {
        public string jobId;
        public int level;
        public int pathId;
    }

    [Serializable]
    public class EmployeeOwnedSave
    {
        public string typeId;
        public int count;
    }

    [Serializable]
    public class EmployeeAssignmentSave
    {
        public string typeId;
        public string jobId;
        public int count;
    }

    [Serializable]
    public class JobEventModSave
    {
        public string jobId;
        public float yieldBonus;
        public int maxWorkersDelta;
        public int rawPerUnitDelta;
        public int coldPerUnitDelta;
        public int spicyPerUnitDelta;
        public int sourPerUnitDelta;
        public int magicPerUnitDelta;
        public bool produceAllFourFlavors;

        public static JobEventModSave From(string jobId, JobEventMods mods)
        {
            if (mods == null) return null;
            return new JobEventModSave
            {
                jobId = jobId,
                yieldBonus = mods.YieldBonus,
                maxWorkersDelta = mods.MaxWorkersDelta,
                rawPerUnitDelta = mods.RawPerUnitDelta,
                coldPerUnitDelta = mods.ColdPerUnitDelta,
                spicyPerUnitDelta = mods.SpicyPerUnitDelta,
                sourPerUnitDelta = mods.SourPerUnitDelta,
                magicPerUnitDelta = mods.MagicPerUnitDelta,
                produceAllFourFlavors = mods.ProduceAllFourFlavors
            };
        }

        public JobEventMods ToMods()
        {
            return new JobEventMods
            {
                YieldBonus = yieldBonus,
                MaxWorkersDelta = maxWorkersDelta,
                RawPerUnitDelta = rawPerUnitDelta,
                ColdPerUnitDelta = coldPerUnitDelta,
                SpicyPerUnitDelta = spicyPerUnitDelta,
                SourPerUnitDelta = sourPerUnitDelta,
                MagicPerUnitDelta = magicPerUnitDelta,
                ProduceAllFourFlavors = produceAllFourFlavors
            };
        }
    }
}
