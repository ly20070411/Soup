using System;
using System.Collections.Generic;
using System.IO;
using Soup.Employees;
using Soup.Events;
using Soup.Jobs;
using Soup.Levels;
using Soup.Relics;
using UnityEngine;

namespace Soup.Game
{
    /// <summary>
    /// JSON save/load for the current run (resources, elves, jobs, relics, turns).
    /// </summary>
    public static class GameSaveService
    {
        public const string FileName = "soup_save.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave() => File.Exists(SavePath);

        public static bool TrySave(out string message)
        {
            try
            {
                var data = Capture();
                string json = JsonUtility.ToJson(data, true);
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath) ?? Application.persistentDataPath);
                File.WriteAllText(SavePath, json);
                message = $"进度已保存\n{SavePath}";
                return true;
            }
            catch (Exception e)
            {
                message = $"保存失败：{e.Message}";
                Debug.LogError($"[GameSaveService] Save failed: {e}");
                return false;
            }
        }

        public static bool TryLoad(out string message)
        {
            if (!HasSave())
            {
                message = "没有可读取的存档";
                return false;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                var data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null)
                {
                    message = "存档损坏";
                    return false;
                }

                Apply(data);
                TurnManager.Instance?.ClearUndoSnapshot();
                message = $"进度已读取（回合 {data.turnIndex}）";
                return true;
            }
            catch (Exception e)
            {
                message = $"读取失败：{e.Message}";
                Debug.LogError($"[GameSaveService] Load failed: {e}");
                return false;
            }
        }

        public static GameSaveData Capture()
        {
            var data = new GameSaveData
            {
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var turns = TurnManager.Instance;
            if (turns != null)
            {
                data.turnIndex = turns.TurnIndex;
                data.score = turns.Score;
                data.lastTurnCooked = turns.LastTurnCooked;
                data.lastTurnScore = turns.LastTurnScore;
                data.stageIndex = turns.StageIndex;
                data.stageCooked = turns.StageCooked;
                data.scoreFromCook = turns.ScoreFromCook;
                data.scoreFromSpicy = turns.ScoreFromSpicy;
                data.scoreFromCold = turns.ScoreFromCold;
                data.scoreFromSour = turns.ScoreFromSour;
                data.scoreFromMagic = turns.ScoreFromMagic;
            }

            var store = ResourceStore.Instance;
            if (store != null)
            {
                data.soft = store.Soft;
                data.tough = store.Tough;
                data.solid = store.Solid;
                data.spicy = store.Spicy;
                data.sour = store.Sour;
                data.cold = store.Cold;
                data.magic = store.Magic;
                data.processed = store.Processed;
                data.cooked = store.Cooked;
                data.warehouseCapacityBonus = store.WarehouseCapacityBonus;
            }

            var elves = ElfManager.Instance;
            if (elves != null)
            {
                data.totalElves = elves.TotalCount;
                foreach (var pair in elves.GetAssignments())
                {
                    if (pair.Key == null || string.IsNullOrEmpty(pair.Key.Id) || pair.Value <= 0)
                        continue;
                    // Legacy field: only persist elf occupying? Better persist elf-only via EmployeeManager below.
                }
            }

            var employees = EmployeeManager.Instance;
            if (employees != null)
            {
                var ownedTypes = new List<string>();
                var ownedCounts = new List<int>();
                employees.CaptureOwned(ownedTypes, ownedCounts);
                for (int i = 0; i < ownedTypes.Count; i++)
                {
                    data.employees.Add(new EmployeeOwnedSave
                    {
                        typeId = ownedTypes[i],
                        count = ownedCounts[i]
                    });
                    if (ownedTypes[i] == EmployeeManager.ElfId)
                        data.totalElves = ownedCounts[i];
                }

                var assignTypes = new List<string>();
                var assignJobs = new List<string>();
                var assignCounts = new List<int>();
                employees.CaptureAssignments(assignTypes, assignJobs, assignCounts);
                for (int i = 0; i < assignTypes.Count; i++)
                {
                    data.employeeAssignments.Add(new EmployeeAssignmentSave
                    {
                        typeId = assignTypes[i],
                        jobId = assignJobs[i],
                        count = assignCounts[i]
                    });
                    if (assignTypes[i] == EmployeeManager.ElfId)
                    {
                        data.assignments.Add(new JobAssignmentSave
                        {
                            jobId = assignJobs[i],
                            count = assignCounts[i]
                        });
                    }
                }
            }

            var progression = JobProgressionManager.Instance;
            if (progression != null)
            {
                data.gatherStarterPicked = !progression.NeedsGatherStarterPick;
                data.processStarterPicked = !progression.NeedsProcessStarterPick;

                foreach (var job in progression.Unlocked)
                {
                    if (job == null || string.IsNullOrEmpty(job.Id)) continue;
                    data.unlockedJobIds.Add(job.Id);
                    int level = progression.GetUpgradeLevel(job);
                    if (level > 0)
                    {
                        data.jobUpgrades.Add(new JobUpgradeSave
                        {
                            jobId = job.Id,
                            level = level,
                            pathId = (int)progression.GetAdvancePath(job)
                        });
                    }
                }

                var offer = progression.CurrentGatherOffer;
                for (int i = 0; i < offer.Count; i++)
                {
                    if (offer[i] != null && !string.IsNullOrEmpty(offer[i].Id))
                        data.gatherOfferIds.Add(offer[i].Id);
                }

                if (data.gatherAuraSourceJobIds == null)
                    data.gatherAuraSourceJobIds = new List<string>();
                if (data.gatherAuraTargetJobIds == null)
                    data.gatherAuraTargetJobIds = new List<string>();
                progression.CaptureDesignatedGatherAuraTargets(
                    data.gatherAuraSourceJobIds,
                    data.gatherAuraTargetJobIds);

                if (data.destroyedGatherJobIds == null)
                    data.destroyedGatherJobIds = new List<string>();
                progression.CaptureDestroyedGatherJobs(data.destroyedGatherJobIds);

                if (data.pendingGatherEfficiencyPenaltyJobIds == null)
                    data.pendingGatherEfficiencyPenaltyJobIds = new List<string>();
                if (data.pendingGatherEfficiencyPenaltyValues == null)
                    data.pendingGatherEfficiencyPenaltyValues = new List<float>();
                progression.CapturePendingGatherEfficiencyPenalties(
                    data.pendingGatherEfficiencyPenaltyJobIds,
                    data.pendingGatherEfficiencyPenaltyValues);
                data.endTurnIncentivesGrantedThisLevel = progression.EndTurnIncentivesGrantedThisLevel;

                if (data.jobEventMods == null)
                    data.jobEventMods = new List<JobEventModSave>();
                progression.CaptureEventMods(data.jobEventMods);
            }

            var relics = RelicManager.Instance;
            if (relics != null)
            {
                for (int i = 0; i < relics.Owned.Count; i++)
                {
                    var relic = relics.Owned[i];
                    if (relic != null && !string.IsNullOrEmpty(relic.Id))
                        data.ownedRelicIds.Add(relic.Id);
                }
            }

            var events = EventManager.Instance;
            if (events != null)
            {
                data.chiefIncentive = 0; // legacy field; 激励 is stored in ownedRelicIds
                data.lastRandomEventTurn = events.LastRandomEventTurn;
                var seen = events.GetSeenEventIds();
                for (int i = 0; i < seen.Count; i++)
                {
                    if (!string.IsNullOrEmpty(seen[i]))
                        data.seenEventIds.Add(seen[i]);
                }
            }

            var levels = LevelManager.Instance;
            if (levels != null)
            {
                if (data.levelRewardRelicOfferIds == null)
                    data.levelRewardRelicOfferIds = new List<string>();
                levels.CaptureState(
                    out data.levelId,
                    out data.levelListIndex,
                    out data.levelTurnIndex,
                    out data.levelStartScore,
                    out data.levelFinishedScore,
                    out data.levelOutcome,
                    out data.levelAwaitingSettle,
                    out data.levelsClearedCount,
                    out data.levelRewardElvesClaimed,
                    out data.levelRewardWarehouseClaimed,
                    out data.levelRewardRelicClaimed,
                    out data.levelRewardAdvanceClaimed,
                    out data.levelRewardEventsClaimed,
                    out data.levelRewardGatherCharges,
                    out data.levelRewardProcessCharges,
                    out data.levelRewardCookCharges,
                    data.levelRewardRelicOfferIds);
            }

            var cam = UnityEngine.Object.FindObjectOfType<ZoneCameraController>();
            if (cam != null)
                data.currentZone = (int)cam.CurrentZone;

            return data;
        }

        public static void Apply(GameSaveData data)
        {
            if (data == null) return;

            TurnManager.Instance?.ApplyState(
                data.turnIndex,
                data.score,
                data.lastTurnCooked,
                data.lastTurnScore,
                data.stageIndex > 0 ? data.stageIndex : 1,
                data.stageCooked,
                data.scoreFromCook,
                data.scoreFromSpicy,
                data.scoreFromCold,
                data.scoreFromSour,
                data.scoreFromMagic);

            ResourceStore.Instance?.ApplyState(
                data.soft, data.tough, data.solid,
                data.spicy, data.sour, data.cold, data.magic,
                data.processed, data.cooked,
                data.warehouseCapacityBonus);

            JobProgressionManager.Instance?.ApplyState(
                data.unlockedJobIds,
                ExtractUpgradeIds(data.jobUpgrades),
                ExtractUpgradePathIds(data.jobUpgrades),
                data.gatherOfferIds,
                data.gatherStarterPicked,
                data.processStarterPicked,
                data.gatherAuraSourceJobIds,
                data.gatherAuraTargetJobIds,
                data.destroyedGatherJobIds,
                data.pendingGatherEfficiencyPenaltyJobIds,
                data.pendingGatherEfficiencyPenaltyValues,
                data.endTurnIncentivesGrantedThisLevel,
                data.jobEventMods);

            RelicManager.Instance?.ApplyOwnedIds(data.ownedRelicIds);
            MigrateLegacyChiefIncentive(data.chiefIncentive);

            EventManager.Instance?.ApplyState(
                data.seenEventIds,
                data.lastRandomEventTurn);

            LevelManager.Instance?.ApplyState(
                data.levelId,
                data.levelListIndex,
                data.levelTurnIndex,
                data.levelStartScore,
                (LevelOutcome)data.levelOutcome,
                data.levelAwaitingSettle,
                data.levelsClearedCount,
                data.levelRewardElvesClaimed,
                data.levelRewardWarehouseClaimed,
                data.levelRewardRelicClaimed,
                data.levelRewardAdvanceClaimed,
                data.levelRewardEventsClaimed,
                data.levelRewardGatherCharges,
                data.levelRewardProcessCharges,
                data.levelRewardCookCharges,
                data.levelRewardRelicOfferIds,
                data.levelFinishedScore);

            if (EmployeeManager.Instance != null && data.employees != null && data.employees.Count > 0)
            {
                var ownedTypes = new List<string>();
                var ownedCounts = new List<int>();
                for (int i = 0; i < data.employees.Count; i++)
                {
                    if (data.employees[i] == null) continue;
                    ownedTypes.Add(data.employees[i].typeId);
                    ownedCounts.Add(data.employees[i].count);
                }

                var assignTypes = new List<string>();
                var assignJobs = new List<string>();
                var assignCounts = new List<int>();
                if (data.employeeAssignments != null)
                {
                    for (int i = 0; i < data.employeeAssignments.Count; i++)
                    {
                        var row = data.employeeAssignments[i];
                        if (row == null) continue;
                        assignTypes.Add(row.typeId);
                        assignJobs.Add(row.jobId);
                        assignCounts.Add(row.count);
                    }
                }

                EmployeeManager.Instance.ApplyState(
                    ownedTypes, ownedCounts, assignTypes, assignJobs, assignCounts);
            }
            else
            {
                ElfManager.Instance?.ApplyState(
                    data.totalElves,
                    ExtractAssignmentIds(data.assignments),
                    ExtractAssignmentCounts(data.assignments));
            }

            var cam = UnityEngine.Object.FindObjectOfType<ZoneCameraController>();
            if (cam != null && Enum.IsDefined(typeof(MapZoneType), data.currentZone))
                cam.SnapToZone((MapZoneType)data.currentZone);

            var map = UnityEngine.Object.FindObjectOfType<JobWorldMap>();
            map?.RefreshLabels();
        }

        private static void MigrateLegacyChiefIncentive(int chiefIncentive)
        {
            if (chiefIncentive <= 0) return;
            var relics = RelicManager.Instance;
            if (relics == null) return;
            var incentive = relics.GetById(RelicManager.IncentiveId);
            if (incentive == null) return;

            for (int i = 0; i < chiefIncentive; i++)
                relics.Acquire(incentive);
        }

        private static List<string> ExtractUpgradeIds(List<JobUpgradeSave> upgrades)
        {
            var list = new List<string>();
            if (upgrades == null) return list;
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i] != null)
                    list.Add(upgrades[i].jobId);
            }
            return list;
        }

        private static List<int> ExtractUpgradePathIds(List<JobUpgradeSave> upgrades)
        {
            var list = new List<int>();
            if (upgrades == null) return list;
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i] == null)
                {
                    list.Add(0);
                    continue;
                }

                // Prefer pathId; fall back to empty if only legacy level was saved.
                list.Add(upgrades[i].pathId);
            }

            return list;
        }

        private static List<string> ExtractAssignmentIds(List<JobAssignmentSave> assignments)
        {
            var list = new List<string>();
            if (assignments == null) return list;
            for (int i = 0; i < assignments.Count; i++)
            {
                if (assignments[i] != null)
                    list.Add(assignments[i].jobId);
            }
            return list;
        }

        private static List<int> ExtractAssignmentCounts(List<JobAssignmentSave> assignments)
        {
            var list = new List<int>();
            if (assignments == null) return list;
            for (int i = 0; i < assignments.Count; i++)
                list.Add(assignments[i] != null ? assignments[i].count : 0);
            return list;
        }
    }
}
