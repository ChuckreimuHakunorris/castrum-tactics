namespace CastrumTactics.Domain.Session;

/// <summary>Accepts gameplay commands and exposes resolved match state to presentation.</summary>
/// <remarks>Call commands serially on the session’s owning thread. State-change subscribers may read state
/// and should defer further commands. A round identifier is meaningful only within its originating session.</remarks>
public interface IGameSession
{
    /// <summary>Gets the latest public state of the match.</summary>
    GameSessionSnapshot Current { get; }

    /// <summary>Publishes a snapshot after each accepted command. Rejections leave the snapshot unchanged.</summary>
    event Action<GameSessionSnapshot>? StateChanged;

    /// <summary>Submits or revises a choice. The second player's submission resolves the round immediately.</summary>
    /// <remarks>If one eligible start remains after resolution, it is resolved as a collision before the next snapshot.</remarks>
    /// <param name="selection">The player, position, and round to submit.</param>
    /// <returns>Whether the command was accepted or the reason it was rejected.</returns>
    SessionCommandResult SubmitOpeningSelection(OpeningSelection selection);

    /// <summary>Submits or revises a main-phase choice. Both players' choices resolve together when the second player submits.</summary>
    /// <param name="command">The player, position, and round to submit.</param>
    /// <returns>Whether the command was accepted or the reason it was rejected.</returns>
    SessionCommandResult SubmitMainPhaseSelection(MainPhaseCommand command);

    /// <summary>Starts a fresh draft under the same map and ruleset after a finished match or an opening without starting pieces.</summary>
    /// <remarks>Restores the initial board, clears match progress, and publishes a snapshot with an advanced round identifier.</remarks>
    /// <param name="roundId">The identifier of the current Finished or OpeningRestartRequired snapshot.</param>
    /// <returns>Accepted on restart, StaleRound if the identifier differs, or WrongPhase during active play.</returns>
    SessionCommandResult RestartDraft(long roundId);
}
