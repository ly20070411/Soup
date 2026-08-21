using System.Collections.Generic;
using Soup.Items;
using UnityEngine;

namespace Soup.Jobs
{
    /// <summary>
    /// 处理岗沿进阶路径汇总后的数值修正。
    /// </summary>
    public struct JobAdvanceProcessMods
    {
        /// <summary>0 = 使用岗位基础每精灵处理量。</summary>
        public int ProcessAmountOverride;

        /// <summary>0 = 使用岗位基础其他材质效率。</summary>
        public float OtherMaterialEfficiencyOverride;

        public IngredientMaterial MaterialRefundMaterial;
        public int MaterialRefundPerProcessedThreshold;
        public int MaterialRefundPerProcessedAmount;
        public int ProcessedRefundPerProcessedThreshold;
        public int ProcessedRefundPerProcessedAmount;
        public float ProcessedOutputWasteFraction;

        public static JobAdvanceProcessMods From(JobItem job, JobAdvanceNodeId path)
        {
            var mods = new JobAdvanceProcessMods();
            if (job == null || path == JobAdvanceNodeId.None)
                return mods;

            var chain = new List<JobAdvanceNodeId>(JobAdvancePath.MaxDepth);
            JobAdvancePath.GetChain(path, chain);
            for (int i = 0; i < chain.Count; i++)
            {
                var node = job.GetAdvanceNode(chain[i]);
                if (node == null) continue;

                if (node.ProcessAmountOverride > 0)
                    mods.ProcessAmountOverride = node.ProcessAmountOverride;

                if (node.OtherMaterialEfficiencyOverride > 0f)
                    mods.OtherMaterialEfficiencyOverride = node.OtherMaterialEfficiencyOverride;

                if (node.MaterialRefundPerProcessedThreshold > 0 && node.MaterialRefundPerProcessedAmount > 0)
                {
                    mods.MaterialRefundMaterial = node.MaterialRefundMaterial;
                    mods.MaterialRefundPerProcessedThreshold = node.MaterialRefundPerProcessedThreshold;
                    mods.MaterialRefundPerProcessedAmount = node.MaterialRefundPerProcessedAmount;
                }

                if (node.ProcessedRefundPerProcessedThreshold > 0 && node.ProcessedRefundPerProcessedAmount > 0)
                {
                    mods.ProcessedRefundPerProcessedThreshold = node.ProcessedRefundPerProcessedThreshold;
                    mods.ProcessedRefundPerProcessedAmount = node.ProcessedRefundPerProcessedAmount;
                }

                if (node.ProcessedOutputWasteFraction > 0f)
                    mods.ProcessedOutputWasteFraction = node.ProcessedOutputWasteFraction;
            }

            return mods;
        }

        public int ResolveAmountPerWorker(JobItem job)
        {
            return ProcessAmountOverride > 0
                ? ProcessAmountOverride
                : (job != null ? job.ProcessAmountPerWorker : 0);
        }

        public float ResolveOtherMaterialEfficiency(JobItem job)
        {
            if (OtherMaterialEfficiencyOverride > 0f)
                return Mathf.Clamp01(OtherMaterialEfficiencyOverride);
            return job != null ? Mathf.Clamp01(job.OtherMaterialEfficiency) : 0f;
        }
    }
}
