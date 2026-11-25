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

    internal Neighborhood NeighborsOf(BoardPosition position, int range = 1) => new(this, position, range);

    // Territory and support traverse this window for every simulated pair. A value iterator avoids a heap object per cell.
    internal readonly struct Neighborhood(BoardState board, BoardPosition center, int range)
    {
        public Enumerator GetEnumerator() => new(board, center, range);

        public struct Enumerator
        {
            private readonly BoardPosition _center;
            private readonly int _left, _right, _bottom;
            private int _x, _y;

            internal Enumerator(BoardState board, BoardPosition center, int range)
            {
                _center = center;
                _left = Math.Max(0, center.X - range);
                _right = Math.Min(board.Width - 1, center.X + range);
                _bottom = Math.Min(board.Height - 1, center.Y + range);
                _x = _left - 1;
                _y = Math.Max(0, center.Y - range);
            }

            public BoardPosition Current => new(_x, _y);

            public bool MoveNext()
            {
                do
                {
                    if (++_x > _right)
                    {
                        _x = _left;
                        _y++;
                    }
                }
                while (_x == _center.X && _y == _center.Y);
                return _y <= _bottom;
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
