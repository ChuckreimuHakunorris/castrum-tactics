using CastrumTactics.Domain;

namespace CastrumTactics.AI;

// A per-decision view of local connectivity and piece support, independent of legal move generation.
internal sealed class TacticalBoard
{
    private readonly int _width;
    private readonly int _height;
    private readonly byte[] _oneSupport;
    private readonly byte[] _twoSupport;
    private readonly RuleSetDefinition _rules;
    private readonly int[] _regions;
    private readonly bool _countCastleBorders;
    private readonly List<Region> _regionDetails = [];

    internal TacticalBoard(BoardState board, bool opening, CancellationToken cancellationToken, RuleSet ruleSet = RuleSet.Classic)
    {
        _rules = RuleSetDefinition.Get(ruleSet);
        _countCastleBorders = opening && _rules.StartingPiece == PieceType.Castle;
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
                if (cell.Piece == PieceType.Knight || _countCastleBorders) StartingPieceCount++;
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
    internal int StartingPieceCount { get; }
    internal int Index(BoardPosition position) => position.Y * _width + position.X;
    internal BoardPosition Position(int index) => new(index % _width, index / _width);
    internal int Support(int index, PlayerId player) => (player == PlayerId.PlayerOne ? _oneSupport : _twoSupport)[index];
    internal Region RegionAt(int index) => _regionDetails[_regions[index]];

    internal IEnumerable<int> Supporters(int index, PlayerId player)
    {
        int range = Math.Max(_rules.KnightSupportRange, _rules.CastleSupportRange);
        BoardPosition target = Position(index);
        foreach (int neighbor in Neighbors(index, range))
        {
            CellState source = Cells[neighbor];
            if (source.Owner != player) continue;
            BoardPosition origin = Position(neighbor);
            int distance = Math.Max(Math.Abs(origin.X - target.X), Math.Abs(origin.Y - target.Y));
            if (distance <= _rules.SurvivalSupportRange(source.Piece)) yield return neighbor;
        }
    }

    internal int SurvivalSupport(int index, PlayerId player) => _rules.KnightSupportRange == 1 && _rules.CastleSupportRange == 1
        ? Support(index, player) : Supporters(index, player).Count();

    internal Neighborhood SupportedTargets(int index, PieceType source) =>
        Neighbors(index, _rules.SurvivalSupportRange(source));

    internal bool InSupportRange(int source, int target, PieceType piece)
    {
        var a = Position(source);
        var b = Position(target);
        return source != target && Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= _rules.SurvivalSupportRange(piece);
    }

    internal Neighborhood Neighbors(int index, int range = 1) => new(_width, _height, index, range);

    // Ranking walks this window several times for each candidate. Keep its iterator on the stack.
    internal readonly record struct Neighborhood(int Width, int Height, int Center, int Range)
    {
        public Enumerator GetEnumerator() => new(Width, Height, Center, Range);

        internal struct Enumerator
        {
            private readonly int _width, _center, _left, _right, _bottom;
            private int _x, _y;

            internal Enumerator(int width, int height, int center, int range)
            {
                _width = width;
                _center = center;
                _left = Math.Max(0, center % width - range);
                _right = Math.Min(width - 1, center % width + range);
                _bottom = Math.Min(height - 1, center / width + range);
                _x = _left - 1;
                _y = Math.Max(0, center / width - range);
            }

            public int Current => _y * _width + _x;

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
                while (_y <= _bottom && Current == _center);
                return _y <= _bottom;
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
        var castleBorders = new HashSet<int>();
        for (int start = 0; start < Cells.Length; start++)
        {
            token.ThrowIfCancellationRequested();
            if (_regions[start] != -1 || !Traversable(Cells[start])) continue;

            queue.Clear();
            castleBorders.Clear();
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
                    // A castle supplies influence to each component it borders without joining those components.
                    if (_countCastleBorders && Cells[neighbor].Piece == PieceType.Castle && castleBorders.Add(neighbor))
                    {
                        if (Cells[neighbor].Owner == PlayerId.PlayerOne) one++;
                        if (Cells[neighbor].Owner == PlayerId.PlayerTwo) two++;
                    }

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
