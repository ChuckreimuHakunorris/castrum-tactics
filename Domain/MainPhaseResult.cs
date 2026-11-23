namespace CastrumTactics.Domain;

/// <summary>The final scores and outcome of a completed main phase.</summary>
public readonly record struct MainPhaseResult
{
    internal MainPhaseResult(int playerOneScore, int playerTwoScore)
    {
        PlayerOneScore = playerOneScore;
        PlayerTwoScore = playerTwoScore;
    }

    /// <summary>Gets the number of playable squares secured by Player One.</summary>
    public int PlayerOneScore { get; }

    /// <summary>Gets the number of playable squares secured by Player Two.</summary>
    public int PlayerTwoScore { get; }

    /// <summary>Gets the higher-scoring player, or null for a draw.</summary>
    public PlayerId? Winner => PlayerOneScore == PlayerTwoScore ? null
        : PlayerOneScore > PlayerTwoScore ? PlayerId.PlayerOne : PlayerId.PlayerTwo;
}
