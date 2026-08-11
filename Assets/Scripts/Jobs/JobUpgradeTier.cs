using System;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// One upgrade step for a job/station. Default mechanic: +population.
    /// Extra effect text is designer-authored (see 岗位及效果一览); cook tiers stay empty for now.
    /// </summary>
    [Serializable]
    public class JobUpgradeTier
    {
        [Tooltip("人口上限增量。采集/处理默认 +5；烹饪岗位无人口上限时保持 0。")]
        [SerializeField, Min(0)] private int maxWorkersBonus = 5;

        [Tooltip("该级进阶的额外效果说明（对照岗位及效果一览填写）。")]
        [TextArea(2, 4)]
        [SerializeField] private string effectDescription = string.Empty;

        public int MaxWorkersBonus => maxWorkersBonus;
        public string EffectDescription => effectDescription ?? string.Empty;

        public void SetMaxWorkersBonus(int value) => maxWorkersBonus = Mathf.Max(0, value);

        public void SetEffectDescription(string value) => effectDescription = value ?? string.Empty;

        public string ToSummary(int tierIndex)
        {
            string pop = maxWorkersBonus > 0 ? $"+{maxWorkersBonus} 人口" : "无人口加成";
            if (string.IsNullOrWhiteSpace(effectDescription))
                return $"Lv{tierIndex + 1}: {pop}";
            return $"Lv{tierIndex + 1}: {pop}；{effectDescription.Trim()}";
        }
    }
}
