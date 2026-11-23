namespace CastrumTactics.Domain;

/// <summary>Identifies the current stage of a match.</summary>
public enum MatchPhase
{
    /// <summary>Both players can submit starting positions for the current round.</summary>
    Opening,

    /// <summary>The opening ended with starting knights for both players.</summary>
    Main,

    /// <summary>The opening ended without starting knights and can be restarted locally.</summary>
    OpeningRestartRequired
}
