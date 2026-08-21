namespace Soup.Jobs
{
    /// <summary>
    /// 岗位进阶树节点。一阶二选一：1 / 2；二阶在已选一阶上再二选一：1-1 / 1-2 或 2-1 / 2-2。
    /// </summary>
    public enum JobAdvanceNodeId
    {
        None = 0,
        Path1 = 1,
        Path2 = 2,
        Path1_1 = 11,
        Path1_2 = 12,
        Path2_1 = 21,
        Path2_2 = 22
    }
}
