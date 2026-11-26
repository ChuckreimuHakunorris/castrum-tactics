extern alias frozen8d3;

using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Xunit;
using Frozen = frozen8d3::CastrumTactics.AI;

namespace CastrumTactics.AI.Tests;

public sealed class Frozen8D3ParityTests
{
    [Theory]
    [InlineData(RuleSet.KnightSupport)]
    [InlineData(RuleSet.Classic)]
    public void FrozenMainAndDisabledAdvancedDraftMatchAcrossTheCatalog(RuleSet rules)
    {
        var engine = new CpuDecisionEngine();
        var frozen = new Frozen.CpuDecisionEngine();
        foreach (var map in PredefinedMapCatalog.BuiltIn.Maps)
        {
            var initial = new LocalGameSession(map.Definition, rules).Current;
            var draft = initial;
            while (draft.Phase == MatchPhase.Opening)
            {
                Compare(draft);
                var first = draft.EligiblePositions[0];
                var last = draft.EligiblePositions[^1];
                draft = draft.PreviewDraft(first, last);
            }
            var main = draft.MainState;
            if (main is not null && !main.IsFinished) Compare(draft);

            void Compare(GameSessionSnapshot state)
            {
                foreach (var level in Enum.GetValues<CpuDifficulty>())
                {
                    var current = CpuDifficultyProfiles.Get(level);
                    // The explicit 8D.3 profile opts out of any later opening-only policy.
                    var profile = state.Phase == MatchPhase.Main || rules == RuleSet.Classic ? current : new CpuProfile(current.Weights, current.OpeningCandidateLimit, current.MainCandidateLimit,
                        current.MainTemperature, current.OpeningTemperature, current.OpeningDiversity,
                        current.UrgentOpeningDiversity, current.OpeningMaximumScoreDifference, current.MainMaximumScoreDifference)
                        { Search = current.Search };
                    foreach (var side in new[] { PlayerId.PlayerOne, PlayerId.PlayerTwo })
                    {
                        var actual = engine.DecideWithDiagnostics(state, side, profile, new Random(451));
                        var expected = frozen.DecideWithDiagnostics(state, side,
                            Frozen.CpuDifficultyProfiles.Get((Frozen.CpuDifficulty)level), new Random(451));
                        Assert.Equal(expected.Decision?.Position, actual.Decision?.Position);
                        Assert.Equal(expected.Search?.ResolvedPairs, actual.Search?.ResolvedPairs);
                        Assert.Equal(expected.Search?.Depth, actual.Search?.Depth);
                        Assert.Equal(expected.Candidates.Select(c => (c.Position, c.Score, c.Probability)),
                            actual.Candidates.Select(c => (c.Position, c.Score, c.Probability)));
                    }
                }
            }
        }
    }
}
