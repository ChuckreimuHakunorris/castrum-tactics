using System;
using System.Collections.Generic;
using System.Reflection;
using CastrumTactics.Domain;

namespace CastrumTactics.RenderingBenchmarks;

internal static class RenderBenchmarkFixtures
{
    private static readonly string[] Dense =
    [
        "RRRR......BB", "RRRR....BBBB", "RRRR....BBBB", "R..r....b..B",
        "..r.r..b.b..", "....r..b....", "....r..b....", "..r.r..b.b..",
        "R..r....b..B", "RRRR....BBBB", "RRRR....BBBB", "RR......BBBB",
    ];

    internal static MainPhaseState Create(string scenario)
    {
        var terrain = new TerrainType[12, 12];
        var cells = new List<(BoardPosition, CellState)>();
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 12; x++)
            {
                terrain[y, x] = TerrainType.Playable;
                char mark = Dense[y][x];
                if (scenario == "targets")
                    mark = (x % 2, y % 2) == (1, 1) ? (x / 2 + y / 2) % 2 == 0 ? 'r' : 'b' : '.';
                if (scenario == "damage" && mark == '.' && (x + y) % 4 == 0) mark = 'g';

                PieceType piece = mark switch
                {
                    'R' or 'B' => PieceType.Castle,
                    'r' or 'b' => PieceType.Knight,
                    'g' => PieceType.Grave,
                    _ => PieceType.None,
                };
                PlayerId? owner = mark switch
                {
                    'R' or 'r' => PlayerId.PlayerOne,
                    'B' or 'b' => PlayerId.PlayerTwo,
                    _ => null,
                };
                int health = scenario == "damage" ? (x + y) % 3 : 3;
                if (piece != PieceType.None || health < 3)
                    cells.Add((new BoardPosition(x, y), new CellState(TerrainType.Playable, piece, owner, health)));
            }

        // The synthetic board uses the same immutable cell factory as resolution without widening the Domain API.
        var fixture = new BoardState(new MapDefinition(12, 12, terrain, []));
        var resolve = typeof(BoardState).GetMethod("WithResolvedCells", BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(IReadOnlyList<(BoardPosition, CellState)>)], null)!;
        var board = (BoardState)resolve.Invoke(fixture, [cells])!;
        return MainPhaseState.Evaluate(board);
    }
}
