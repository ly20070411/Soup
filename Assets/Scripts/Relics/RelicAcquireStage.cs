namespace Soup.Relics
{
    /// <summary>
    /// How a relic enters the run.
    /// </summary>
    public enum RelicAcquireStage
    {
        /// <summary>Offered as a one-of-three pick at New Game start.</summary>
        Starting = 0,
        /// <summary>Granted by events (or similar rewards).</summary>
        Event = 1
    }
}
