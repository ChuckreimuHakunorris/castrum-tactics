namespace CastrumTactics.Domain.Session;

/// <summary>Accepts gameplay commands and exposes resolved match state to presentation.</summary>
/// <remarks>Call commands serially. State-change subscribers may read state and should defer further commands.</remarks>
public interface IGameSession
{
    /// <summary>Gets the latest public state of the match.</summary>
    GameSessionSnapshot Current { get; }

    /// <summary>Publishes a snapshot after each accepted command. Rejections leave the snapshot unchanged.</summary>
    event Action<GameSessionSnapshot>? StateChanged;

    /// <summary>Submits or revises a choice. The second player's submission resolves the round immediately.</summary>
    /// <param name="selection">The player, position, and round to submit.</param>
    /// <returns>Whether the command was accepted or the reason it was rejected.</returns>
    SessionCommandResult SubmitOpeningSelection(OpeningSelection selection);

    /// <summary>Starts a fresh draft on the same map when the opening requires a restart.</summary>
    /// <param name="roundId">The identifier of the snapshot that reported the restart requirement.</param>
    /// <returns>Whether the command was accepted or the reason it was rejected.</returns>
    SessionCommandResult RestartDraft(long roundId);
}
