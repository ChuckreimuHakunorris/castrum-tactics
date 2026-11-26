using System.Diagnostics;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.AI;

internal sealed class OpeningSearch(GameSessionSnapshot root, PlayerId player, CpuOpeningOptions options,
    CancellationToken token, long started, TimeSpan? timeLimit)
{
    private const double RestartCost = 0.08;
    private const double AdverseWeight = 0.4;
    private readonly BoardPosition[] _starts = root.EligiblePositions.ToArray();
    private OpeningPosition? _position;
    private StrategicPosition? _legacy;
    private StrategicPosition Legacy => _legacy ??= new(root.Board, RuleSetDefinition.Get(root.RuleSet), token);
    private readonly Dictionary<(string State, BoardPosition Own, BoardPosition Reply), GameSessionSnapshot> _pairs = [];
    private readonly Dictionary<(string State, int Depth, int Preview), double> _values = [];
    private int _work, _reused, _cachedStates, _restarts, _previews, _solves;
    private long _matrixTicks;
    private bool _budget, _time;

    internal SimultaneousSearch.SearchResult Analyze(CpuCandidate[] own, CpuCandidate[] opponent, bool diagnostics)
    {
        // One common set includes the collision for every modeled action.
        var tactical = new Dictionary<BoardPosition, double>();
        for (int i = 0; i < own.Length; i++)
        {
            tactical[own[i].Position] = tactical.GetValueOrDefault(own[i].Position) + 1.0 / (i + 1);
            tactical[opponent[i].Position] = tactical.GetValueOrDefault(opponent[i].Position) + 1.0 / (i + 1);
        }
        var priorities = tactical.OrderByDescending(p => p.Value).ThenBy(p => p.Key.Y).ThenBy(p => p.Key.X)
            .Select(p => p.Key).ToArray();
        var positions = priorities.Take(options.CandidateLimit).ToArray();
        Tier? completed = null;
        int depth = 0;
        for (int next = 1; next <= Math.Min(options.Depth, root.EligiblePositions.Count); next++)
        {
            var result = EvaluateMatrix(root, Key(root), positions, priorities, next, 0);
            if (result is null) break;
            completed = result;
            depth = next;
            if (next >= MaximumRemainingDepth(root)) break;
        }

        int previewDepth = 0;
        for (int preview = 1; preview <= options.MainPreviewDepth && completed is not null; preview++)
        {
            var result = EvaluateMatrix(root, Key(root), positions, priorities, depth, preview);
            if (result is null) break;
            completed = result;
            previewDepth = preview;
        }

        CpuSearchReport? report = null;
        if (diagnostics)
        {
            CpuActionAnalysis[] actions = completed is null ? [] : positions.Select((p, i) =>
                new CpuActionAnalysis(p, completed.Expected[i], completed.Adverse[i], true, depth)).ToArray();
            CpuReplyAnalysis[] replies = completed is null ? [] : positions.Select((p, i) =>
                new CpuReplyAnalysis(p, completed.Replies[i])).ToArray();
            report = new(actions, replies, null, _work, depth, _budget, _time, default)
            {
                CompletedDepth = depth, ReusedPairs = _reused,
                Opening = new(_restarts, _previews, previewDepth, completed is not null && positions.Length == _starts.Length, _cachedStates)
            };
            if (completed?.Matrix is { } m)
                report = report with { Matrix = new(m.Lower, m.Upper, m.Pivots, positions.Length,
                    positions.Length, positions.Length == _starts.Length, _solves,
                    Stopwatch.GetElapsedTime(0, _matrixTicks)) };
        }

        if (completed is null) return new(own, own.Length, report);
        bool samplePolicy = options.Policy != CpuOpeningPolicy.ExpectedAdverse;
        var candidates = own.ToDictionary(candidate => candidate.Position);
        var compared = positions.Select((p, i) => candidates[p] with
        {
            Score = completed.Ratings[i] * 8,
            Probability = completed.Policy[i]
        }).OrderByDescending(c => c.Score).ToArray();
        var ordered = compared.Concat(own.Where(c => !positions.Contains(c.Position))).ToArray();
        return new(ordered, compared.Length, report, samplePolicy);
    }

    private Tier? EvaluateMatrix(GameSessionSnapshot state, string key, BoardPosition[] actions,
        BoardPosition[] priorities, int depth, int preview)
    {
        if (Stopped()) return null;
        var matrix = new double[actions.Length, actions.Length];
        for (int i = 0; i < actions.Length; i++)
        for (int j = 0; j < actions.Length; j++)
        {
            token.ThrowIfCancellationRequested();
            var pair = (key, actions[i], actions[j]);
            if (!_pairs.TryGetValue(pair, out var child))
            {
                if (!Charge()) return null;
                child = player == PlayerId.PlayerOne
                    ? state.PreviewDraft(actions[i], actions[j]) : state.PreviewDraft(actions[j], actions[i]);
                _pairs[pair] = child;
                if (child.Phase == MatchPhase.OpeningRestartRequired) _restarts++;
            }
            else _reused++;

            string childKey = Key(child);
            var cached = (childKey, depth - 1, preview);
            if (!_values.TryGetValue(cached, out double value))
            {
                if (depth > 1 && child.Phase == MatchPhase.Opening)
                {
                    var remaining = priorities.Where(child.EligiblePositions.Contains).Take(options.CandidateLimit).ToArray();
                    var nested = EvaluateMatrix(child, childKey, remaining, priorities, depth - 1, preview);
                    if (nested is null) return null;
                    value = nested.Value;
                }
                else
                {
                    var estimated = EvaluateLeaf(child, preview);
                    if (estimated is null) return null;
                    value = estimated.Value;
                }
                _values[cached] = value;
            }
            else _cachedStates++;

            matrix[i, j] = value;
        }
        return Rate(matrix);
    }

    private double? EvaluateLeaf(GameSessionSnapshot state, int preview)
    {
        if (Stopped()) return null;
        // A restart delays both players. Penalize it in the draft model without assigning a terminal loss.
        if (state.Phase == MatchPhase.OpeningRestartRequired) return -RestartCost;
        if (state.FinalResult is { } result) return result.Winner is null ? 0 : result.Winner == player ? 1 : -1;
        var territory = state.Territory ?? TerritoryAnalyzer.Analyze(state.Board);
        double score = options.FrontierEvaluation
            ? (_position ??= new(root.Board)).Evaluate(state.Board, territory, state.EligiblePositions, token).Value(options.Synergy)
            : Bound(Legacy.Evaluate(state.Board, territory, state.GetMainPhaseLegalSelections(PlayerId.PlayerOne),
                state.GetMainPhaseLegalSelections(PlayerId.PlayerTwo), token, opening: true).Score(PlayerId.PlayerOne));
        if (player == PlayerId.PlayerTwo) score = -score;
        if (preview == 0 || state.MainState is null) return score;
        var tactical = PreviewMain(state.MainState, preview, score);
        return tactical is null ? null : 0.45 * tactical.Value + 0.55 * score;
    }

    private double? PreviewMain(MainPhaseState state, int depth, double unexamined)
    {
        if (Stopped()) return null;
        if (state.Result is { } result) return result.Winner is null ? 0 : result.Winner == player ? 1 : -1;
        var opponent = player == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
        var mine = state.GetLegalSelections(player);
        var theirs = state.GetLegalSelections(opponent);
        double score = Bound(Legacy.Evaluate(state.Board, state.Territory,
            state.GetLegalSelections(PlayerId.PlayerOne), state.GetLegalSelections(PlayerId.PlayerTwo),
            token, resilient: true, prepareFrontier: false).Score(player));
        // Preview only compact matrices with every legal reply. Wide boards retain the draft estimate.
        if (depth == 0) return score;
        if ((long)mine.Count * theirs.Count > 36) return unexamined;
        var matrix = new double[mine.Count, theirs.Count];
        int i = 0;
        foreach (var own in mine)
        {
            int j = 0;
            foreach (var reply in theirs)
            {
                if (!Charge()) return null;
                _previews++;
                var child = state.Advance(new(player, own), new(opponent, reply));
                var value = PreviewMain(child, depth - 1, score);
                if (value is null) return null;
                matrix[i, j++] = value.Value;
            }
            i++;
        }
        return Rate(matrix)?.Value;
    }

    private Tier? Rate(double[,] matrix)
    {
        if (Stopped()) return null;
        int rows = matrix.GetLength(0), columns = matrix.GetLength(1);
        var expected = new double[rows];
        var adverse = new double[rows];
        var ratings = new double[rows];
        var reply = Enumerable.Repeat(1.0 / columns, columns).ToArray();
        for (int i = 0; i < rows; i++)
        {
            adverse[i] = 1;
            for (int j = 0; j < columns; j++)
            {
                expected[i] += reply[j] * matrix[i, j];
                adverse[i] = Math.Min(adverse[i], matrix[i, j]);
            }
            ratings[i] = options.Policy == CpuOpeningPolicy.WorstCase
                ? adverse[i] : (1 - AdverseWeight) * expected[i] + AdverseWeight * adverse[i];
        }

        if (options.Policy == CpuOpeningPolicy.Equilibrium)
        {
            long start = Stopwatch.GetTimestamp();
            _solves++;
            var game = NumericalMatrixGame.Solve(matrix, token, Stopped);
            _matrixTicks += Stopwatch.GetTimestamp() - start;
            return game is null ? null : new(expected, adverse, ratings, game.Row, game.Column, game.Value, game);
        }
        double best = ratings.Max();
        int ties = ratings.Count(v => Math.Abs(best - v) <= 1e-12);
        return new(expected, adverse, ratings, ratings.Select(v => Math.Abs(best - v) <= 1e-12 ? 1.0 / ties : 0)
            .ToArray(), reply, best, null);
    }

    private bool Charge()
    {
        if (Stopped()) return false;
        if (_work == options.PairBudget) { _budget = true; return false; }
        _work++;
        return true;
    }

    private bool Stopped()
    {
        token.ThrowIfCancellationRequested();
        return _time |= timeLimit is { } duration && Stopwatch.GetElapsedTime(started) >= duration;
    }

    // A decision fixes the terrain, rules and original eligible set. Only those cells can change,
    // and neither the phase nor future choices depend on how many submissions produced the board.
    private string Key(GameSessionSnapshot state) => string.Create(checked(3 * _starts.Length), (state, _starts), (key, data) =>
    {
        for (int i = 0; i < data._starts.Length; i++)
        {
            var cell = data.state.Board.GetCell(data._starts[i]);
            key[3 * i] = (char)(((int)cell.Piece << 2) | (int)(cell.Owner ?? 0));
            key[3 * i + 1] = (char)cell.GroundHealth;
            key[3 * i + 2] = (char)(cell.GroundHealth >> 16);
        }
    });

    private static int MaximumRemainingDepth(GameSessionSnapshot state) => Math.Max(1, state.EligiblePositions.Count - 1);
    private double Bound(double score) => 0.85 * score / (Math.Max(4, Math.Sqrt(root.Board.Width * (double)root.Board.Height)) +
        Math.Abs(score));

    private sealed record Tier(double[] Expected, double[] Adverse, double[] Ratings, double[] Policy,
        double[] Replies, double Value, NumericalMatrixGame.Solution? Matrix);
}
