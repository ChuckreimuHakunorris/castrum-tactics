using System.Diagnostics;
using System.Numerics;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.Development.Solver;

internal enum SolverUtility { Outcome, Margin }

internal sealed record SolveLimits(int MaxStates, TimeSpan MaxTime, long MaxManagedBytes)
{
    internal static readonly SolveLimits Default = new(1_500_000, TimeSpan.FromSeconds(180), 1800L * 1024 * 1024);
}

internal sealed class SolveLimitException(string message) : Exception(message);

internal readonly record struct SolvedEdge(SolvedPosition Target, bool Exchange)
{
    internal Rational Value(SolverUtility utility, int endpoint = 0)
    {
        var game = Target.Game(utility);
        int side = Exchange ? -endpoint : endpoint;
        var value = side < 0 ? game.Lower : side > 0 ? game.Upper : game.Value;
        return Exchange ? -value : value;
    }
}

internal sealed class SolvedPosition
{
    internal SolvedPosition(MainPhaseState state, long rank, BoardTransform toCanonical = default, int orbitSize = 1,
        SolvedPosition? parent = null, BoardPosition redMove = default, BoardPosition blueMove = default,
        BoardState? boardTemplate = null, string? canonicalKey = null)
    {
        _boardTemplate = boardTemplate ?? state.Board;
        _canonicalKey = canonicalKey;
        Rules = state.RuleSet;
        Result = state.Result;
        Rank = rank;
        ToCanonical = toCanonical;
        OrbitSize = orbitSize;
        Parent = parent;
        RedMove = redMove;
        BlueMove = blueMove;
        Red = Result is null ? state.GetLegalSelections(PlayerId.PlayerOne).ToArray() : [];
        Blue = Result is null ? state.GetLegalSelections(PlayerId.PlayerTwo).ToArray() : [];
        Children = Result is null ? new SolvedPosition[checked(Red.Length * Blue.Length)] : [];
        if (Children.Length > 64) _longExchangeMask = new System.Collections.BitArray(Children.Length);
    }

    private readonly BoardState _boardTemplate;
    private readonly string? _canonicalKey;
    private ulong _exchangeMask;
    private readonly System.Collections.BitArray? _longExchangeMask;
    internal BoardState Board => _canonicalKey is null ? _boardTemplate :
        PositionIdentity.Materialize(_canonicalKey, _boardTemplate, ToCanonical);
    internal int Width => _boardTemplate.Width;
    internal int Height => _boardTemplate.Height;
    internal RuleSet Rules { get; }
    internal MainPhaseResult? Result { get; }
    internal SolvedPosition? Parent { get; }
    internal BoardPosition RedMove { get; }
    internal BoardPosition BlueMove { get; }
    internal long Rank { get; }
    internal BoardTransform ToCanonical { get; }
    internal int OrbitSize { get; }
    internal BoardPosition[] Red { get; }
    internal BoardPosition[] Blue { get; }
    internal SolvedPosition[] Children { get; }
    internal int DistinctDestinations { get; set; }
    internal Equilibrium? Outcome { get; set; }
    internal Equilibrium? Margin { get; set; }
    internal Equilibrium Game(SolverUtility utility) => (utility == SolverUtility.Outcome ? Outcome : Margin)!;
    internal SolvedEdge Child(int i, int j)
    {
        int index = i * Blue.Length + j;
        return new(Children[index], _longExchangeMask?[index] ?? ((_exchangeMask & (1UL << index)) != 0));
    }

    internal void SetChild(int index, SolvedEdge edge)
    {
        Children[index] = edge.Target;
        if (!edge.Exchange) return;
        if (_longExchangeMask is not null) _longExchangeMask[index] = true;
        else _exchangeMask |= 1UL << index;
    }

    internal Rational[,] Matrix(SolverUtility utility, int endpoint = 0)
    {
        var matrix = new Rational[Red.Length, Blue.Length];
        for (int i = 0; i < Red.Length; i++)
        for (int j = 0; j < Blue.Length; j++)
            matrix[i, j] = Child(i, j).Value(utility, endpoint);
        return matrix;
    }

}

internal sealed class SmallBoardGraph(SolveLimits limits, CancellationToken token, bool symmetry = true, bool ignoreLockedHealth = false, int intervalScale = 0, bool fastPolicy = false)
{
    internal const PlayerId Red = PlayerId.PlayerOne;
    internal const PlayerId Blue = PlayerId.PlayerTwo;
    private readonly Dictionary<string, SolvedPosition> _positions = new(StringComparer.Ordinal);
    private readonly List<SolvedPosition> _postorder = [];
    private readonly Dictionary<int, Equilibrium> _terminals = [];
    private readonly PolicyPool _pool = new();
    private BoardState? _boardTemplate;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    internal IReadOnlyDictionary<string, SolvedPosition> Positions => _positions;
    internal IReadOnlyList<SolvedPosition> Postorder => _postorder;
    internal BoardSymmetry Symmetry { get; private set; } = new([default]);
    internal bool IgnoresLockedHealth => ignoreLockedHealth;
    internal int IntervalScale => intervalScale;
    internal long Pairs { get; private set; }
    internal TimeSpan Elapsed => _clock.Elapsed;

    internal SolvedPosition Solve(GameSessionSnapshot root)
    {
        if (root.MainState is null || root.Phase is not (MatchPhase.Main or MatchPhase.Finished))
            throw new ArgumentException("A resolved Main or Finished snapshot is required.", nameof(root));
        if (_positions.Count != 0) throw new InvalidOperationException("Use a new graph to solve a different root.");
        CheckLimits();
        Symmetry = BoardSymmetry.ForRoot(root.MainState, symmetry);
        _boardTemplate = root.Board;
        return Visit(root.Board, root.RuleSet, Identity(root.Board, root.RuleSet),
            PositionIdentity.RemainingWork(root.Board), null, default, default).Target;
    }

    internal (SolvedPosition Node, BoardTransform QueryToCanonical) Find(MainPhaseState state)
    {
        var board = state.Board;
        string raw = Identity(board, state.RuleSet);
        var (key, transform) = Symmetry.Canonicalize(raw, board.Width, board.Height);
        if (!_positions.TryGetValue(key, out var node))
        {
            string exchanged = new BoardTransform(0, true).Apply(raw, board.Width, board.Height);
            (key, transform) = Symmetry.Canonicalize(exchanged, board.Width, board.Height);
            if (!_positions.TryGetValue(key, out node))
                throw new InvalidOperationException("The resolved position is outside the solved graph.");
            transform = transform with { Exchange = !transform.Exchange };
        }
        if (node.Margin is null) throw new InvalidOperationException("The resolved position has not been certified.");
        return (node, transform);
    }

    internal Equilibrium Lookup(MainPhaseState state, SolverUtility utility)
    {
        var (node, transform) = Find(state);
        var game = node.Game(utility);
        if (state.IsFinished) return transform.Exchange == node.ToCanonical.Exchange ? game : BoardTransform.Negate(game);
        var red = state.GetLegalSelections(Red).ToArray();
        var blue = state.GetLegalSelections(Blue).ToArray();
        var canonical = node.ToCanonical.PolicyToCanonical(game, node.Red, node.Blue, node.Width, node.Height);
        return transform.PolicyFromCanonical(canonical, red, blue, node.Width, node.Height);
    }

    private SolvedEdge Visit(BoardState board, RuleSet rules, string raw, long rank, SolvedPosition? parent,
        BoardPosition redMove, BoardPosition blueMove)
    {
        var (key, transform) = Symmetry.Canonicalize(raw, board.Width, board.Height);
        if (_positions.TryGetValue(key, out var existing))
        {
            if (existing.Margin is null) throw new InvalidOperationException("Encountered an unresolved transposition.");
            return new(existing, transform.Exchange ^ existing.ToCanonical.Exchange);
        }

        if (_positions.Count >= limits.MaxStates) throw new SolveLimitException("Maximum unique states reached.");
        var state = MainPhaseState.Evaluate(board, rules);
        // The full representative retains its original health for inspection and authoritative history reconstruction.
        string exactKey = ignoreLockedHealth
            ? transform.Apply(PositionIdentity.Key(board, rules), board.Width, board.Height) : key;
        var node = new SolvedPosition(state, rank, transform, Symmetry.OrbitSize(raw, board.Width, board.Height),
            parent, redMove, blueMove, _boardTemplate, exactKey);
        _positions.Add(key, node);
        if (state.Result is { } result)
        {
            int margin = result.PlayerOneScore - result.PlayerTwoScore;
            node.Outcome = Terminal(Math.Sign(margin));
            node.Margin = Terminal(margin);
        }
        else
        {
            int index = 0;
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var red in node.Red)
            foreach (var blue in node.Blue)
            {
                CheckLimits();
                Pairs++;
                var next = state.ResolveTurn(new(Red, red), new(Blue, blue));
                long nextRank = PositionIdentity.RemainingWork(next);
                if (nextRank >= rank)
                    throw new InvalidOperationException("Authoritative transition violated the strict progress measure.");
                string childKey = Identity(next, rules);
                destinations.Add(childKey);
                node.SetChild(index++, Visit(next, rules, childKey, nextRank, node, red, blue));
            }

            node.DistinctDestinations = destinations.Count;
            node.Outcome = SolveGame(node, SolverUtility.Outcome);
            node.Margin = SolveGame(node, SolverUtility.Margin);
        }

        _postorder.Add(node);
        return new(node, false);
    }

    private Equilibrium SolveGame(SolvedPosition node, SolverUtility utility)
    {
        if (intervalScale == 0) return _pool.Intern(MatrixGame.Solve(node.Matrix(utility), CheckLimits));
        bool pointChildren = node.Children.All(child => child.Game(utility).IsExact);
        var lowerMatrix = node.Matrix(utility, -1);
        var upperMatrix = pointChildren ? lowerMatrix : node.Matrix(utility, 1);
        bool smallExact = pointChildren && (node.Red.Length == 1 || node.Blue.Length == 1 ||
            node.Red.Length == 2 && node.Blue.Length == 2);
        var candidate = fastPolicy && !smallExact ? Binary64Policy.TryCertify(lowerMatrix, upperMatrix, intervalScale) : null;
        var game = candidate ?? (pointChildren
            ? MatrixGame.Solve(lowerMatrix, CheckLimits)
            : MatrixGame.SolveBounds(lowerMatrix, upperMatrix, CheckLimits));
        if (fastPolicy && game.Lower != -game.Upper && Symmetry.ExchangesPlayers &&
            node.OrbitSize < Symmetry.Transforms.Length)
        {
            string key = Identity(node.Board, node.Rules);
            var symmetry = Symmetry.Transforms.FirstOrDefault(t => t.Exchange &&
                t.Apply(key, node.Width, node.Height) == key);
            if (symmetry.Exchange)
            {
                // A color-exchanging stabilizer can give two equivalent coordinate mappings. Pair
                // the better certified policy with its exchanged counterpart and make the bounds invariant.
                var reflected = symmetry.PolicyToCanonical(game, node.Red, node.Blue, node.Width, node.Height);
                game = game.Lower < reflected.Lower
                    ? reflected with { Blue = game.Blue, Upper = -reflected.Lower }
                    : game with { Blue = reflected.Blue, Upper = -game.Lower };
            }
        }
        var lower = game.Lower.RoundOutward(intervalScale, upper: false);
        var upper = game.Upper.RoundOutward(intervalScale, upper: true);
        return _pool.Intern(game with { Value = (lower + upper) / 2, Lower = lower, Upper = upper });
    }

    private string Identity(BoardState board, RuleSet rules) =>
        PositionIdentity.Key(board, rules, ignoreLockedHealth: ignoreLockedHealth);

    private Equilibrium Terminal(int value)
    {
        if (!_terminals.TryGetValue(value, out var game)) _terminals[value] = game = new(value, [], []);
        return game;
    }

    private void CheckLimits()
    {
        token.ThrowIfCancellationRequested();
        if (_clock.Elapsed >= limits.MaxTime) throw new SolveLimitException("Maximum solve time reached.");
        if ((Pairs & 255) == 0 && GC.GetTotalMemory(forceFullCollection: false) >= limits.MaxManagedBytes)
            throw new SolveLimitException("Managed memory threshold reached.");
    }

    internal object Statistics(SolvedPosition root)
    {
        var total = new DepthStatistics();
        var byDepth = new List<object>();
        var layer = new Dictionary<SolvedPosition, BigInteger> { [root] = BigInteger.One };
        int depth = 0;
        while (layer.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var row = new DepthStatistics();
            var next = new Dictionary<SolvedPosition, BigInteger>();
            foreach (var (node, count) in layer)
            {
                row.Positions += node.OrbitSize;
                row.Histories += count;
                if (node.Result is not null)
                {
                    row.TerminalPositions += node.OrbitSize;
                    row.CompletedHistories += count;
                }
                foreach (var child in node.Children)
                    next[child] = next.GetValueOrDefault(child) + count;
            }
            byDepth.Add(new { turns = depth++, row.Positions, row.TerminalPositions,
                histories = row.Histories.ToString(), completed = row.CompletedHistories.ToString() });
            total.Histories += row.Histories;
            total.CompletedHistories += row.CompletedHistories;
            layer = next;
        }

        long unique = 0, terminal = 0, redWins = 0, blueWins = 0, draws = 0, edges = 0, distinctEdges = 0;
        var actions = new Dictionary<(int Red, int Blue), long>();
        var outcomes = new MixStatistics[2];
        var utilities = Enum.GetValues<SolverUtility>();
        foreach (var node in _postorder)
        {
            token.ThrowIfCancellationRequested();
            unique += node.OrbitSize;
            edges += (long)node.Children.Length * node.OrbitSize;
            distinctEdges += (long)node.DistinctDestinations * node.OrbitSize;
            if (node.Result is { } result)
            {
                terminal += node.OrbitSize;
                if (result.Winner is null) draws += node.OrbitSize;
                else if (Symmetry.ExchangesPlayers)
                {
                    redWins += node.OrbitSize / 2;
                    blueWins += node.OrbitSize / 2;
                }
                else if (result.Winner == Red) redWins += node.OrbitSize;
                else blueWins += node.OrbitSize;
                continue;
            }

            Accumulate(actions, (node.Red.Length, node.Blue.Length));
            foreach (var utility in utilities)
            {
                var game = node.Game(utility);
                ref var stats = ref outcomes[(int)utility];
                if (!game.IsExact) stats.IntervalPositions += node.OrbitSize;
                var gap = game.Upper - game.Lower;
                if (gap > stats.MaximumGap) stats.MaximumGap = gap;
                bool redMix = MatrixGame.RowGuarantees(node.Matrix(utility, 1)).Max() < game.Lower;
                bool blueMix = MatrixGame.ColumnCeilings(node.Matrix(utility, -1)).Min() > game.Upper;
                if (redMix || blueMix) stats.Either += node.OrbitSize;
                if (Symmetry.ExchangesPlayers)
                {
                    int perSide = ((redMix ? 1 : 0) + (blueMix ? 1 : 0)) * node.OrbitSize / 2;
                    stats.Red += perSide;
                    stats.Blue += perSide;
                }
                else
                {
                    if (redMix) stats.Red += node.OrbitSize;
                    if (blueMix) stats.Blue += node.OrbitSize;
                }
            }

            void Accumulate(Dictionary<(int Red, int Blue), long> counts, (int Red, int Blue) pair)
            {
                if (!Symmetry.ExchangesPlayers || pair.Red == pair.Blue) counts[pair] = counts.GetValueOrDefault(pair) + node.OrbitSize;
                else
                {
                    var reverse = (pair.Blue, pair.Red);
                    counts[pair] = counts.GetValueOrDefault(pair) + node.OrbitSize / 2;
                    counts[reverse] = counts.GetValueOrDefault(reverse) + node.OrbitSize / 2;
                }
            }
        }

        long active = unique - terminal;
        return new
        {
            unique, cachedPositions = _positions.Count, nonterminal = active, terminal, redWins, blueWins, draws,
            pairTransitions = edges, distinctEdges, resolvedPairs = Pairs, repeatedDestinations = edges - unique + 1,
            maximumTurns = depth - 1, meanBranching = active == 0 ? 0 : (double)edges / active,
            maximumBranching = _postorder.Max(node => node.Children.Length),
            completedHistories = total.CompletedHistories.ToString(), allPrefixes = total.Histories.ToString(),
            actionCounts = actions.OrderBy(kv => kv.Key.Red).ThenBy(kv => kv.Key.Blue)
                .Select(kv => new { red = kv.Key.Red, blue = kv.Key.Blue, positions = kv.Value }).ToArray(),
            depths = byDepth,
            symmetry = Symmetry.Transforms.Select(t => new { t.Geometry, playerExchange = t.Exchange }),
            objectives = utilities.Select(u => new { utility = u.ToString(), redRequiresMix = outcomes[(int)u].Red,
                blueRequiresMix = outcomes[(int)u].Blue, eitherRequiresMix = outcomes[(int)u].Either,
                intervalPositions = outcomes[(int)u].IntervalPositions,
                maximumGap = outcomes[(int)u].MaximumGap.ToString() })
        };
    }

    internal PolicyOutcome PolicyPair(SolvedPosition root, SolverUtility utility)
    {
        var results = new Dictionary<SolvedPosition, PolicyOutcome>();
        return VisitPolicy(root);

        PolicyOutcome VisitPolicy(SolvedPosition node)
        {
            if (results.TryGetValue(node, out var previous)) return previous;
            if (node.Result is { } terminal)
                return new(terminal.Winner == Red ? 1 : 0, terminal.Winner is null ? 1 : 0,
                    terminal.Winner == Blue ? 1 : 0, 0);

            PolicyOutcome result = new(0, 0, 0, 1);
            var game = node.Game(utility);
            for (int i = 0; i < node.Red.Length; i++)
            for (int j = 0; j < node.Blue.Length; j++)
            {
                Rational probability = game.Red[i] * game.Blue[j];
                if (probability.IsZero) continue;
                var edge = node.Child(i,j);
                var child = VisitPolicy(edge.Target);
                if (edge.Exchange) child = child with { Win = child.Loss, Loss = child.Win };
                result = new(result.Win + probability * child.Win, result.Draw + probability * child.Draw,
                    result.Loss + probability * child.Loss, result.Turns + probability * child.Turns);
            }
            results[node] = result;
            return result;
        }
    }

    internal (bool Red, bool Blue) CanForceDraw(SolvedPosition root) => GuaranteedResult(root, allowWin: false);

    internal (bool Red, bool Blue) CanAvoidLoss(SolvedPosition root) => GuaranteedResult(root, allowWin: true);

    private (bool Red, bool Blue) GuaranteedResult(SolvedPosition root, bool allowWin)
    {
        var guaranteed = new Dictionary<SolvedPosition, (bool Red, bool Blue)>();
        foreach (var node in _postorder)
        {
            token.ThrowIfCancellationRequested();
            if (node.Result is { } result)
                guaranteed[node] = (result.Winner is null || allowWin && result.Winner == Red,
                    result.Winner is null || allowWin && result.Winner == Blue);
            else guaranteed[node] = (
                Enumerable.Range(0, node.Red.Length).Any(i => Enumerable.Range(0, node.Blue.Length)
                    .All(j => Guaranteed(node.Child(i,j), true))),
                Enumerable.Range(0, node.Blue.Length).Any(j => Enumerable.Range(0, node.Red.Length)
                    .All(i => Guaranteed(node.Child(i,j), false))));
        }
        return guaranteed[root];

        bool Guaranteed(SolvedEdge edge, bool red) => red ^ edge.Exchange
            ? guaranteed[edge.Target].Red : guaranteed[edge.Target].Blue;
    }

    private sealed class DepthStatistics
    {
        internal long Positions, TerminalPositions;
        internal BigInteger Histories, CompletedHistories;
    }

    private struct MixStatistics
    {
        internal long Red, Blue, Either, IntervalPositions;
        internal Rational MaximumGap;
    }
}

internal sealed record PolicyOutcome(Rational Win, Rational Draw, Rational Loss, Rational Turns);
