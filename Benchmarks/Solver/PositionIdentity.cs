using CastrumTactics.Domain;

namespace CastrumTactics.Development.Solver;

internal static class PositionIdentity
{
    internal const int Version = 1;

    // Three UTF-16 code units per square encode all CellState values, including arbitrary int health.
    // Ordinal string equality compares the complete encoding even when dictionary hashes collide.
    internal static string Key(BoardState board, RuleSet rules, bool ignoreLockedHealth = false)
    {
        int width = board.Width, height = board.Height;
        int size = checked(5 + 3 * width * height);
        Span<char> key = size <= 512 ? stackalloc char[size] : new char[size];
        key[0] = (char)rules;
        key[1] = (char)width;
        key[2] = (char)(width >> 16);
        key[3] = (char)height;
        key[4] = (char)(height >> 16);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var cell = board.GetCell(new(x, y));
            int offset = 5 + 3 * (y * width + x);
            key[offset] = (char)CellBits(cell);
            int health = ignoreLockedHealth && cell.Piece is PieceType.Castle or PieceType.Grave ? 0 : cell.GroundHealth;
            key[offset + 1] = (char)health;
            key[offset + 2] = (char)(health >> 16);
        }
        return new(key);
    }

    internal static int CellBits(CellState cell) =>
        (int)cell.Terrain | ((int)cell.Piece << 1) | ((int)(cell.Owner ?? 0) << 3);

    internal static BoardState Materialize(string key, BoardState template, BoardTransform toCanonical)
    {
        int width = template.Width, height = template.Height;
        var outcomes = new (BoardPosition, CellState)[checked(width * height)];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var position = new BoardPosition(x, y);
            var from = toCanonical.Map(position, width, height);
            int offset = 5 + 3 * (from.Y * width + from.X);
            int combined = key[offset];
            var terrain = (TerrainType)(combined & 1);
            var piece = (PieceType)((combined >> 1) & 3);
            int owner = combined >> 3;
            if (toCanonical.Exchange && owner != 0) owner ^= 3;
            int health = key[offset + 1] | (key[offset + 2] << 16);
            outcomes[y * width + x] = (position, new(terrain, piece, owner == 0 ? null : (PlayerId)owner, health));
        }
        return template.WithResolvedCells(outcomes);
    }

    internal static BoardPosition Transform(BoardPosition p, int width, int height, int transform)
    {
        int x = p.X, y = p.Y;
        if ((transform & 4) != 0) (x, y) = (y, x);
        if ((transform & 1) != 0) x = width - 1 - x;
        if ((transform & 2) != 0) y = height - 1 - y;
        return new(x, y);
    }

    internal static long RemainingWork(BoardState board)
    {
        long remaining = 0;
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            var cell = board.GetCell(new(x, y));
            if (cell.Terrain == TerrainType.Inaccessible) continue;
            remaining += cell.Piece switch
            {
                PieceType.None => cell.GroundHealth + 2L,
                PieceType.Knight => cell.GroundHealth + 1L,
                _ => 0
            };
        }
        return remaining;
    }
}
