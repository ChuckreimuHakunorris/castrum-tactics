using CastrumTactics.Domain;

namespace CastrumTactics.AI;

internal sealed partial class SimultaneousSearch
{
    private const int RetainedPairs = 512;
    private Dictionary<(MainPhaseState State, BoardPosition Own, BoardPosition Reply), ResolvedPair>? _retained;
    private int _reusedPairs;

    private bool TryResolve(MainPhaseState state, BoardPosition own, BoardPosition reply, bool cache, out ResolvedPair outcome)
    {
        var key = (state, own, reply);
        if (_retained is not null && _retained.TryGetValue(key, out outcome))
        {
            if (ShouldStop()) return false;
            _reusedPairs++;
            return true;
        }

        if (!SpendPair())
        {
            outcome = default;
            return false;
        }
        MainPhaseState child = state.Advance(new(_player, own), new(_opponent, reply));
        outcome = new(child, Evaluate(child));
        if (cache && (_retained?.Count ?? 0) < RetainedPairs)
            (_retained ??= new()).Add(key, outcome);
        return true;
    }

    private void VerifyImmediateWins(MainPhaseState state, CpuCandidate[] replies, List<Rating> ratings)
    {
        for (int i = 0; i < ratings.Count && ratings[i].Safety == 2; i++)
        {
            var rating = ratings[i];
            var own = rating.Candidate.Position;
            double adverse = rating.Adverse;
            foreach (BoardPosition reply in state.GetLegalSelections(_opponent))
            {
                if (own == reply || replies.Any(known => known.Position == reply)) continue;
                if (!TryResolve(state, own, reply, cache: false, out var result))
                {
                    ratings.Sort(Compare);
                    return;
                }
                adverse = Math.Min(adverse, result.Value);
                // A single refutation disproves the apparent win. Keep the risk information it uncovered.
                if (adverse < StrategicPosition.TerminalWin) break;
            }
            int safety = adverse >= StrategicPosition.TerminalWin ? 3 :
                adverse <= -StrategicPosition.TerminalWin ? 0 : 1;
            ratings[i] = rating with { Adverse = adverse, Safety = safety };
        }
        ratings.Sort(Compare);
    }

    private Rating? RefineDeep(Rating root, double[] weights, int depth)
    {
        double expected = 0, adverse = double.PositiveInfinity;
        int reached = 1;
        bool win = root.Exhaustive;
        for (int i = 0; i < root.Children.Length; i++)
        {
            var child = root.Children[i];
            double leaf = Evaluate(child);
            if (!TryContinue(new(child, leaf), depth - 1, depth < _options.Depth, out var branch)) return null;
            if (i < weights.Length) expected += weights[i] * branch.Value;
            adverse = Math.Min(adverse, branch.Value);
            win &= branch.ProvenWin;
            reached = Math.Max(reached, 1 + branch.Turns);
        }
        return root with { Expected = expected, Adverse = adverse, Depth = reached, Safety = win ? 3 : root.Safety };
    }

    private bool TryContinue(ResolvedPair position, int remaining, bool keep, out Continuation result)
    {
        var (state, leaf) = position;
        result = new(leaf, state.Result?.Winner == _player, 0);
        if (remaining == 0 || state.IsFinished) return true;
        if (ShouldStop()) return false;

        // Recreate the frontier for this state before all ordering. Both shortlists precede any pair resolution.
        EvaluateFeatures(state);
        var tactical = new TacticalBoard(state.Board, false, _token, state.RuleSet);
        var own = Shortlist(state.Board, Rank(state, tactical, _player, _opponent), _player,
            _options.ContinuationOwnCandidates);
        var replies = Shortlist(state.Board, Rank(state, tactical, _opponent, _player), _opponent,
            _options.ContinuationOpponentCandidates);
        var weights = ReplyWeights(replies);
        var opponentLegal = state.GetLegalSelections(_opponent);
        var best = new Continuation(double.NegativeInfinity, false, 0);
        int explored = 0;

        foreach (var choice in own)
        {
            var move = choice.Position;
            bool extra = opponentLegal.Contains(move) && !replies.Any(reply => reply.Position == move);
            int count = replies.Length + (extra ? 1 : 0);
            bool win = count == opponentLegal.Count;
            double expected = 0, adverse = double.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var reply = i < replies.Length ? replies[i].Position : move;
                if (!TryResolve(state, move, reply, keep, out var child) ||
                    !TryContinue(child, remaining - 1, keep, out var branch)) return false;

                if (i < weights.Length) expected += weights[i] * branch.Value;
                adverse = Math.Min(adverse, branch.Value);
                win &= branch.ProvenWin;
                explored = Math.Max(explored, 1 + branch.Turns);
            }

            double value = (1 - _options.Risk) * expected + _options.Risk * adverse;
            if (win && !best.ProvenWin || win == best.ProvenWin && value > best.Value)
                best = new(value, win, explored);
        }

        // Blend heuristic continuations only. A terminal-scale outcome keeps its magnitude, and proof is separate.
        double estimate = Math.Abs(best.Value) >= StrategicPosition.TerminalWin
            ? best.Value : 0.15 * leaf + 0.85 * best.Value;
        result = new(estimate, best.ProvenWin, explored);
        return true;
    }

    private readonly record struct ResolvedPair(MainPhaseState State, double Value);
    private readonly record struct Continuation(double Value, bool ProvenWin, int Turns);
}
