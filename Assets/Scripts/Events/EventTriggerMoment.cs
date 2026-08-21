namespace Soup.Events
{
    /// <summary>
    /// When an event may be considered for presentation.
    /// </summary>
    public enum EventTriggerMoment
    {
        /// <summary>After a production turn resolves (legacy random).</summary>
        AfterTurn = 0,
        /// <summary>Only via debug / explicit Present call.</summary>
        ManualOnly = 1,
        /// <summary>After 大关结算 — stage event pair pool.</summary>
        AfterStage = 2
    }
}
