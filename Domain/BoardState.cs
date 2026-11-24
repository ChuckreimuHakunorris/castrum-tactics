namespace CastrumTactics.Domain;

/// <summary>An immutable view of a match board at a particular point in time.</summary>
public sealed class BoardState
{
    private const int InitialPlayableGroundHealth = 3;

    private readonly MapDefinition _map;
    private readonly CellState[,] _cells;

    /// <summary>Creates an empty and unowned board from the supplied map.</summary>
    /// <param name="map">The map that supplies dimensions and terrain accessibility.</param>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    /// <remarks>Playable ground starts with health 3. Inaccessible ground starts with health 0.</remarks>
    public BoardState(MapDefinition map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _cells = new CellState[map.Height, map.Width];

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                TerrainType terrain = map.GetTerrain(new BoardPosition(x, y));
                int groundHealth = terrain == TerrainType.Playable ? InitialPlayableGroundHealth : 0;
                _cells[y, x] = new CellState(terrain, PieceType.None, null, groundHealth);
            }
        }
    }

    private BoardState(BoardState previous)
    {
        _map = previous._map;
        _cells = (CellState[,])previous._cells.Clone();
    }

    /// <summary>Gets the number of columns on the board.</summary>
    public int Width => _map.Width;

    /// <summary>Gets the number of rows on the board.</summary>
    public int Height => _map.Height;

    /// <summary>Checks whether a position identifies a cell on the board.</summary>
    /// <param name="position">The position to check.</param>
    /// <returns>True if both coordinates are nonnegative and within the board dimensions.</returns>
    public bool IsWithinBounds(BoardPosition position) => _map.IsWithinBounds(position);

    /// <summary>Gets the immutable snapshot of a cell, including its terrain accessibility.</summary>
    /// <param name="position">The cell position to look up.</param>
    /// <returns>The cell's current snapshot.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the board.</exception>
    public CellState GetCell(BoardPosition position)
    {
        if (!IsWithinBounds(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Position is outside the board.");
        }

        return _cells[position.Y, position.X];
    }

    /// <summary>Checks whether the cell has playable terrain and is free of a grave.</summary>
    /// <param name="position">The cell position to check.</param>
    /// <returns>True if the cell is playable.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The position is outside the board.</exception>
    public bool IsPlayable(BoardPosition position) => GetCell(position).IsPlayable;

    internal IEnumerable<BoardPosition> NeighborsOf(BoardPosition position)
    {
        for (int y = Math.Max(0, position.Y - 1); y <= Math.Min(Height - 1, position.Y + 1); y++)
        {
            for (int x = Math.Max(0, position.X - 1); x <= Math.Min(Width - 1, position.X + 1); x++)
            {
                var neighbor = new BoardPosition(x, y);
                if (neighbor != position)
                {
                    yield return neighbor;
                }
            }
        }
    }

    // Only the rules supply outcomes. Apply them to one copy after the full pair has been validated.
    internal BoardState WithResolvedCells(BoardPosition position, CellState cell,
        (BoardPosition Position, CellState Cell)? second = null)
    {
        var next = new BoardState(this);
        next.SetResolvedCell(position, cell);
        if (second is { } outcome)
        {
            next.SetResolvedCell(outcome.Position, outcome.Cell);
        }
        return next;
    }

    private void SetResolvedCell(BoardPosition position, CellState cell)
    {
        if (GetCell(position).Terrain != cell.Terrain)
        {
            throw new ArgumentException("A resolved cell must preserve the map's terrain.", nameof(cell));
        }

        _cells[position.Y, position.X] = cell;
    }

    internal BoardState WithResolvedCells(IReadOnlyList<(BoardPosition Position, CellState Cell)> outcomes)
    {
        if (outcomes.Count == 0) return this;
        var next = new BoardState(this);
        foreach (var (position, cell) in outcomes)
        {
            next.SetResolvedCell(position, cell);
        }
        return next;
    }
}
