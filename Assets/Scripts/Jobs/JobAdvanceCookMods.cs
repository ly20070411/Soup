using System.Collections.Generic;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// 烹饪岗沿进阶路径汇总后的数值修正。
    /// </summary>
    public struct JobAdvanceCookMods
    {
        /// <summary>0 = 使用岗位基础每精灵烹饪量。</summary>
        public int CookAmountOverride;

        /// <summary>0 = 使用岗位基础分数倍率。</summary>
        public float ScoreMultiplierOverride;

        public static JobAdvanceCookMods From(JobItem job, JobAdvanceNodeId path)
        {
            var mods = new JobAdvanceCookMods();
            if (job == null || path == JobAdvanceNodeId.None)
                return mods;

            var chain = new List<JobAdvanceNodeId>(JobAdvancePath.MaxDepth);
            JobAdvancePath.GetChain(path, chain);
            for (int i = 0; i < chain.Count; i++)
            {
                var node = job.GetAdvanceNode(chain[i]);
                if (node == null) continue;

                if (node.CookAmountOverride > 0)
                    mods.CookAmountOverride = node.CookAmountOverride;

                if (node.ScoreMultiplierOverride > 0f)
                    mods.ScoreMultiplierOverride = node.ScoreMultiplierOverride;
            }

            return mods;
        }

        public int ResolveAmountPerWorker(JobItem job)
        {
            return CookAmountOverride > 0
                ? CookAmountOverride
                : (job != null ? job.CookAmountPerWorker : 0);
        }

        public float ResolveScoreMultiplier(JobItem job)
        {
            return ScoreMultiplierOverride > 0f
                ? ScoreMultiplierOverride
                : (job != null ? Mathf.Max(0f, job.ScoreMultiplier) : 0f);
        }
    }
}
