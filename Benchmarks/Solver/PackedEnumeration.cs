using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using CastrumTactics.Domain;

namespace CastrumTactics.Development.Solver;

// Values are irrelevant to physical counting. Store only packed identities, integer node IDs and
// compact per-node statistics. The domain still evaluates and resolves every visited representative.
internal sealed class PackedEnumeration(MainPhaseState root, SolveLimits limits, bool useSymmetry, CancellationToken token, bool ignoreLockedHealth = false)
{
    private readonly Dictionary<ulong, int> _ids = [];
    private readonly Blocks<Node> _nodes = new();
    private readonly Blocks<int> _edges = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly BoardSymmetry _symmetry = BoardSymmetry.ForRoot(root, useSymmetry);
    private readonly int _width = root.Board.Width;
    private readonly int _height = root.Board.Height;
    private int _expanded;

    internal int Visited => _ids.Count;
    internal int Expanded => _expanded;
    internal long Pairs { get; private set; }
    internal TimeSpan Elapsed => _clock.Elapsed;

    internal static bool Supports(BoardState board)
    {
        if ((long)board.Width * board.Height > 9) return false;
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
            if (board.GetCell(new(x,y)).GroundHealth > 3) return false;
        return true;
    }

    internal void Run()
    {
        CheckLimits();
        Visit(root.Board, Pack(root.Board), PositionIdentity.RemainingWork(root.Board));
        // Depth propagation needs only node IDs and edges.
        _ids.Clear();
        _ids.TrimExcess();
    }

    private int Visit(BoardState board, ulong raw, long rank)
    {
        var (key, orbit) = Canonicalize(raw);
        ref int idEntry = ref CollectionsMarshal.GetValueRefOrAddDefault(_ids, key, out bool found);
        if (found) return idEntry;
        if (_ids.Count > limits.MaxStates)
        {
            _ids.Remove(key);
            throw new SolveLimitException("Maximum unique states reached.");
        }

        int id = _nodes.Reserve(1);
        idEntry = id;

        var state = MainPhaseState.Evaluate(board, root.RuleSet);
        if (state.Result is { } result)
        {
            _nodes[id] = new(0, 0, 0, orbit, (sbyte)Math.Sign(result.PlayerOneScore - result.PlayerTwoScore), 0);
        }
        else
        {
            int redCount = state.GetLegalSelections(PlayerId.PlayerOne).Count;
            int blueCount = state.GetLegalSelections(PlayerId.PlayerTwo).Count;
            int count = redCount * blueCount;
            int start = _edges.Reserve(count);
            int index = 0, unique = 0;
            Span<ulong> destinations = stackalloc ulong[count];
            foreach (var red in state.GetLegalSelections(PlayerId.PlayerOne))
            foreach (var blue in state.GetLegalSelections(PlayerId.PlayerTwo))
            {
                CheckLimits();
                Pairs++;
                var next = state.ResolveTurn(new(PlayerId.PlayerOne, red), new(PlayerId.PlayerTwo, blue));
                long childRank = PositionIdentity.RemainingWork(next);
                if (childRank >= rank) throw new InvalidOperationException("The authoritative transition did not decrease rank.");
                ulong child = Pack(next);
                if (!destinations[..unique].Contains(child)) destinations[unique++] = child;
                _edges[start + index++] = Visit(next, child, childRank);
            }
            _nodes[id] = new(start, (byte)redCount, (byte)blueCount, orbit, 0, (byte)unique);
        }

        _expanded++;
        return id;
    }

    private void CheckLimits()
    {
        token.ThrowIfCancellationRequested();
        if (_clock.Elapsed >= limits.MaxTime) throw new SolveLimitException("Maximum enumeration time reached.");
        if ((Pairs & 1023) == 0 && GC.GetTotalMemory(false) >= limits.MaxManagedBytes)
            throw new SolveLimitException("Managed memory threshold reached.");
    }

    private ulong Pack(BoardState board)
    {
        ulong key = 0;
        int index = 0;
        for (int y = 0; y < _height; y++)
        for (int x = 0; x < _width; x++)
        {
            var cell = board.GetCell(new(x,y));
            int health = ignoreLockedHealth && cell.Piece is PieceType.Castle or PieceType.Grave ? 0 : cell.GroundHealth;
            int bits = PositionIdentity.CellBits(cell) | (health << 5);
            key |= (ulong)bits << (index * 7);
            index++;
        }
        return key;
    }

    private (ulong Key, byte Orbit) Canonicalize(ulong key)
    {
        ulong best = key;
        int stabilizer = 1;
        foreach (var transform in _symmetry.Transforms)
        {
            if (transform == default) continue;
            ulong mapped = 0;
            int index = 0;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                ulong cell = (key >> (index++ * 7)) & 127;
                if (transform.Exchange && (cell & 24) != 0) cell ^= 24;
                var destination = transform.Map(new(x,y), _width, _height);
                mapped |= cell << (7 * (destination.Y * _width + destination.X));
            }
            if (mapped < best) best = mapped;
            if (mapped == key) stabilizer++;
        }
        return (best, (byte)(_symmetry.Transforms.Length / stabilizer));
    }

    internal object Statistics()
    {
        var layers = new List<object>();
        var current = new Dictionary<int, BigInteger> { [0] = BigInteger.One };
        BigInteger completed = BigInteger.Zero, prefixes = BigInteger.Zero;
        int depth = 0;
        while (current.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var next = new Dictionary<int, BigInteger>();
            long positions = 0, terminalPositions = 0;
            BigInteger histories = BigInteger.Zero, finalHistories = BigInteger.Zero;
            foreach (var (id, count) in current)
            {
                var node = _nodes[id];
                positions += node.Orbit;
                histories += count;
                if (node.Terminal)
                {
                    terminalPositions += node.Orbit;
                    finalHistories += count;
                }
                else
                {
                    for (int i = node.EdgeStart; i < node.EdgeStart + node.PairCount; i++)
                    {
                        int child = _edges[i];
                        ref BigInteger frequency = ref CollectionsMarshal.GetValueRefOrAddDefault(next, child, out _);
                        frequency += count;
                    }
                }
            }
            prefixes += histories;
            completed += finalHistories;
            layers.Add(new { turns = depth++, positions, terminalPositions, histories = histories.ToString(),
                completed = finalHistories.ToString() });
            current = next;
        }

        long unique = 0, terminal = 0, wins = 0, losses = 0, draws = 0, transitions = 0, distinctEdges = 0;
        int maxBranching = 0;
        var actionCounts = new long[10,10];
        for (int id = 0; id < _nodes.Count; id++)
        {
            var node = _nodes[id];
            unique += node.Orbit;
            distinctEdges += node.DistinctDestinations * node.Orbit;
            transitions += node.PairCount * node.Orbit;
            maxBranching = Math.Max(maxBranching, node.PairCount);
            if (node.Terminal)
            {
                terminal += node.Orbit;
                if (node.TerminalSign == 0) draws += node.Orbit;
                else if (_symmetry.ExchangesPlayers) { wins += node.Orbit / 2; losses += node.Orbit / 2; }
                else if (node.TerminalSign > 0) wins += node.Orbit;
                else losses += node.Orbit;
                continue;
            }

            if (_symmetry.ExchangesPlayers && node.Red != node.Blue)
            {
                actionCounts[node.Red, node.Blue] += node.Orbit / 2;
                actionCounts[node.Blue, node.Red] += node.Orbit / 2;
            }
            else actionCounts[node.Red, node.Blue] += node.Orbit;
        }

        long active = unique - terminal;
        return new
        {
            unique, cachedPositions = _nodes.Count, nonterminal = active, terminal, redWins = wins, blueWins = losses, draws,
            pairTransitions = transitions, distinctEdges, resolvedPairs = Pairs,
            repeatedDestinations = transitions - unique + 1, maximumTurns = depth - 1,
            meanBranching = active > 0 ? (double)transitions / active : 0, maximumBranching = maxBranching,
            completedHistories = completed.ToString(), allPrefixes = prefixes.ToString(),
            depths = layers,
            actionCounts = (from red in Enumerable.Range(1, 9)
                            from blue in Enumerable.Range(1, 9)
                            where actionCounts[red,blue] > 0
                            select new { red, blue, positions = actionCounts[red,blue] }).ToArray(),
            symmetry = _symmetry.Transforms.Select(t => new { t.Geometry, playerExchange = t.Exchange })
        };
    }

    private readonly record struct Node(int EdgeStart, byte Red, byte Blue, byte Orbit, sbyte TerminalSign, byte DistinctDestinations)
    {
        internal int PairCount => Red * Blue;
        internal bool Terminal => Red == 0;
    }

    private sealed class Blocks<T> where T : struct
    {
        private const int Bits = 17;
        private const int Size = 1 << Bits;
        private readonly List<T[]> _blocks = [];
        internal int Count { get; private set; }

        internal ref T this[int index] => ref _blocks[index >> Bits][index & (Size - 1)];

        internal int Reserve(int count)
        {
            int start = Count;
            Count = checked(Count + count);
            while ((long)_blocks.Count * Size < Count) _blocks.Add(new T[Size]);
            return start;
        }
    }
}
