using System.Collections.Frozen;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.AI;

internal sealed partial class SimultaneousSearch
{
    internal SearchResult AnalyzeDraft(GameSessionSnapshot snapshot, CpuCandidate[] choices, CpuCandidate[] opponent,
        bool diagnostics)
    {
        int replyCount = Math.Min(opponent.Length, _options.OpponentCandidates + 2);
        // Every start outside the common reply model needs an additional collision preview.
        while (replyCount > 1 && (long)choices.Length * replyCount + choices.Length - replyCount > _options.PairBudget)
            replyCount--;
        var replies = opponent[..replyCount];
        var weights = ReplyWeights(replies);
        var completed = new List<Rating>(choices.Length);
        foreach (var choice in choices)
        {
            var rating = RateDraft(snapshot, choice, replies, weights);
            if (rating is null) break;
            completed.Add(rating);
        }

        // Preserve the legacy static fallback when a usable draft pool did not fit.
        if (completed.Count < Math.Min(2, choices.Length))
            return Finish(choices, [], [], replies, weights, 0, diagnostics);
        completed.Sort(Compare);
        return Finish(choices, completed, completed, replies, weights, 1, diagnostics);
    }

    private Rating? RateDraft(GameSessionSnapshot snapshot, CpuCandidate choice, CpuCandidate[] replies, double[] weights)
    {
        bool collision = replies.Any(r => r.Position == choice.Position);
        bool extra = !collision && snapshot.EligiblePositions.Contains(choice.Position);
        int count = replies.Length + (extra ? 1 : 0);
        double expected = 0, adverse = double.PositiveInfinity;
        int area = snapshot.Board.Width * snapshot.Board.Height;
        for (int i = 0; i < count; i++)
        {
            if (!SpendPair()) return null;
            BoardPosition reply = i < replies.Length ? replies[i].Position : choice.Position;
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
            if (i < replies.Length) expected += weights[i] * value;
            adverse = Math.Min(adverse, value);
        }
        // Opening scores are normalized to preserve softmax variation on large maps.
        double scale = Math.Max(4, Math.Sqrt(area));
        double prior = 0.25 * choice.Score;
        return new(choice, expected / scale + prior, adverse / scale + prior,
            collision || extra, 1, 1, []);
    }
}
