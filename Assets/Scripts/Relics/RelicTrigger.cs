namespace Soup.Relics
{
    /// <summary>
    /// When during a turn / run a relic rule is evaluated.
    /// </summary>
    public enum RelicTrigger
    {
        AfterGather = 0,
        BeforeSpicy = 1,
        AfterScore = 2,
        /// <summary>Immediately when the relic is acquired.</summary>
        OnAcquire = 3,
        /// <summary>Start of a production turn, before gather.</summary>
        TurnStart = 4,
        /// <summary>When a campaign level begins.</summary>
        LevelStart = 5,
        /// <summary>After process jobs finish producing.</summary>
        AfterProcess = 6,
        /// <summary>
        /// Not run via RelicEffectRunner.Run; queried for passive labor / loss hooks.
        /// </summary>
        Passive = 7
    }
}
