namespace Soup.Levels
{
    /// <summary>Current level win/lose state.</summary>
    public enum LevelOutcome
    {
        InProgress = 0,
        Won = 1,
        Lost = 2
    }

    /// <summary>操作面板调试：胜利结算类型。</summary>
    public enum DebugCampaignVictoryKind
    {
        Normal,
        Challenge,
        UltimateChallenge
    }
}
