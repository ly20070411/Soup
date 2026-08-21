using Soup.Employees;
using Soup.Game;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Events
{
    /// <summary>
    /// Applies option effects to runtime managers.
    /// </summary>
    public static class EventEffectRunner
    {
        public static void Apply(EventOption option)
        {
            if (option?.Effects == null) return;

            for (int i = 0; i < option.Effects.Count; i++)
                Apply(option.Effects[i]);
        }

        public static void Apply(EventEffect effect)
        {
            if (effect == null) return;

            switch (effect.EffectType)
            {
                case EventEffectType.AddElves:
                    ElfManager.Instance?.AddElves(effect.IntValue);
                    break;
                case EventEffectType.AddChiefIncentive:
                    GrantIncentiveStacks(effect);
                    break;
                case EventEffectType.GrantRelic:
                    GrantRelicStacks(effect.RelicReference, Mathf.Max(1, effect.IntValue));
                    break;
                case EventEffectType.AddEmployee:
                    if (effect.EmployeeReference != null && effect.IntValue != 0)
                        EmployeeManager.Instance?.Add(effect.EmployeeReference, effect.IntValue);
                    break;
                case EventEffectType.ModifyWarehouseCapacity:
                    ResourceStore.Instance?.AddWarehouseCapacityBonus(effect.IntValue);
                    break;
                case EventEffectType.RemoveAllFatigue:
                    RemoveAllFatigue();
                    break;
                case EventEffectType.ModifyJobYieldBonus:
                    JobProgressionManager.Instance?.AddEventYieldBonus(
                        effect.JobReference, effect.FloatValue);
                    break;
                case EventEffectType.ModifyJobMaxWorkers:
                    JobProgressionManager.Instance?.AddEventMaxWorkersDelta(
                        effect.JobReference, effect.IntValue);
                    break;
                case EventEffectType.ModifyJobRawAndColdPerUnit:
                    JobProgressionManager.Instance?.AddEventRawAndColdPerUnit(
                        effect.JobReference, effect.IntValue, effect.SecondaryInt);
                    break;
                case EventEffectType.EnableJobAllFourFlavors:
                    JobProgressionManager.Instance?.EnableEventAllFourFlavors(effect.JobReference);
                    break;
                case EventEffectType.DestroyGatherJob:
                    JobProgressionManager.Instance?.TryDestroyGatherJob(effect.JobReference);
                    break;
                case EventEffectType.ChanceElfDeltaOrJobYield:
                    ApplyChanceElfDeltaOrJobYield(effect);
                    break;
            }
        }

        private static void ApplyChanceElfDeltaOrJobYield(EventEffect effect)
        {
            if (effect == null) return;
            if (UnityEngine.Random.value < 0.5f)
            {
                if (effect.IntValue != 0)
                    ElfManager.Instance?.AddElves(effect.IntValue);
                return;
            }

            if (effect.JobReference != null && !Mathf.Approximately(effect.FloatValue, 0f))
                JobProgressionManager.Instance?.AddEventYieldBonus(
                    effect.JobReference, effect.FloatValue);
        }

        private static void RemoveAllFatigue()
        {
            var relics = RelicManager.Instance;
            if (relics == null) return;

            RelicItem fatigue = relics.GetById(RelicManager.FatigueId);
            if (fatigue == null)
            {
                var db = Resources.Load<RelicDatabase>(RelicManager.ResourcesDatabasePath);
                fatigue = db != null ? db.GetById(RelicManager.FatigueId) : null;
            }

            if (fatigue == null)
            {
                Debug.LogWarning("[EventEffectRunner] 找不到遗物「疲倦」，无法消除。");
                return;
            }

            while (relics.RemoveOwned(fatigue))
            {
            }
        }

        /// <summary>Resolve 激励 relic (effect reference or database id).</summary>
        public static RelicItem ResolveIncentiveRelic(EventEffect effect = null)
        {
            if (effect?.RelicReference != null
                && (effect.RelicReference.Id == RelicManager.IncentiveId
                    || effect.RelicReference.DisplayName == "激励"
                    || effect.RelicReference.Id == "incentive"))
                return effect.RelicReference;

            if (RelicManager.Instance != null)
            {
                var fromMgr = RelicManager.Instance.GetById(RelicManager.IncentiveId);
                if (fromMgr != null) return fromMgr;
            }

            var db = Resources.Load<RelicDatabase>(RelicManager.ResourcesDatabasePath);
            return db != null ? db.GetById(RelicManager.IncentiveId) : null;
        }

        private static void GrantIncentiveStacks(EventEffect effect)
        {
            if (effect == null) return;
            int n = effect.IntValue;
            if (n == 0) return;

            var incentive = ResolveIncentiveRelic(effect);
            if (incentive == null)
            {
                Debug.LogWarning("[EventEffectRunner] 找不到遗物「激励」，无法发放。");
                return;
            }

            if (n > 0)
            {
                GrantRelicStacks(incentive, n);
                return;
            }

            // Negative: remove stacks (rare).
            var relics = RelicManager.Instance;
            if (relics == null) return;
            int remove = -n;
            for (int i = 0; i < remove; i++)
            {
                if (!relics.RemoveOwned(incentive))
                    break;
            }
        }

        private static void GrantRelicStacks(RelicItem relic, int stacks)
        {
            if (relic == null || stacks <= 0) return;
            var relics = RelicManager.Instance;
            if (relics == null) return;
            for (int i = 0; i < stacks; i++)
                relics.Acquire(relic);
        }
    }
}
