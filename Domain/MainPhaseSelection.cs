namespace CastrumTactics.Domain;

/// <summary>A player's selected square for simultaneous main-phase resolution.</summary>
/// <param name="Player">The player making the selection.</param>
/// <param name="Position">The square selected on the pre-turn board.</param>
public readonly record struct MainPhaseSelection(PlayerId Player, BoardPosition Position);
