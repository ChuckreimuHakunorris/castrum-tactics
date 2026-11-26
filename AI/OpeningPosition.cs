using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.AI;

// A draft-specific projection. Paths are opportunities through empty terrain, not legal move proofs.
internal sealed class OpeningPosition
{
    private readonly BoardPosition[] _positions;
    private readonly int[][] _neighbors;
    private readonly bool[] _empty;
    private readonly int[][] _reach;
    private readonly bool[] _remaining;
    private readonly int[][] _queues;
    private readonly int _width;
    private readonly int _playable;

    internal OpeningPosition(BoardState board)
    {
        _width = board.Width;
        int area = checked(board.Width * board.Height);
        _positions = Enumerable.Range(0, area).Select(i => new BoardPosition(i % _width, i / _width)).ToArray();
        _neighbors = _positions.Select(p => Neighbors(p, board.Height).ToArray()).ToArray();
        _empty = new bool[area];
        _remaining = new bool[area];
        _reach = [new int[area], new int[area]];
        _queues = [new int[area], new int[area]];
        _playable = _positions.Count(p => board.GetCell(p).Terrain == TerrainType.Playable);

        IEnumerable<int> Neighbors(BoardPosition p, int height)
        {
            for (int y = Math.Max(0, p.Y - 1); y <= Math.Min(height - 1, p.Y + 1); y++)
            for (int x = Math.Max(0, p.X - 1); x <= Math.Min(_width - 1, p.X + 1); x++)
                if (x != p.X || y != p.Y) yield return y * _width + x;
        }
    }

    internal Features Evaluate(GameSessionSnapshot state, CancellationToken token) =>
        Evaluate(state.Board, state.Territory, state.EligiblePositions, token);

    internal Features Evaluate(BoardState board, TerritoryAnalysis? territory,
        IReadOnlyList<BoardPosition> remaining, CancellationToken token)
    {
        int one = 0, two = 0;
        int material = 0;
        Array.Fill(_reach[0], int.MaxValue);
        Array.Fill(_reach[1], int.MaxValue);
        Array.Clear(_remaining);
        foreach (var p in remaining)
        {
            _remaining[Index(p)] = true;
            foreach (int cell in _neighbors[Index(p)]) _remaining[cell] = true;
        }
        for (int i = 0; i < _positions.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            CellState cell = board.GetCell(_positions[i]);
            _empty[i] = cell.IsPlayable && cell.Piece == PieceType.None;
            if (cell.Owner == PlayerId.PlayerOne) { _queues[0][one++] = i; _reach[0][i] = 0; material++; }
            if (cell.Owner == PlayerId.PlayerTwo) { _queues[1][two++] = i; _reach[1][i] = 0; material--; }
        }

        Spread(_queues[0], one, _reach[0], token);
        Spread(_queues[1], two, _reach[1], token);

        double coverage = 0, footholds = 0, accessible = 0, openRoutes = 0;
        for (int i = 0; i < _positions.Length; i++)
        {
            if (!_empty[i]) continue;
            int a = _reach[0][i], b = _reach[1][i];
            if (a == b) continue;
            double priority = _remaining[i] ? 0.45 : 1;
            double first = a == int.MaxValue ? 0 : 1.0 / (1 + 0.45 * (a - 1));
            double second = b == int.MaxValue ? 0 : 1.0 / (1 + 0.45 * (b - 1));
            // Only the earliest route from each side counts, so extra castles earn marginal coverage.
            coverage += priority * (first - second);
            accessible += priority * ((a == int.MaxValue ? 0 : 1) - (b == int.MaxValue ? 0 : 1));
            footholds += (a == 1 ? 1 : 0) - (b == 1 ? 1 : 0);
            if (a == 1 || b == 1)
            {
                int supports = 0;
                foreach (int neighbor in _neighbors[i])
                    if (_empty[neighbor]) supports++;
                openRoutes += ((a == 1 ? 1 : 0) - (b == 1 ? 1 : 0)) * supports / 8.0;
            }
        }

        return new(territory is null ? 0 : territory.PlayerOneScore - territory.PlayerTwoScore,
            coverage, accessible, footholds, openRoutes, material, _playable);
    }

    private int Index(BoardPosition p) => p.Y * _width + p.X;

    private void Spread(int[] queue, int end, int[] distance, CancellationToken token)
    {
        for (int head = 0; head < end; head++)
        {
            token.ThrowIfCancellationRequested();
            int i = queue[head];
            foreach (int j in _neighbors[i])
            {
                if (!_empty[j] || distance[j] != int.MaxValue) continue;
                distance[j] = distance[i] + 1;
                queue[end++] = j;
            }
        }
    }

    internal readonly record struct Features(double Secured, double Coverage, double Accessible, double Footholds,
        double OpenRoutes, double Material, int Playable)
    {
        internal double Value(double synergy)
        {
            double span = Math.Max(4, Math.Sqrt(Playable));
            double score = 0.8 * Secured + 0.8 * Material + 0.18 * Footholds + 0.12 * OpenRoutes +
                synergy * (0.65 * Coverage + 0.24 * Accessible);
            return 0.85 * score / (span + Math.Abs(score));
        }
    }
}
