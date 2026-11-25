using System.Collections.Frozen;
using System.Diagnostics;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.AI;

// Each node uses one common response distribution. Extra collisions are risk checks only.
internal sealed class SimultaneousSearch
{
    private readonly PlayerId _player;
    private readonly PlayerId _opponent;
    private readonly CpuProfile _profile;
    private readonly CpuSearchOptions _options;
    private readonly CancellationToken _token;
    private readonly StrategicPosition _position;
    private readonly long _start;
    private readonly TimeSpan? _timeLimit;
    private CpuStrategicFeatures? _rootFeatures;
    private int _pairs;
    private bool _budgetExhausted;
    private bool _timeLimitReached;

    internal SimultaneousSearch(GameSessionSnapshot snapshot, PlayerId player, PlayerId opponent, CpuProfile profile,
        CancellationToken token, long start, TimeSpan? timeLimit)
    {
        _player = player;
        _opponent = opponent;
        _profile = profile;
        _options = profile.Search!;
        _token = token;
        _start = start;
        _timeLimit = timeLimit;
        _position = new(snapshot.Board, RuleSetDefinition.Get(snapshot.RuleSet), token);
    }

    internal SearchResult Analyze(GameSessionSnapshot snapshot, CpuCandidate[] own, CpuCandidate[] opponent, bool diagnostics)
    {
        bool opening = snapshot.Phase == MatchPhase.Opening;
        CpuCandidate[] choices;
        CpuCandidate[] replies;
        if (opening)
        {
            // Draft starts have different long-term consequences. Compare all when the budget permits.
            choices = own;
            replies = opponent.Take(Math.Min(_options.OpponentCandidates + 2,
                Math.Max(1, _options.PairBudget / own.Length))).ToArray();
        }
        else
        {
            var features = EvaluateFeatures(snapshot.MainState!);
            if (diagnostics) _rootFeatures = features.Contributions(_player);
            choices = Shortlist(snapshot.Board, own, _player, _options.OwnCandidates);
            replies = Shortlist(snapshot.Board, opponent, _opponent, _options.OpponentCandidates);
        }

        double[] weights = ReplyWeights(replies);
        int depth = 0;
        var first = new List<Rating>(choices.Length);
        foreach (CpuCandidate choice in choices)
        {
            Rating? rating = opening
                ? RateDraft(snapshot, choice, replies, weights)
                : RateMain(snapshot.MainState!, choice, replies, weights, _options.Depth > 1);
            if (rating is null) break;
            first.Add(rating);
        }

        // Only completed rows share comparable reply weights. Preserve draft variation whenever it is possible.
        if (first.Count < (opening ? Math.Min(2, choices.Length) : 1))
            return Finish(own, [], [], replies, weights, 0, diagnostics);

        first.Sort(Compare);
        depth = 1;
        IReadOnlyList<Rating> finalists = first;
        if (!opening && _options.Depth > 1 && first.Count == choices.Length && first[0].Safety < 2)
        {
            int finalistCount = _options.OwnCandidates >= 16 ? 6 : 4;
            int width = _options.OwnCandidates >= 16 ? 4 : 3;
            Rating[] comparable = first.Where(r => r.Safety == first[0].Safety).Take(finalistCount).ToArray();
            // Budget conservatively for equal-depth comparison of every retained finalist.
            if (comparable.Sum(EstimateCost) > _options.PairBudget - _pairs) width = 3;
            while (comparable.Length > 2 && comparable.Sum(EstimateCost) > _options.PairBudget - _pairs)
                comparable = comparable[..^1];
            if (snapshot.Board.Width * snapshot.Board.Height <= 25)
            {
                // Solve denser reply matrices on small boards when the same budget covers them.
                while (width < _options.OpponentCandidates)
                {
                    width++;
                    if (comparable.Sum(EstimateCost) <= _options.PairBudget - _pairs) continue;
                    width--;
                    break;
                }
            }
            var second = new List<Rating>(comparable.Length);
            foreach (Rating rating in comparable)
            {
                Rating? refined = Refine(rating, weights, width);
                if (refined is null) break;
                second.Add(refined);
            }
            if (second.Count == comparable.Length && second.Any(r => r.Depth == 2))
            {
                second.Sort(Compare);
                finalists = second;
                depth = 2;
            }

            int EstimateCost(Rating rating) => rating.Children.Sum(child => child.IsFinished ? 0 :
                Math.Min(width, child.GetLegalSelections(_player).Count) *
                Math.Min(width + 1, child.GetLegalSelections(_opponent).Count));
        }

        return Finish(own, finalists, first, replies, weights, depth, diagnostics);
    }

    private SearchResult Finish(CpuCandidate[] own, IReadOnlyList<Rating> finalists, IReadOnlyList<Rating> first,
        CpuCandidate[] replies, double[] weights, int depth, bool diagnostics)
    {
        if (depth == 0)
            return new(own, own.Length, CreateReport([], replies, weights, depth, diagnostics));

        var ranked = new List<CpuCandidate>(own.Length);
        var analyses = diagnostics ? new List<CpuActionAnalysis>(first.Count) : null;
        var included = new HashSet<BoardPosition>();
        void Include(Rating rating)
        {
            if (!included.Add(rating.Candidate.Position)) return;
            ranked.Add(rating.Candidate with { Score = rating.Value(_options.Risk) });
            analyses?.Add(new(rating.Candidate.Position, rating.Expected, rating.Adverse, rating.Collision, rating.Depth));
        }

        foreach (Rating rating in finalists) Include(rating);
        int selectable = finalists.Count(r => r.Safety == finalists[0].Safety);
        foreach (Rating rating in first) Include(rating);
        ranked.AddRange(own.Where(c => !included.Contains(c.Position)));
        return new(ranked.ToArray(), selectable, CreateReport(analyses, replies, weights, depth, diagnostics));
    }

    private CpuSearchReport? CreateReport(List<CpuActionAnalysis>? analyses, CpuCandidate[] replies, double[] weights,
        int depth, bool diagnostics) => diagnostics
        ? new(analyses!.AsReadOnly(), Array.AsReadOnly(replies.Select((r, i) =>
            new CpuReplyAnalysis(r.Position, weights[i])).ToArray()),
            _rootFeatures, _pairs, depth, _budgetExhausted, _timeLimitReached, TimeSpan.Zero)
        : null;

    private Rating? RateDraft(GameSessionSnapshot snapshot, CpuCandidate choice, CpuCandidate[] replies, double[] weights)
    {
        double expected = 0, adverse = double.PositiveInfinity;
        int area = snapshot.Board.Width * snapshot.Board.Height;
        for (int i = 0; i < replies.Length; i++)
        {
            if (!SpendPair()) return null;
            BoardPosition reply = replies[i].Position;
            var next = _player == PlayerId.PlayerOne
                ? snapshot.PreviewDraft(choice.Position, reply)
                : snapshot.PreviewDraft(reply, choice.Position);
            double value;
            if (next.Phase == MatchPhase.OpeningRestartRequired)
            {
                value = -4;
            }
            else if (next.FinalResult is { } result)
            {
                int margin = Margin(result.PlayerOneScore - result.PlayerTwoScore);
                value = Math.Sign(margin) * (3 + 0.6 * area) + 0.3 * margin;
            }
            else
            {
                var state = next.MainState;
                var empty = FrozenSet<BoardPosition>.Empty;
                value = _position.Evaluate(next.Board, next.Territory ?? TerritoryAnalyzer.Analyze(next.Board),
                    state?.GetLegalSelections(PlayerId.PlayerOne) ?? empty,
                    state?.GetLegalSelections(PlayerId.PlayerTwo) ?? empty, _token, opening: true).Score(_player, opening: true);
            }
            expected += weights[i] * value;
            adverse = Math.Min(adverse, value);
        }
        // Opening scores are normalized to preserve softmax variation on large maps.
        double scale = Math.Max(4, Math.Sqrt(area));
        double prior = 0.25 * choice.Score;
        return new(choice, expected / scale + prior, adverse / scale + prior,
            replies.Any(r => r.Position == choice.Position), 1, 1, []);
    }

    private Rating? RateMain(MainPhaseState state, CpuCandidate choice, CpuCandidate[] replies, double[] weights,
        bool retainChildren)
    {
        bool collision = replies.Any(r => r.Position == choice.Position);
        bool extra = !collision && state.GetLegalSelections(_opponent).Contains(choice.Position);
        int count = replies.Length + (extra ? 1 : 0);
        var children = retainChildren ? new MainPhaseState[count] : [];
        double expected = 0, adverse = double.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (!SpendPair()) return null;
            BoardPosition reply = i < replies.Length ? replies[i].Position : choice.Position;
            MainPhaseState child = state.Advance(new(_player, choice.Position), new(_opponent, reply));
            double value = Evaluate(child);
            if (retainChildren) children[i] = child;
            if (i < replies.Length) expected += weights[i] * value;
            adverse = Math.Min(adverse, value);
        }
        int safety = adverse >= StrategicPosition.TerminalWin ? 2 : adverse <= -StrategicPosition.TerminalWin ? 0 : 1;
        return new(choice, expected, adverse, collision || extra, 1, safety, children);
    }

    private Rating? Refine(Rating first, double[] rootWeights, int width)
    {
        double expected = 0, adverse = double.PositiveInfinity;
        bool searched = false;
        for (int i = 0; i < first.Children.Length; i++)
        {
            MainPhaseState child = first.Children[i];
            double value = Evaluate(child);
            if (!child.IsFinished)
            {
                var tactical = new TacticalBoard(child.Board, false, _token, child.RuleSet);
                CpuCandidate[] own = Shortlist(child.Board, Rank(child, tactical, _player, _opponent), _player, width);
                CpuCandidate[] replies = Shortlist(child.Board, Rank(child, tactical, _opponent, _player), _opponent, width);
                double[] weights = ReplyWeights(replies);
                Rating? best = null;
                foreach (CpuCandidate choice in own)
                {
                    Rating? option = RateMain(child, choice, replies, weights, retainChildren: false);
                    if (option is null) return null;
                    if (best is null || Compare(option, best) < 0) best = option;
                }
                // Blend estimates only for a nonterminal continuation. Preserve tactical terminal signals.
                double continuation = best!.Value(_options.Risk);
                value = 0.15 * value + 0.85 * continuation;
                searched = true;
            }
            if (i < rootWeights.Length) expected += rootWeights[i] * value;
            adverse = Math.Min(adverse, value);
        }
        return first with { Expected = expected, Adverse = adverse, Depth = searched ? 2 : 1 };
    }

    private CpuCandidate[] Rank(MainPhaseState state, TacticalBoard tactical, PlayerId player, PlayerId opponent) =>
        CpuDecisionEngine.RankMain(tactical, state.Territory, player, opponent,
            state.GetLegalSelections(player), state.GetLegalSelections(opponent), _profile, true, _token);

    private CpuCandidate[] Shortlist(BoardState board, CpuCandidate[] ranked, PlayerId player, int limit)
    {
        if (ranked.Length <= limit) return ranked;

        int baseCount = Math.Max(1, (limit + 1) / 2);
        var selected = ranked.Take(baseCount).ToList();
        var remaining = ranked.Skip(baseCount)
            .Select(c => (Candidate: c, Frontier: _position.Frontier(c.Position, player))).ToList();
        // Preserve tactical upgrades and captures alongside geographic alternatives.
        AddBest(c => board.GetCell(c.Position) is { Piece: PieceType.Knight } cell && cell.Owner == player);
        AddBest(c => c.Features.Capture > 0 || c.Features.SupportRisk > 0);
        while (selected.Count < limit)
        {
            _token.ThrowIfCancellationRequested();
            int best = 0;
            double highest = double.NegativeInfinity;
            for (int i = 0; i < remaining.Count; i++)
            {
                var (candidate, frontier) = remaining[i];
                int distance = selected.Min(other => Distance(other.Position, candidate.Position));
                double priority = candidate.Score + 1.8 * frontier + 0.22 * Math.Min(5, distance);
                if (priority <= highest) continue;
                best = i;
                highest = priority;
            }
            Add(best);
        }
        return selected.ToArray();

        void AddBest(Func<CpuCandidate, bool> predicate)
        {
            if (selected.Count >= limit) return;
            int i = remaining.FindIndex(c => predicate(c.Candidate));
            if (i >= 0) Add(i);
        }

        void Add(int i)
        {
            selected.Add(remaining[i].Candidate);
            remaining.RemoveAt(i);
        }
    }

    private static int Distance(BoardPosition a, BoardPosition b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private double[] ReplyWeights(CpuCandidate[] replies)
    {
        double best = replies.Max(r => r.Score);
        double[] weights = replies.Select(r => Math.Exp((r.Score - best) / _options.ReplyTemperature)).ToArray();
        double total = weights.Sum();
        for (int i = 0; i < weights.Length; i++)
            weights[i] = 0.90 * weights[i] / total + 0.10 / weights.Length;
        return weights;
    }

    private double Evaluate(MainPhaseState state)
    {
        if (state.Result is { } result)
        {
            int margin = Margin(result.PlayerOneScore - result.PlayerTwoScore);
            return Math.Sign(margin) * StrategicPosition.TerminalWin + Math.Clamp(margin, -1000, 1000);
        }
        return EvaluateFeatures(state).Score(_player);
    }

    private StrategicPosition.PositionFeatures EvaluateFeatures(MainPhaseState state) =>
        _position.Evaluate(state.Board, state.Territory, state.GetLegalSelections(PlayerId.PlayerOne),
            state.GetLegalSelections(PlayerId.PlayerTwo), _token);

    private int Margin(int oneLead) => _player == PlayerId.PlayerOne ? oneLead : -oneLead;

    private bool SpendPair()
    {
        _token.ThrowIfCancellationRequested();
        if (_timeLimit is { } timeLimit && Stopwatch.GetElapsedTime(_start) >= timeLimit)
        {
            _timeLimitReached = true;
            return false;
        }
        if (_pairs < _options.PairBudget)
        {
            _pairs++;
            return true;
        }
        _budgetExhausted = true;
        return false;
    }

    private int Compare(Rating a, Rating b)
    {
        int safety = b.Safety.CompareTo(a.Safety);
        if (safety != 0) return safety;
        int value = b.Value(_options.Risk).CompareTo(a.Value(_options.Risk));
        if (value != 0) return value;
        int y = a.Candidate.Position.Y.CompareTo(b.Candidate.Position.Y);
        return y == 0 ? a.Candidate.Position.X.CompareTo(b.Candidate.Position.X) : y;
    }

    internal readonly record struct SearchResult(CpuCandidate[] Candidates, int Selectable, CpuSearchReport? Report);

    private sealed record Rating(CpuCandidate Candidate, double Expected, double Adverse, bool Collision,
        int Depth, int Safety, MainPhaseState[] Children)
    {
        internal double Value(double risk) => (1 - risk) * Expected + risk * Adverse;
    }
}
