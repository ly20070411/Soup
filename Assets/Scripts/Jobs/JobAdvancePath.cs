using System.Collections.Generic;

namespace Soup.Jobs
{
    /// <summary>
    /// 进阶树路径规则：互斥分支、深度、可选子节点。
    /// </summary>
    public static class JobAdvancePath
    {
        public const int MaxDepth = 2;

        public static int Depth(JobAdvanceNodeId id)
        {
            switch (id)
            {
                case JobAdvanceNodeId.Path1:
                case JobAdvanceNodeId.Path2:
                    return 1;
                case JobAdvanceNodeId.Path1_1:
                case JobAdvanceNodeId.Path1_2:
                case JobAdvanceNodeId.Path2_1:
                case JobAdvanceNodeId.Path2_2:
                    return 2;
                default:
                    return 0;
            }
        }

        public static string ToLabel(JobAdvanceNodeId id)
        {
            switch (id)
            {
                case JobAdvanceNodeId.Path1: return "1";
                case JobAdvanceNodeId.Path2: return "2";
                case JobAdvanceNodeId.Path1_1: return "1-1";
                case JobAdvanceNodeId.Path1_2: return "1-2";
                case JobAdvanceNodeId.Path2_1: return "2-1";
                case JobAdvanceNodeId.Path2_2: return "2-2";
                default: return "-";
            }
        }

        public static JobAdvanceNodeId Parent(JobAdvanceNodeId id)
        {
            switch (id)
            {
                case JobAdvanceNodeId.Path1_1:
                case JobAdvanceNodeId.Path1_2:
                    return JobAdvanceNodeId.Path1;
                case JobAdvanceNodeId.Path2_1:
                case JobAdvanceNodeId.Path2_2:
                    return JobAdvanceNodeId.Path2;
                case JobAdvanceNodeId.Path1:
                case JobAdvanceNodeId.Path2:
                    return JobAdvanceNodeId.None;
                default:
                    return JobAdvanceNodeId.None;
            }
        }

        public static bool IsLeaf(JobAdvanceNodeId id) => Depth(id) >= MaxDepth;

        public static bool IsValid(JobAdvanceNodeId id) =>
            id == JobAdvanceNodeId.None
            || id == JobAdvanceNodeId.Path1
            || id == JobAdvanceNodeId.Path2
            || id == JobAdvanceNodeId.Path1_1
            || id == JobAdvanceNodeId.Path1_2
            || id == JobAdvanceNodeId.Path2_1
            || id == JobAdvanceNodeId.Path2_2;

        /// <summary>当前节点下一次可进阶的两个选项；已满级时返回 false。</summary>
        public static bool TryGetChoices(JobAdvanceNodeId current, out JobAdvanceNodeId a, out JobAdvanceNodeId b)
        {
            switch (current)
            {
                case JobAdvanceNodeId.None:
                    a = JobAdvanceNodeId.Path1;
                    b = JobAdvanceNodeId.Path2;
                    return true;
                case JobAdvanceNodeId.Path1:
                    a = JobAdvanceNodeId.Path1_1;
                    b = JobAdvanceNodeId.Path1_2;
                    return true;
                case JobAdvanceNodeId.Path2:
                    a = JobAdvanceNodeId.Path2_1;
                    b = JobAdvanceNodeId.Path2_2;
                    return true;
                default:
                    a = JobAdvanceNodeId.None;
                    b = JobAdvanceNodeId.None;
                    return false;
            }
        }

        public static void GetChoices(JobAdvanceNodeId current, List<JobAdvanceNodeId> results)
        {
            results?.Clear();
            if (results == null) return;
            if (!TryGetChoices(current, out var a, out var b)) return;
            results.Add(a);
            results.Add(b);
        }

        public static bool IsValidNext(JobAdvanceNodeId current, JobAdvanceNodeId choice)
        {
            if (!TryGetChoices(current, out var a, out var b)) return false;
            return choice == a || choice == b;
        }

        /// <summary>从根到 <paramref name="id"/> 的节点链（不含 None）。</summary>
        public static void GetChain(JobAdvanceNodeId id, List<JobAdvanceNodeId> results)
        {
            results?.Clear();
            if (results == null || id == JobAdvanceNodeId.None) return;

            var stack = new List<JobAdvanceNodeId>(MaxDepth);
            var cursor = id;
            while (cursor != JobAdvanceNodeId.None)
            {
                stack.Add(cursor);
                cursor = Parent(cursor);
            }

            for (int i = stack.Count - 1; i >= 0; i--)
                results.Add(stack[i]);
        }

        public static bool HasTaken(JobAdvanceNodeId current, JobAdvanceNodeId node)
        {
            if (node == JobAdvanceNodeId.None) return true;
            if (current == JobAdvanceNodeId.None) return false;

            var cursor = current;
            while (cursor != JobAdvanceNodeId.None)
            {
                if (cursor == node) return true;
                cursor = Parent(cursor);
            }

            return false;
        }
    }
}
