namespace CastrumTactics.Domain;

/// <summary>An immutable classification of the cells on one resolved board.</summary>
public sealed class TerritoryAnalysis
{
    private readonly TerritoryControl[,] _cells;

    // The analyzer transfers ownership of its completed grid and never writes to it again.
    internal TerritoryAnalysis(TerritoryControl[,] cells)
    {
        _cells = cells;
        foreach (TerritoryControl control in cells)
        {
            if (control == TerritoryControl.SecuredByPlayerOne) PlayerOneScore++;
            else if (control == TerritoryControl.SecuredByPlayerTwo) PlayerTwoScore++;
        }
    }

    /// <summary>Gets the total number of secured squares for Player One, regardless of their occupants.</summary>
    public int PlayerOneScore { get; }

    /// <summary>Gets the total number of secured squares for Player Two, regardless of their occupants.</summary>
    public int PlayerTwoScore { get; }

    /// <summary>Gets the strategic control of a square independently of its physical occupant.</summary>
    /// <param name="position">The cell to query.</param>
    /// <returns>The cell's classification on the analyzed board.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the board.</exception>
    public TerritoryControl GetControl(BoardPosition position)
    {
        if (position.X < 0 || position.X >= _cells.GetLength(1) ||
            position.Y < 0 || position.Y >= _cells.GetLength(0))
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Position is outside the analyzed board.");
        }

        return _cells[position.Y, position.X];
    }
}
