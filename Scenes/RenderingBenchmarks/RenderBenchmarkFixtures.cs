using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.RenderingBenchmarks;

internal static class RenderBenchmarkFixtures
{
    private static readonly string[] Dense =
    [
        "RRRR......BB", "RRRR....BBBB", "RRRR....BBBB", "R..r....b..B",
        "..r.r..b.b..", "....r..b....", "....r..b....", "..r.r..b.b..",
        "R..r....b..B", "RRRR....BBBB", "RRRR....BBBB", "RR......BBBB",
    ];

    internal static bool IsSynthetic(string scenario) => scenario is "castles" or "damage-extreme" or "targets";

    internal static MainPhaseState ReplayHeavyDamage()
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("great-divide").Definition;
        GameSessionSnapshot draft = new LocalGameSession(map, RuleSet.Classic).Current;
        while (draft.Phase == MatchPhase.Opening)
            draft = draft.PreviewDraft(draft.EligiblePositions[0], draft.EligiblePositions[1]);

        var state = draft.MainState!;
        var random = new Random(163);
        var best = state;
        (bool AllOccupants, int KnightWear) bestCoverage = (false, -1);
        // Preserve an active midgame with accumulated wear, before either side runs out of legal moves.
        for (int turn = 0; turn < 96 && !state.IsFinished; turn++)
        {
            int damage = 0;
            bool wornCastle = false, wornGrave = false, wornOne = false, wornTwo = false;
            for (int y = 0; y < state.Board.Height; y++)
                for (int x = 0; x < state.Board.Width; x++)
                {
                    var cell = state.Board.GetCell(new BoardPosition(x, y));
                    int wear = TerrainCatalog.Damage(cell);
                    if (wear == 0) continue;
                    wornCastle |= cell.Piece == PieceType.Castle;
                    wornGrave |= cell.Piece == PieceType.Grave;
                    if (cell.Piece != PieceType.Knight) continue;
                    damage += wear;
                    wornOne |= cell.Owner == PlayerId.PlayerOne;
                    wornTwo |= cell.Owner == PlayerId.PlayerTwo;
                }
            var coverage = (wornCastle && wornGrave && wornOne && wornTwo, damage);
            if (coverage.CompareTo(bestCoverage) > 0)
            {
                best = state;
                bestCoverage = coverage;
            }

            var first = Pick(PlayerId.PlayerOne);
            var second = Pick(PlayerId.PlayerTwo);
            state = state.Advance(new(PlayerId.PlayerOne, first), new(PlayerId.PlayerTwo, second));
        }

        return best;

        BoardPosition Pick(PlayerId player)
        {
            var targets = state.GetLegalSelections(player).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            var captures = targets.Where(p => state.Board.GetCell(p) is { Piece: PieceType.Knight, Owner: { } owner } &&
                owner != player).ToArray();
            var candidates = captures.Length > 0 && random.Next(4) != 0 ? captures : targets;
            return candidates[random.Next(candidates.Length)];
        }
    }

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
                if (scenario == "damage-extreme" && mark == '.' && (x + y) % 4 == 0) mark = 'g';

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
                int health = scenario == "damage-extreme" ? (x + y) % 3 : 3;
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
