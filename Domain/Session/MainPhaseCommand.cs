namespace CastrumTactics.Domain.Session;

/// <summary>Submits or revises one player's selection for a specific main-phase turn.</summary>
/// <param name="Player">The player making the selection.</param>
/// <param name="Position">The requested target on the resolved board.</param>
/// <param name="RoundId">The identifier of the snapshot used to choose the target.</param>
public readonly record struct MainPhaseCommand(PlayerId Player, BoardPosition Position, long RoundId);
