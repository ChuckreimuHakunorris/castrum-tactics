namespace CastrumTactics.Domain.Tests;

internal static class ResolvedBoard
{
    internal static BoardState Parse(params string[] rows)
    {
        int width = rows[0].Length;
        int height = rows.Length;
        var terrain = new TerrainType[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                terrain[y, x] = rows[y][x] == '#' ? TerrainType.Inaccessible : TerrainType.Playable;
            }
        }

        var board = new BoardState(new MapDefinition(width, height, terrain, []));
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                CellState? piece = rows[y][x] switch
                {
                    '.' or '#' => null,
                    '1' => new(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerOne, 3),
                    '2' => new(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerTwo, 3),
                    'A' => new(TerrainType.Playable, PieceType.Castle, PlayerId.PlayerOne, 3),
                    'B' => new(TerrainType.Playable, PieceType.Castle, PlayerId.PlayerTwo, 3),
                    'g' => new(TerrainType.Playable, PieceType.Grave, null, 2),
                    _ => throw new ArgumentException("Unknown test board symbol.", nameof(rows))
                };
                if (piece is { } cell)
                {
                    board = board.WithResolvedCells(new BoardPosition(x, y), cell);
                }
            }
        }
        return board;
    }

    internal static BoardState AtHealth(this BoardState board, BoardPosition position, int health)
    {
        CellState cell = board.GetCell(position);
        return board.WithResolvedCells(position, new CellState(cell.Terrain, cell.Piece, cell.Owner, health));
    }

    internal static CellState[] Cells(this BoardState board) =>
        (from y in Enumerable.Range(0, board.Height)
         from x in Enumerable.Range(0, board.Width)
         select board.GetCell(new BoardPosition(x, y))).ToArray();
}
