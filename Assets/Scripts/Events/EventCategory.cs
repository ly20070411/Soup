namespace Soup.Events
{
    /// <summary>
    /// Event classification: 一般事件 / 进阶专属事件.
    /// </summary>
    public enum EventCategory
    {
        /// <summary>一般事件</summary>
        General = 0,
        /// <summary>进阶专属事件（对应岗位进阶一次后才可能出现）</summary>
        AdvancedExclusive = 1
    }
}
