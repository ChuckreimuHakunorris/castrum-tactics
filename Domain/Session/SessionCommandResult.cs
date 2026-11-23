namespace CastrumTactics.Domain.Session;

/// <summary>The result of a session command. Rejected commands leave the session unchanged.</summary>
public enum SessionCommandResult
{
    /// <summary>The command was applied. The session publishes the resulting snapshot.</summary>
    Accepted,

    /// <summary>The command did not identify either participant.</summary>
    InvalidPlayer,

    /// <summary>The command referred to a round that is no longer current or has not begun.</summary>
    StaleRound,

    /// <summary>The command is unavailable in the current match phase.</summary>
    WrongPhase,

    /// <summary>The position is unavailable to this player in the current round.</summary>
    IneligiblePosition
}
