namespace CastrumTactics.Domain;

/// <summary>An immutable classification of the cells on one resolved board.</summary>
public sealed class TerritoryAnalysis
{
    private readonly TerritoryControl[,] _cells;

    // The analyzer transfers ownership of its completed grid and never writes to it again.
    internal TerritoryAnalysis(TerritoryControl[,] cells) => _cells = cells;

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
