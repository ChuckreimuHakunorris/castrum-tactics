namespace CastrumTactics.Domain.Session;

/// <summary>Submits or revises one player's choice for a specific opening round.</summary>
/// <param name="Player">The player making the selection.</param>
/// <param name="Position">The requested starting position.</param>
/// <param name="RoundId">The round identifier from the session snapshot used to choose the position.</param>
public readonly record struct OpeningSelection(PlayerId Player, BoardPosition Position, long RoundId);
