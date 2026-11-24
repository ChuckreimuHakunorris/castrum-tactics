using CastrumTactics.Domain;

namespace CastrumTactics.AI;

// A per-decision view of local connectivity and piece support, independent of legal move generation.
internal sealed class TacticalBoard
{
    private readonly int _width;
    private readonly int _height;
    private readonly byte[] _oneSupport;
    private readonly byte[] _twoSupport;
    private readonly int[] _regions;
    private readonly List<Region> _regionDetails = [];

    internal TacticalBoard(BoardState board, bool opening, CancellationToken cancellationToken)
    {
        _width = board.Width;
        _height = board.Height;
        Cells = new CellState[checked(_width * _height)];
        _oneSupport = new byte[Cells.Length];
        _twoSupport = new byte[Cells.Length];
        _regions = opening ? new int[Cells.Length] : [];
        Array.Fill(_regions, -1);

        for (int index = 0; index < Cells.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CellState cell = Cells[index] = board.GetCell(Position(index));
            if (cell.IsPlayable)
            {
                PlayableCount++;
            }

            if (cell.Owner is { } owner)
            {
                if (cell.Piece == PieceType.Knight && owner == PlayerId.PlayerOne) OneKnights++;
                if (cell.Piece == PieceType.Knight && owner == PlayerId.PlayerTwo) TwoKnights++;
                byte[] support = owner == PlayerId.PlayerOne ? _oneSupport : _twoSupport;
                foreach (int neighbor in Neighbors(index))
                {
                    support[neighbor]++;
                }
            }
        }

        if (opening)
        {
            FindOpeningRegions(cancellationToken);
        }
    }

    internal CellState[] Cells { get; }
    internal int PlayableCount { get; }
    internal int OneKnights { get; }
    internal int TwoKnights { get; }
    internal int Index(BoardPosition position) => position.Y * _width + position.X;
    internal BoardPosition Position(int index) => new(index % _width, index / _width);
    internal int Support(int index, PlayerId player) => (player == PlayerId.PlayerOne ? _oneSupport : _twoSupport)[index];
    internal Region RegionAt(int index) => _regionDetails[_regions[index]];

    internal IEnumerable<int> Neighbors(int index)
    {
        BoardPosition position = Position(index);
        for (int y = Math.Max(0, position.Y - 1); y <= Math.Min(_height - 1, position.Y + 1); y++)
        {
            for (int x = Math.Max(0, position.X - 1); x <= Math.Min(_width - 1, position.X + 1); x++)
            {
                int neighbor = y * _width + x;
                if (neighbor != index) yield return neighbor;
            }
        }
    }

    // Look for disconnected groups in the adjacent ring. Longer paths outside this ring may still connect them.
    internal double Bottleneck(int index)
    {
        Span<BoardPosition> ring = stackalloc BoardPosition[8];
        int count = 0;
        foreach (int neighbor in Neighbors(index))
        {
            if (Traversable(Cells[neighbor]))
            {
                ring[count++] = Position(neighbor);
            }
        }

        int remaining = (1 << count) - 1;
        int groups = 0;
        while (remaining != 0)
        {
            int connected = remaining & -remaining;
            int frontier = connected;
            groups++;
            while (frontier != 0)
            {
                int next = System.Numerics.BitOperations.TrailingZeroCount((uint)frontier);
                frontier &= frontier - 1;
                for (int i = 0; i < count; i++)
                {
                    int bit = 1 << i;
                    if ((remaining & bit) != 0 && (connected & bit) == 0 &&
                        Math.Abs(ring[i].X - ring[next].X) <= 1 && Math.Abs(ring[i].Y - ring[next].Y) <= 1)
                    {
                        connected |= bit;
                        frontier |= bit;
                    }
                }
            }

            remaining &= ~connected;
        }

        return Math.Clamp((groups - 1) / 2.0, 0, 1);
    }

    private void FindOpeningRegions(CancellationToken token)
    {
        var queue = new List<int>();
        for (int start = 0; start < Cells.Length; start++)
        {
            token.ThrowIfCancellationRequested();
            if (_regions[start] != -1 || !Traversable(Cells[start])) continue;

            queue.Clear();
            queue.Add(start);
            _regions[start] = _regionDetails.Count;
            int one = 0;
            int two = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                int index = queue[i];
                if (Cells[index].Owner == PlayerId.PlayerOne) one++;
                if (Cells[index].Owner == PlayerId.PlayerTwo) two++;
                foreach (int neighbor in Neighbors(index))
                {
                    if (_regions[neighbor] == -1 && Traversable(Cells[neighbor]))
                    {
                        _regions[neighbor] = _regions[start];
                        queue.Add(neighbor);
                    }
                }
            }

            _regionDetails.Add(new Region(queue.Count, one, two));
        }
    }

    private static bool Traversable(CellState cell) => cell.IsPlayable && cell.Piece != PieceType.Castle;

    internal readonly record struct Region(int Size, int One, int Two)
    {
        internal int Count(PlayerId player) => player == PlayerId.PlayerOne ? One : Two;
    }
}
