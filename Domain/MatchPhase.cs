namespace CastrumTactics.Domain;

/// <summary>Identifies the current stage of a match.</summary>
public enum MatchPhase
{
    /// <summary>Both players can submit starting positions for the current round.</summary>
    Opening,

    /// <summary>Both players can submit actions for the current main-phase turn.</summary>
    Main,

    /// <summary>The opening ended without any starting pieces and can be restarted locally.</summary>
    OpeningRestartRequired,

    /// <summary>The match ended because a player has no legal main-phase selections.</summary>
    Finished
}
