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
    /// Full in-run state snapshot (resources / turns / employees / relics / progression /
    /// level flow / events / job modifiers). Used for 撤回上一回合 and disk saves.
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        // ---------------------------------------------------------------- meta
        public long SavedAtUtcTicks;
        public string LevelDisplayName = string.Empty;
        public string RandomStateJson = string.Empty;

        // ----------------------------------------------------------- resources
        public int Soft;
        public int Tough;
        public int Solid;
        public int Spicy;
        public int Sour;
        public int Cold;
        public int Magic;
        public int Processed;
        public int Cooked;

        // ------------------------------------------------------------ turn flow
        public int TurnIndex;
        public int Score;
        public int LastTurnCooked;
        public int LastTurnScore;
        public int StageIndex = 1;
        public int StageCooked;

        // -------------------------------------------------------------- employees
        public List<string> EmployeeOwnedTypes = new List<string>();
        public List<int> EmployeeOwnedCounts = new List<int>();
        public List<string> EmployeeAssignTypes = new List<string>();
        public List<string> EmployeeAssignJobIds = new List<string>();
        public List<int> EmployeeAssignCounts = new List<int>();

        // ----------------------------------------------------------------- relics
        public List<string> RelicIds = new List<string>();

        // -------------------------------------------------------- job progression
        public List<string> UnlockedJobIds = new List<string>();
        public List<string> UpgradeJobIds = new List<string>();
        public List<int> UpgradeLevels = new List<int>();
        public List<string> GatherOfferIds = new List<string>();
        public bool GatherStarterPicked;
        public bool ProcessStarterPicked;

        // ------------------------------------------------------------- level flow
        public int LevelLevelIndex;
        public int LevelTurnIndex;
        public int LevelScoreGainedInLevel;
        public int LevelOutcomeInt;
        public bool LevelClearRewardsActive;
        public int LevelRunModeInt;
        public bool LevelRewardClaimed;
        public bool LevelBriefingActive;
        public bool LevelOutroActive;
        public bool LevelStarted;
        public int LevelLastSourUsed;
        public int LevelLastSourScore;
        public string LevelStartSnapshotJson = string.Empty;
        public List<int> LevelRewardKinds = new List<int>();
        public List<string> LevelRewardContentIds = new List<string>();
        public List<string> LevelRewardTitles = new List<string>();
        public List<string> LevelRewardDescriptions = new List<string>();

        // ----------------------------------------------------------------- events
        public int EventChiefIncentive;
        public int EventCooldownTurnsLeft;
        public string EventPendingId = string.Empty;
        public List<string> EventQueuedIds = new List<string>();
        public List<string> EventSeenIds = new List<string>();

        // ----------------------------------------------------------- job modifiers
        public List<string> ModYieldJobIds = new List<string>();
        public List<float> ModYieldValues = new List<float>();
        public List<string> ModCapacityJobIds = new List<string>();
        public List<int> ModCapacityValues = new List<int>();
        public List<string> ModFlavorJobIds = new List<string>();
        public List<int> ModFlavorKinds = new List<int>();
        public List<int> ModFlavorPerUnits = new List<int>();
        public List<string> ModDisabledJobIds = new List<string>();
    }

    /// <summary>
    /// Save slot summary shown in menus without applying the save.
    /// </summary>
    public class SaveSlotInfo
    {
        public bool Exists;
        public int Slot;
        public string LevelDisplayName = string.Empty;
        public int TotalScore;
        public int TurnIndex;
        public DateTime SavedAtUtc;
    }

    /// <summary>
    /// Captures / restores GameSaveData against the live managers,
    /// plus JSON disk persistence (3 slots under persistentDataPath/saves).
    /// </summary>
    public static class GameSaveService
    {
        public const int SlotCount = 3;
        private static readonly string SaveFolder =
            Path.Combine(Application.persistentDataPath, "saves");

        // --------------------------------------------------------------- capture

        public static GameSaveData Capture()
        {
            var data = new GameSaveData
            {
                SavedAtUtcTicks = DateTime.UtcNow.Ticks,
                RandomStateJson = JsonUtility.ToJson(UnityEngine.Random.state)
            };

            var store = ResourceStore.Instance;
            if (store != null)
            {
                data.Soft = store.Soft;
                data.Tough = store.Tough;
                data.Solid = store.Solid;
                data.Spicy = store.Spicy;
                data.Sour = store.Sour;
                data.Cold = store.Cold;
                data.Magic = store.Magic;
                data.Processed = store.Processed;
                data.Cooked = store.Cooked;
            }

            var turns = TurnManager.Instance;
            if (turns != null)
            {
                data.TurnIndex = turns.TurnIndex;
                data.Score = turns.Score;
                data.LastTurnCooked = turns.LastTurnCooked;
                data.LastTurnScore = turns.LastTurnScore;
                data.StageIndex = turns.StageIndex;
                data.StageCooked = turns.StageCooked;
            }

            var employees = EmployeeManager.Instance;
            if (employees != null)
            {
                employees.CaptureOwned(data.EmployeeOwnedTypes, data.EmployeeOwnedCounts);
                employees.CaptureAssignments(
                    data.EmployeeAssignTypes,
                    data.EmployeeAssignJobIds,
                    data.EmployeeAssignCounts);
            }

            var relics = RelicManager.Instance;
            if (relics != null)
            {
                var owned = relics.Owned;
                for (int i = 0; i < owned.Count; i++)
                {
                    if (owned[i] != null)
                        data.RelicIds.Add(owned[i].Id);
                }
            }

            var levels = LevelManager.Instance;
            if (levels != null)
            {
                levels.CaptureState(
                    out data.LevelLevelIndex,
                    out data.LevelTurnIndex,
                    out data.LevelScoreGainedInLevel,
                    out data.LevelOutcomeInt,
                    out data.LevelClearRewardsActive);
                levels.CaptureExtendedState(data);
                data.LevelDisplayName = levels.Current != null ? levels.Current.DisplayName : string.Empty;
            }

            var events = EventManager.Instance;
            if (events != null)
            {
                events.CaptureState(
                    out data.EventChiefIncentive,
                    out data.EventCooldownTurnsLeft,
                    out string pendingId);
                data.EventPendingId = pendingId ?? string.Empty;
                events.CaptureExtendedState(data);
            }

            var modifiers = JobModifierManager.Instance;
            if (modifiers != null)
            {
                modifiers.CaptureState(
                    data.ModYieldJobIds,
                    data.ModYieldValues,
                    data.ModCapacityJobIds,
                    data.ModCapacityValues,
                    data.ModFlavorJobIds,
                    data.ModFlavorKinds,
                    data.ModFlavorPerUnits,
                    data.ModDisabledJobIds);
            }

            CaptureProgression(data);
            return data;
        }

        public static void Apply(GameSaveData data)
        {
            if (data == null) return;

            ResourceStore.Instance?.ApplyState(
                data.Soft, data.Tough, data.Solid,
                data.Spicy, data.Sour, data.Cold, data.Magic,
                data.Processed, data.Cooked);

            TurnManager.Instance?.ApplyState(
                data.TurnIndex,
                data.Score,
                data.LastTurnCooked,
                data.LastTurnScore,
                data.StageIndex,
                data.StageCooked);

            EmployeeManager.Instance?.ApplyState(
                data.EmployeeOwnedTypes,
                data.EmployeeOwnedCounts,
                data.EmployeeAssignTypes,
                data.EmployeeAssignJobIds,
                data.EmployeeAssignCounts);

            RelicManager.Instance?.ApplyOwnedIds(data.RelicIds);

            LevelManager.Instance?.ApplyState(
                data.LevelLevelIndex,
                data.LevelTurnIndex,
                data.LevelScoreGainedInLevel,
                data.LevelOutcomeInt,
                data.LevelClearRewardsActive);
            LevelManager.Instance?.ApplyExtendedState(data);

            // 事件队列可能带岗位解锁条件，必须先恢复岗位再重建待选事件。
            ApplyProgression(data);

            EventManager.Instance?.ApplyState(
                data.EventChiefIncentive,
                data.EventCooldownTurnsLeft,
                data.EventPendingId);
            EventManager.Instance?.ApplyExtendedState(data);

            JobModifierManager.Instance?.ApplyState(
                data.ModYieldJobIds,
                data.ModYieldValues,
                data.ModCapacityJobIds,
                data.ModCapacityValues,
                data.ModFlavorJobIds,
                data.ModFlavorKinds,
                data.ModFlavorPerUnits,
                data.ModDisabledJobIds);

            if (!string.IsNullOrWhiteSpace(data.RandomStateJson))
                UnityEngine.Random.state = JsonUtility.FromJson<UnityEngine.Random.State>(data.RandomStateJson);
        }

        // ------------------------------------------------------------------ disk

        public static string GetSlotPath(int slot)
        {
            return Path.Combine(SaveFolder, $"slot_{Mathf.Clamp(slot, 1, SlotCount)}.json");
        }

        public static bool SaveToDisk(int slot, GameSaveData data = null)
        {
            if (data == null)
                data = Capture();
            if (data == null) return false;

            try
            {
                Directory.CreateDirectory(SaveFolder);
                File.WriteAllText(GetSlotPath(slot), JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSaveService] 保存失败（槽位 {slot}）：{e.Message}");
                return false;
            }
        }

        public static GameSaveData LoadFromDisk(int slot)
        {
            string path = GetSlotPath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSaveService] 读取失败（槽位 {slot}）：{e.Message}");
                return null;
            }
        }

        public static SaveSlotInfo GetSlotInfo(int slot)
        {
            var info = new SaveSlotInfo { Slot = slot };
            var data = LoadFromDisk(slot);
            if (data == null) return info;

            info.Exists = true;
            info.LevelDisplayName = data.LevelDisplayName;
            info.TotalScore = data.Score;
            info.TurnIndex = data.TurnIndex;
            info.SavedAtUtc = data.SavedAtUtcTicks > 0
                ? new DateTime(data.SavedAtUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;
            return info;
        }

        public static bool DeleteSlot(int slot)
        {
            string path = GetSlotPath(slot);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        public static int FindLatestSlot()
        {
            int latest = -1;
            DateTime latestTime = DateTime.MinValue;
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                var info = GetSlotInfo(slot);
                if (!info.Exists || info.SavedAtUtc <= latestTime) continue;
                latestTime = info.SavedAtUtc;
                latest = slot;
            }

            return latest;
        }

        /// <summary>
        /// Load a save into a live run: initialize core managers, wipe state without
        /// re-firing first-level start effects, then apply the snapshot.
        /// </summary>
        public static bool StartRunFromSave(GameSaveData data)
        {
            if (data == null) return false;

            EnsureCoreManagers();
            TurnManager.Instance?.ResetRun(restartLevel: false);
            Apply(data);
            TurnManager.Instance?.ClearUndoSnapshot();
            return true;
        }

        /// <summary>Make sure the manager chain exists (main-menu load path).</summary>
        private static void EnsureCoreManagers()
        {
            var config = Resources.Load<GameConfig>(ResourceStore.ResourcesConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                config.hideFlags = HideFlags.HideAndDontSave;
            }

            ResourceStore.Initialize(config);
            ElfManager.Initialize(config);
            TurnManager.Initialize();
        }

        // ----------------------------------------------------------- progression

        private static void CaptureProgression(GameSaveData data)
        {
            var progression = JobProgressionManager.Instance;
            if (progression == null) return;

            foreach (var job in progression.Unlocked)
            {
                if (job != null)
                    data.UnlockedJobIds.Add(job.Id);
            }

            foreach (var job in progression.Unlocked)
            {
                if (job == null) continue;
                int level = progression.GetUpgradeLevel(job);
                if (level > 0)
                {
                    data.UpgradeJobIds.Add(job.Id);
                    data.UpgradeLevels.Add(level);
                }
            }

            var offer = progression.CurrentGatherOffer;
            for (int i = 0; i < offer.Count; i++)
            {
                if (offer[i] != null)
                    data.GatherOfferIds.Add(offer[i].Id);
            }

            data.GatherStarterPicked = !progression.NeedsGatherStarterPick;
            data.ProcessStarterPicked = !progression.NeedsProcessStarterPick;
        }

        private static void ApplyProgression(GameSaveData data)
        {
            var progression = JobProgressionManager.Instance;
            if (progression == null) return;

            progression.ApplyState(
                data.UnlockedJobIds,
                data.UpgradeJobIds,
                data.UpgradeLevels,
                data.GatherOfferIds,
                data.GatherStarterPicked,
                data.ProcessStarterPicked);
        }
    }
}
