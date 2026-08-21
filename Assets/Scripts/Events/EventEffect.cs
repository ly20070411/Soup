using System;
using Soup.Employees;
using Soup.Jobs;
using Soup.Relics;
using UnityEngine;

namespace Soup.Events
{
    [Serializable]
    public class EventEffect
    {
        [SerializeField] private EventEffectType effectType = EventEffectType.AddElves;
        [SerializeField] private int intValue;
        [SerializeField] private int secondaryInt;
        [SerializeField] private float floatValue;
        [SerializeField] private RelicItem relicReference;
        [SerializeField] private EmployeeItem employeeReference;
        [SerializeField] private JobItem jobReference;

        public EventEffectType EffectType => effectType;
        public int IntValue => intValue;
        public int SecondaryInt => secondaryInt;
        public float FloatValue => floatValue;
        public RelicItem RelicReference => relicReference;
        public EmployeeItem EmployeeReference => employeeReference;
        public JobItem JobReference => jobReference;

        private void ClearJobFields()
        {
            jobReference = null;
            floatValue = 0f;
            secondaryInt = 0;
        }

        public void Set(EventEffectType type, int value)
        {
            effectType = type;
            intValue = value;
            relicReference = null;
            employeeReference = null;
            ClearJobFields();
        }

        public void SetGrantRelic(RelicItem relic)
        {
            effectType = EventEffectType.GrantRelic;
            intValue = 1;
            relicReference = relic;
            employeeReference = null;
            ClearJobFields();
        }

        /// <summary>Grant <paramref name="count"/> stacks of 激励 (same as GrantRelic stacks).</summary>
        public void SetGrantIncentive(RelicItem incentive, int count)
        {
            effectType = EventEffectType.AddChiefIncentive;
            intValue = count;
            relicReference = incentive;
            employeeReference = null;
            ClearJobFields();
        }

        public void SetAddEmployee(EmployeeItem employee, int count)
        {
            effectType = EventEffectType.AddEmployee;
            intValue = count;
            relicReference = null;
            employeeReference = employee;
            ClearJobFields();
        }

        public void SetWarehouseCapacity(int delta)
        {
            effectType = EventEffectType.ModifyWarehouseCapacity;
            intValue = delta;
            relicReference = null;
            employeeReference = null;
            ClearJobFields();
        }

        public void SetModifyJobYieldBonus(JobItem job, float bonus)
        {
            effectType = EventEffectType.ModifyJobYieldBonus;
            jobReference = job;
            floatValue = bonus;
            intValue = 0;
            secondaryInt = 0;
            relicReference = null;
            employeeReference = null;
        }

        public void SetModifyJobMaxWorkers(JobItem job, int delta)
        {
            effectType = EventEffectType.ModifyJobMaxWorkers;
            jobReference = job;
            intValue = delta;
            floatValue = 0f;
            secondaryInt = 0;
            relicReference = null;
            employeeReference = null;
        }

        public void SetModifyJobRawAndColdPerUnit(JobItem job, int rawDelta, int coldDelta)
        {
            effectType = EventEffectType.ModifyJobRawAndColdPerUnit;
            jobReference = job;
            intValue = rawDelta;
            secondaryInt = coldDelta;
            floatValue = 0f;
            relicReference = null;
            employeeReference = null;
        }

        public void SetEnableJobAllFourFlavors(JobItem job)
        {
            effectType = EventEffectType.EnableJobAllFourFlavors;
            jobReference = job;
            intValue = 0;
            secondaryInt = 0;
            floatValue = 0f;
            relicReference = null;
            employeeReference = null;
        }

        public void SetDestroyGatherJob(JobItem job)
        {
            effectType = EventEffectType.DestroyGatherJob;
            jobReference = job;
            intValue = 0;
            secondaryInt = 0;
            floatValue = 0f;
            relicReference = null;
            employeeReference = null;
        }

        public void SetChanceElfDeltaOrJobYield(JobItem job, int elfDelta, float yieldBonus)
        {
            effectType = EventEffectType.ChanceElfDeltaOrJobYield;
            jobReference = job;
            intValue = elfDelta;
            floatValue = yieldBonus;
            secondaryInt = 0;
            relicReference = null;
            employeeReference = null;
        }

        public string ToSummary()
        {
            string jobName = jobReference != null ? jobReference.DisplayName : "岗位";
            switch (effectType)
            {
                case EventEffectType.AddElves:
                    return intValue >= 0 ? $"小精灵 +{intValue}" : $"小精灵 {intValue}";
                case EventEffectType.AddChiefIncentive:
                    return intValue >= 0
                        ? $"激励 ×{intValue}"
                        : $"激励 {intValue}";
                case EventEffectType.GrantRelic:
                    if (relicReference == null)
                        return "获得遗物";
                    return intValue > 1
                        ? $"获得 {relicReference.DisplayName} ×{intValue}"
                        : $"获得 {relicReference.DisplayName}";
                case EventEffectType.AddEmployee:
                {
                    string name = employeeReference != null
                        ? employeeReference.DisplayName
                        : "员工";
                    return intValue >= 0 ? $"{name} +{intValue}" : $"{name} {intValue}";
                }
                case EventEffectType.ModifyWarehouseCapacity:
                    return intValue >= 0
                        ? $"仓库容量 +{intValue}"
                        : $"仓库容量 {intValue}";
                case EventEffectType.RemoveAllFatigue:
                    return "消除所有 疲倦";
                case EventEffectType.ModifyJobYieldBonus:
                    return $"{jobName} 产量 {(floatValue >= 0 ? "+" : "")}{floatValue * 100f:0.#}%";
                case EventEffectType.ModifyJobMaxWorkers:
                    return $"{jobName} 上限 {(intValue >= 0 ? "+" : "")}{intValue}";
                case EventEffectType.ModifyJobRawAndColdPerUnit:
                    return $"{jobName} 食材/单位 {(intValue >= 0 ? "+" : "")}{intValue}，寒冷/单位 {(secondaryInt >= 0 ? "+" : "")}{secondaryInt}";
                case EventEffectType.EnableJobAllFourFlavors:
                    return $"{jobName} 随机风味转为四风味同产";
                case EventEffectType.DestroyGatherJob:
                    return $"永久失去 {jobName}";
                case EventEffectType.ChanceElfDeltaOrJobYield:
                    return $"50% 小精灵 {intValue}，50% {jobName} 产量 +{floatValue * 100f:0.#}%";
                default:
                    return effectType.ToString();
            }
        }

        public void SetRemoveAllFatigue()
        {
            effectType = EventEffectType.RemoveAllFatigue;
            intValue = 0;
            relicReference = null;
            employeeReference = null;
            ClearJobFields();
        }
    }
}
