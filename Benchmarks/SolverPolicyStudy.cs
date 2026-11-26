extern alias frozen;
extern alias frozen8c;
extern alias frozen8d2;

using CastrumTactics.AI;
using CastrumTactics.Development.Solver;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using EightC = frozen8c::CastrumTactics.AI;
using EightD2 = frozen8d2::CastrumTactics.AI;
using Frozen = frozen::CastrumTactics.AI;

internal static partial class Program
{
    private static object AnalyzePolicies(SmallBoardGraph graph, PredefinedMap map, string starts, string[] basePath,
        int sampleSize, string split, CancellationToken token)
    {
        if (sampleSize < 1) throw new ArgumentOutOfRangeException(nameof(sampleSize));
        var variants = SolverStudyOpponents().Select(CreateOpponent).ToArray();
        // Balance sampling across the monotone progress measure. Weight each tested representative equally.
        var selected = StudyPositions(graph, sampleSize, split, token);
        var comparisons = new List<PolicyComparison>();
        var features = new List<EvaluationPoint>();
        var horizon = new HorizonStudy(graph.Postorder[^1].Board);
        foreach (var node in selected)
        {
            token.ThrowIfCancellationRequested();
            var session = OpenSolverSession(map.Id, starts);
            foreach (var pair in basePath) ReplayPair(pair);
            var history = new Stack<SolvedPosition>();
            for (var parent = node; parent.Parent is not null; parent = parent.Parent) history.Push(parent);
            foreach (var move in history) Play(session, Coordinate(move.RedMove), Coordinate(move.BlueMove));
            var snapshot = session.Current;
            if (PositionIdentity.Key(snapshot.Board, snapshot.RuleSet) != PositionIdentity.Key(node.Board, node.Rules))
                throw new InvalidOperationException("Representative path did not reconstruct its position.");
            int stage = Stage(node, graph.Postorder[^1]);
            var tactical = new StrategicPosition(node.Board, RuleSetDefinition.Get(node.Rules), token);
            var f = tactical.Evaluate(node.Board, snapshot.Territory!, snapshot.GetMainPhaseLegalSelections(Red),
                snapshot.GetMainPhaseLegalSelections(Blue), token);
            var revised = tactical.Evaluate(node.Board, snapshot.Territory!, snapshot.GetMainPhaseLegalSelections(Red),
                snapshot.GetMainPhaseLegalSelections(Blue), token, resilient: true, prepareFrontier: false);
            var contributions = f.Contributions(Red);
            features.Add(new(stage, [f.Score(Red), contributions.Secured, contributions.Influence, contributions.Material,
                contributions.Formation, contributions.Ground, contributions.Mobility], node.Outcome!.Value.Approximate(),
                node.Margin!.Value.Approximate(), string.Join(',', basePath.Concat(history.Select(move =>
                    $"{Coordinate(move.RedMove)}/{Coordinate(move.BlueMove)}"))),
                SimultaneousSearch.BoundedEstimate(revised.Score(Red), node.Width * node.Height),
                node.Outcome.Lower.Approximate(), node.Outcome.Upper.Approximate()));

            foreach (var side in new[] { Red, Blue })
            {
                var actions = side == Red ? node.Red : node.Blue;
                var opposing = side == Red ? node.Blue : node.Red;
                foreach (var opponent in variants)
                {
                    token.ThrowIfCancellationRequested();
                    var decision = opponent.Decide(snapshot, side, new Random(42), true, token);
                    var modeled = CpuPolicy(snapshot, side, actions, decision);
                    var indexes = decision.Diagnostics is CpuDecisionReport { Search: { } search }
                        ? search.Replies.Select(reply => Array.IndexOf(opposing, reply.Position)).ToHashSet()
                        : null;
                    var rootActions = decision.Diagnostics is CpuDecisionReport { Search: { } report }
                        ? report.Actions.Select(action => Array.IndexOf(actions, action.Position)).ToHashSet()
                        : null;
                    var rating = decision.Diagnostics is CpuDecisionReport { Search: { } searchReport }
                        ? searchReport.Actions.Select(a => (Index: Array.IndexOf(actions, a.Position), a.Expected, a.Adverse)).ToArray()
                        : null;
                    Compare(opponent.Spec.Id, stage, node, side, modeled, Array.IndexOf(actions, Position(decision.Move)),
                        indexes, rootActions, decision.Depth, decision.CompletedDepth, rating);
                }

                if (ReferenceEquals(node, graph.Postorder[^1]) || Array.IndexOf(selected, node) < 12)
                {
                    for (int depth = 1; depth <= 4; depth++)
                    {
                        token.ThrowIfCancellationRequested();
                        var limited = horizon.Game(node, depth, token);
                        var probs = side == Red ? limited.Red : limited.Blue;
                        Compare($"full-matrix-depth-{depth}", stage, node, side,
                            probs.Select(p => p.Approximate()).ToArray(), Array.FindIndex(probs, p => !p.IsZero));
                        var pure = side == Red ? MatrixGame.RowGuarantees(horizon.Matrix(node, depth, token))
                            : MatrixGame.ColumnCeilings(horizon.Matrix(node, depth, token)).Select(v => -v).ToArray();
                        int action = Array.IndexOf(pure, pure.Max());
                        Compare($"pure-maximin-depth-{depth}", stage, node, side,
                            actions.Select((_, i) => i == action ? 1.0 : 0.0).ToArray(), action);
                    }
                }
            }

            void ReplayPair(string text)
            {
                var (red, blue) = ParsePair(text);
                Play(session, Coordinate(red), Coordinate(blue));
            }
        }

        string[] names = ["score", "secured", "influence", "material", "formation", "ground", "mobility"];
        return new
        {
            seed = 42, positionSelectionSeed = 4242, split, stage = "rank thirds (2 early, 1 middle, 0 late)",
            sampledRepresentatives = selected.Length, policyInterpretation =
                "CPU-reported binary64 probabilities, or uniform over the exact Probe tie set. Guarantees use solved play after the present turn.",
            horizonInterpretation = "Full reply matrices, terminal outcome and quantized h/(3+|h|) at the cutoff. Tested on a subset.",
            cpu = comparisons.GroupBy(row => (row.Id, row.Utility))
                .Select(group => Summarize(group.Key.Id, group.Key.Utility, group)),
            stages = comparisons.GroupBy(row => (row.Id, row.Utility, row.Stage))
                .Select(group => new { group.Key.Stage, results = Summarize(group.Key.Id, group.Key.Utility, group) }),
            evaluator = new[] { -1, 0, 1, 2 }.Select(stage =>
            {
                var subset = features.Where(point => stage == -1 || point.Stage == stage).ToArray();
                var outcomeError = subset.Select(point =>
                    Math.Abs(point.Features[0] / (3 + Math.Abs(point.Features[0])) - point.Outcome)).ToArray();
                return new { stage, n = subset.Length, meanOutcomeAbsoluteError = subset.Length == 0 ? (double?)null : outcomeError.Average(),
                    resilientMeanOutcomeAbsoluteError = subset.Length == 0 ? (double?)null :
                        subset.Average(point => Math.Abs(point.Resilient - point.Outcome)),
                    certifiedLegacySignErrors = subset.Count(point => OppositeSign(point.Features[0], point)),
                    certifiedResilientSignErrors = subset.Count(point => OppositeSign(point.Resilient, point)),
                    signErrors = subset.Count(point => Math.Sign(point.Features[0]) * point.Outcome < 0),
                    correlations = names.Select((name, i) => new
                {
                    feature = name, outcome = Pearson(subset, i, point => point.Outcome),
                    margin = Pearson(subset, i, point => point.Margin)
                }) };
            }),
            confidentMisreads = features.Where(p => Math.Sign(p.Features[0]) * p.Outcome < 0)
                .OrderByDescending(p => Math.Abs(p.Features[0])).Take(8).Select(p =>
                    new { p.Stage, heuristic = p.Features[0], p.Outcome, p.Margin, p.Path })
        };

        static bool OppositeSign(double heuristic, EvaluationPoint point) =>
            heuristic > 0 && point.OutcomeUpper < 0 || heuristic < 0 && point.OutcomeLower > 0;

        void Compare(string id, int stage, SolvedPosition node, PlayerId side, double[] distribution, int selectedAction,
            HashSet<int>? modeledReplies = null, HashSet<int>? searchedActions = null,
            int? depth = null, int? completedDepth = null, (int Index, double Expected, double Adverse)[]? rating = null)
        {
            bool red = side == Red;
            foreach (var utility in Enum.GetValues<SolverUtility>())
            {
                var game = node.Game(utility);
                var m = node.Matrix(utility);
                var value = red ? node.Game(utility).Value : -node.Game(utility).Value;
                var pure = red ? MatrixGame.RowGuarantees(m) : MatrixGame.ColumnCeilings(m).Select(v => -v).ToArray();
                var dominated = MatrixGame.Dominated(m, red);
                int replyCount = m.GetLength(red ? 1 : 0);
                Rational At(int i, int j) => red ? m[i,j] : -m[j,i];
                double Worst(IEnumerable<int> replies) => replies.Min(j =>
                    distribution.Select((p, i) => p * At(i,j).Approximate()).Sum());
                double guarantee = Worst(Enumerable.Range(0, replyCount));
                var probabilities = distribution.Select(Rational.FromBinary64).ToArray();
                var total = probabilities.Aggregate(default(Rational), (a, b) => a + b);
                for (int i = 0; i < probabilities.Length; i++) probabilities[i] /= total;
                Rational PolicyBound(int endpoint)
                {
                    var bound = node.Matrix(utility, red ? endpoint : -endpoint);
                    return Enumerable.Range(0, replyCount).Select(j => probabilities.Select((p, i) =>
                        p * (red ? bound[i,j] : -bound[j,i])).Aggregate(default(Rational), (a, b) => a + b)).Min();
                }
                var gapLower = (red ? game.Lower : -game.Upper) - PolicyBound(1);
                var gapUpper = (red ? game.Upper : -game.Lower) - PolicyBound(-1);
                double replyGap = modeledReplies is { Count: > 0 } ? Worst(modeledReplies) - guarantee : 0;
                bool missedBest = searchedActions is { Count: > 0 } &&
                    searchedActions.Max(i => pure[i]) < pure.Max();
                int comparable = 0, agreements = 0;
                if (rating is not null)
                {
                    for (int i = 0; i < rating.Length; i++)
                    for (int j = i + 1; j < rating.Length; j++)
                    {
                        int trueOrder = pure[rating[i].Index].CompareTo(pure[rating[j].Index]);
                        double estimate = rating[i].Adverse - rating[j].Adverse;
                        if (Math.Abs(estimate) <= 1e-8) estimate = rating[i].Expected - rating[j].Expected;
                        if (trueOrder == 0 || Math.Abs(estimate) <= 1e-8) continue;
                        comparable++;
                        if (trueOrder == Math.Sign(estimate)) agreements++;
                    }
                }
                comparisons.Add(new(id, utility.ToString(), stage, value.Approximate(), guarantee,
                    pure[selectedAction].Approximate(), pure.Max() < value, pure[selectedAction] == value,
                    dominated[selectedAction], distribution.Where((_, i) => pure[i] == value).Sum(),
                    distribution.Where((_, i) => dominated[i]).Sum(), replyGap > 1e-10, missedBest,
                    depth, completedDepth, agreements, comparable,
                    Math.Max(0, gapLower.Approximate()), gapUpper.Approximate()));
            }
        }
    }

    private static SolvedPosition[] StudyPositions(SmallBoardGraph graph, int sampleSize, string split, CancellationToken token)
    {
        var root = graph.Postorder[^1];
        if (split == "all")
        {
            var random = new Random(4242);
            var groups = graph.Postorder.Where(node => node.Result is null).GroupBy(node => Stage(node, root))
                .OrderBy(group => group.Key).Select(group => group.ToArray()).ToArray();
            return groups.SelectMany(group => group.OrderBy(_ => random.Next())
                    .Take((sampleSize + groups.Length - 1) / groups.Length))
                .Prepend(root).Where(node => node.Result is null).Distinct().ToArray();
        }

        int perStage = (sampleSize + 2) / 3;
        var symmetry = new BoardSymmetry((from geometry in Enumerable.Range(0, root.Width == root.Height ? 8 : 4)
            from exchange in new[] { false, true } select new BoardTransform(geometry, exchange)).ToArray());
        var queues = Enumerable.Range(0, 3)
            .Select(_ => new PriorityQueue<(string Key, SolvedPosition Node), (ulong Hash, string Key)>(
                Comparer<(ulong Hash, string Key)>.Create((a, b) =>
                    a.Hash != b.Hash ? b.Hash.CompareTo(a.Hash) : string.CompareOrdinal(b.Key, a.Key)))).ToArray();
        var retained = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, node) in graph.Positions)
        {
            token.ThrowIfCancellationRequested();
            if (node.Result is not null || ReferenceEquals(node, root)) continue;
            string identity = symmetry.Canonicalize(key, root.Width, root.Height).Key;
            ulong hash = 14695981039346656037UL ^ 4242;
            foreach (char unit in identity) hash = unchecked((hash ^ unit) * 1099511628211UL);
            hash ^= hash >> 33;
            hash = unchecked(hash * 0xff51afd7ed558ccdUL);
            hash ^= hash >> 33;
            if (((hash & 3) == 0) != (split == "held-out") || !retained.Add(identity)) continue;
            var queue = queues[Stage(node, root)];
            queue.Enqueue((identity, node), (hash, identity));
            if (queue.Count > perStage) retained.Remove(queue.Dequeue().Key);
        }
        return queues.SelectMany(queue => queue.UnorderedItems.OrderBy(item => item.Priority.Hash)
            .ThenBy(item => item.Element.Key, StringComparer.Ordinal).Select(item => item.Element.Node)).ToArray();
    }

    private static IEnumerable<ParticipantSpec> SolverStudyOpponents()
    {
        foreach (string kind in new[] { "static", "frozen8c", "frozen8d2", "strategic" })
        foreach (var difficulty in Enum.GetValues<CpuDifficulty>())
            yield return new($"{kind}-{difficulty.ToString().ToLowerInvariant()}", $"{kind}:{difficulty}");
        yield return new("probe", "probe");
        yield return new("monster-one", "strategic:Monster", Search: new(Depth: 1));
        yield return new("monster-narrow-replies", "strategic:Monster", Search: new(Depth: 2, OpponentCandidates: 2));
        yield return new("monster-two", "strategic:Monster", Search: new(Depth: 2));
        yield return new("resilient", "strategic:Monster", Search: new(Depth: 2, ResilientEvaluation: true));
        yield return new("bounded", "strategic:Monster", Search: new(Depth: 2, BoundedUtility: true));
        yield return new("matrix-one", "strategic:Monster", Search: new(Depth: 1, Equilibrium: MatrixPolicy.Root));
        yield return new("matrix-expected", "strategic:Monster", Search: new(Depth: 2, Equilibrium: MatrixPolicy.Expected));
        yield return new("matrix-root", "strategic:Monster", Search: new(Depth: 2, Equilibrium: MatrixPolicy.Root));
        yield return new("matrix-continuations", "strategic:Monster", Search: new(Depth: 2, Equilibrium: MatrixPolicy.Continuations));
        yield return new("combined", "strategic:Monster", Search: new(Depth: 2, ResilientEvaluation: true,
            Equilibrium: MatrixPolicy.Continuations));
        yield return new("resilient-wide", "strategic:Monster", 2000, Search: new(Depth: 2, OwnCandidates: 22,
            OpponentCandidates: 14, ResilientEvaluation: true));
        yield return new("monster-wide", "strategic:Monster", 2000, Search: new(Depth: 2, OwnCandidates: 22, OpponentCandidates: 14));
        yield return new("monster-deep", "strategic:Monster", 2000, Search: new(Depth: 3,
            ContinuationOwnCandidates: 2, ContinuationOpponentCandidates: 2, DeepFinalists: 2, ProofOnlyDeepening: false));
        yield return new("monster-deep-full-replies", "strategic:Monster", 2000, Search: new(Depth: 3,
            ContinuationOwnCandidates: 2, ContinuationOpponentCandidates: 2, DeepFinalists: 2,
            RequireFullRootReplies: true, ProofOnlyDeepening: false));
        yield return new("monster-deep-large", "strategic:Monster", 4000, Search: new(Depth: 3,
            ContinuationOwnCandidates: 3, ContinuationOpponentCandidates: 2, DeepFinalists: 2, ProofOnlyDeepening: false));
        yield return new("monster-four", "strategic:Monster", 8000, Search: new(Depth: 4,
            ContinuationOwnCandidates: 2, ContinuationOpponentCandidates: 2, DeepFinalists: 2, ProofOnlyDeepening: false));
    }

    private static double[] CpuPolicy(GameSessionSnapshot snapshot, PlayerId side, BoardPosition[] actions,
        DecisionSample decision)
    {
        IEnumerable<(BoardPosition Position, double Probability)> probabilities = decision.Diagnostics switch
        {
            CpuDecisionReport report => report.Candidates.Select(c => (c.Position, c.Probability)),
            EightC.CpuDecisionReport report => report.Candidates.Select(c => (c.Position, c.Probability)),
            EightD2.CpuDecisionReport baseline => baseline.Candidates.Select(c => (c.Position, c.Probability)),
            Frozen.CpuDecisionReport report => report.Candidates.Select(c => (c.Position, c.Probability)),
            _ => ProbePolicy(snapshot, side)
        };
        var result = new double[actions.Length];
        foreach (var (position, probability) in probabilities)
            result[Array.IndexOf(actions, position)] = probability;
        if (Math.Abs(result.Sum() - 1) > 1e-10 || result.Any(p => !double.IsFinite(p) || p < 0))
            throw new InvalidDataException("CPU diagnostics contain invalid probabilities.");
        return result;
    }

    private static (BoardPosition Position, double Probability)[] ProbePolicy(GameSessionSnapshot snapshot, PlayerId side)
    {
        var first = new ProbeRandom(0);
        var initial = ArchivedProbe.Probe(snapshot, side, first, out _);
        var result = new (BoardPosition, double)[first.Count];
        result[0] = (initial, 1.0 / first.Count);
        for (int i = 1; i < result.Length; i++)
        {
            var random = new ProbeRandom(i);
            var action = ArchivedProbe.Probe(snapshot, side, random, out _);
            if (random.Count != first.Count || result.Take(i).Any(item => item.Item1 == action))
                throw new InvalidDataException("Probe did not produce a stable tie set.");
            result[i] = (action, 1.0 / first.Count);
        }
        return result;
    }

    private sealed class ProbeRandom(int selection) : Random
    {
        internal int Count { get; private set; }

        /// <summary>Returns the requested tie index and records the archived Probe's single sampling interval.</summary>
        public override int Next(int maxValue)
        {
            if (Count != 0 || selection >= maxValue) throw new InvalidOperationException("Unexpected Probe RNG use.");
            Count = maxValue;
            return selection;
        }
    }

    private static int Stage(SolvedPosition node, SolvedPosition root) => root.Rank == 0 ? 0 :
        (int)Math.Min(2, 3 * node.Rank / root.Rank);

    private static object Summarize(string id, string utility, IEnumerable<PolicyComparison> rows)
    {
        var data = rows.ToArray();
        var gaps = data.Select(row => row.Value - row.Guarantee).Order().ToArray();
        return new { id, utility, n = data.Length, meanValue = data.Average(row => row.Value),
            meanGuarantee = data.Average(row => row.Guarantee), meanGap = data.Average(row => row.Value - row.Guarantee),
            meanGapLower = data.Average(row => row.GapLower), meanGapUpper = data.Average(row => row.GapUpper),
            meanSampledPureGap = data.Average(row => row.Value - row.PureGuarantee),
            medianGap = gaps[(gaps.Length - 1) / 2], p95Gap = gaps[(int)Math.Ceiling(gaps.Length * .95) - 1],
            maxGap = gaps[^1],
            optimalPureSelections = data.Count(row => row.OptimalPure), dominatedSelections = data.Count(row => row.Dominated),
            meanOptimalPureMass = data.Average(row => row.OptimalPureMass), meanDominatedMass = data.Average(row => row.DominatedMass),
            mixRequired = data.Count(row => row.MixRequired),
            mixRequiredMeanGap = data.Where(row => row.MixRequired).Select(row => row.Value - row.Guarantee).DefaultIfEmpty().Average(),
            missedAdverseReply = data.Count(row => row.MissedReply), missedBestPureAction = data.Count(row => row.MissedBest),
            rankingAgreement = data.Sum(row => row.RankingPairs) == 0 ? (double?)null :
                (double)data.Sum(row => row.RankingAgreements) / data.Sum(row => row.RankingPairs),
            meanDepth = data.Average(row => row.Depth), meanCompletedDepth = data.Average(row => row.CompletedDepth) };
    }

    private static double? Pearson(EvaluationPoint[] points, int feature, Func<EvaluationPoint, double> target)
    {
        if (points.Length < 2) return null;
        double a = points.Average(point => point.Features[feature]), b = points.Average(target);
        double numerator = 0, aa = 0, bb = 0;
        foreach (var point in points)
        {
            double x = point.Features[feature] - a, y = target(point) - b;
            numerator += x * y;
            aa += x * x;
            bb += y * y;
        }
        return aa == 0 || bb == 0 ? null : numerator / Math.Sqrt(aa * bb);
    }

    private sealed record EvaluationPoint(int Stage, double[] Features, double Outcome, double Margin, string Path,
        double Resilient, double OutcomeLower, double OutcomeUpper);
    private sealed record PolicyComparison(string Id, string Utility, int Stage, double Value, double Guarantee,
        double PureGuarantee, bool MixRequired, bool OptimalPure, bool Dominated, double OptimalPureMass,
        double DominatedMass, bool MissedReply, bool MissedBest, int? Depth, int? CompletedDepth,
        int RankingAgreements, int RankingPairs, double GapLower, double GapUpper);

    private sealed class HorizonStudy(BoardState root)
    {
        private readonly Dictionary<(SolvedPosition, int), Equilibrium> _cache = [];
        private readonly StrategicPosition _position = new(root, RuleSetDefinition.Get(RuleSet.KnightSupport), default);

        internal Equilibrium Game(SolvedPosition node, int depth, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (node.Result is not null) return node.Outcome!;
            if (_cache.TryGetValue((node, depth), out var cached)) return cached;
            Equilibrium result;
            if (depth == 0)
            {
                var state = MainPhaseState.Evaluate(node.Board, node.Rules);
                double h = _position.Evaluate(node.Board, state.Territory, state.GetLegalSelections(Red),
                    state.GetLegalSelections(Blue), token).Score(Red);
                result = new((Rational)(int)Math.Round(100_000 * h / (3 + Math.Abs(h))) / 100_000, [], []);
            }
            else result = MatrixGame.Solve(Matrix(node, depth, token), token.ThrowIfCancellationRequested);
            _cache[(node, depth)] = result;
            return result;
        }

        internal Rational[,] Matrix(SolvedPosition node, int depth, CancellationToken token)
        {
            var matrix = new Rational[node.Red.Length, node.Blue.Length];
            for (int i = 0; i < node.Red.Length; i++)
            for (int j = 0; j < node.Blue.Length; j++)
            {
                var edge = node.Child(i,j);
                var value = Game(edge.Target, depth - 1, token).Value;
                matrix[i,j] = edge.Exchange ? -value : value;
            }
            return matrix;
        }
    }
}
