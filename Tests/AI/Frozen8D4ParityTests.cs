extern alias frozen8d4;

using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Xunit;
using Frozen = frozen8d4::CastrumTactics.AI;

namespace CastrumTactics.AI.Tests;

public sealed class Frozen8D4ParityTests
{
    [Theory]
    [InlineData(RuleSet.KnightSupport)]
    [InlineData(RuleSet.Classic)]
    public void FrozenDraftAndMainMatchTheReferenceAcrossTheCatalog(RuleSet rules)
    {
        var engine = new CpuDecisionEngine();
        var frozen = new Frozen.CpuDecisionEngine();
        var calibrated = CpuDifficultyProfiles.Get(CpuDifficulty.VeryHard);
        var limits = calibrated.Search!;
        Assert.True(limits.ResilientEvaluation);
        Assert.False(limits.BoundedUtility);
        Assert.Equal(MatrixPolicy.Off, limits.Equilibrium);
        Assert.Null(calibrated.OpeningSearch);
        Assert.Equal((2, 14, 9, 500), (limits.Depth, limits.OwnCandidates, limits.OpponentCandidates, limits.PairBudget));
        var priorVeryHard = new CpuProfile(calibrated.Weights, calibrated.OpeningCandidateLimit, calibrated.MainCandidateLimit,
            calibrated.MainTemperature, calibrated.OpeningTemperature, calibrated.OpeningDiversity,
            calibrated.UrgentOpeningDiversity, calibrated.OpeningMaximumScoreDifference, calibrated.MainMaximumScoreDifference)
        {
            Search = new(2, 14, 9, 500, 0.36, 1.1)
        };
        int changedDecisions = 0;
        foreach (var map in PredefinedMapCatalog.BuiltIn.Maps)
        {
            var session = new LocalGameSession(map.Definition, rules);
            while (session.Current.Phase == MatchPhase.Opening)
            {
                var draft = session.Current;
                Compare(draft);
                Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(
                    new(PlayerId.PlayerOne, draft.EligiblePositions[0], draft.RoundId)));
                Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(
                    new(PlayerId.PlayerTwo, draft.EligiblePositions[^1], draft.RoundId)));
            }
            for (int turn = 0; turn < 3 && session.Current.Phase == MatchPhase.Main; turn++)
            {
                var state = session.Current;
                Compare(state);
                var red = state.GetMainPhaseLegalSelections(PlayerId.PlayerOne).First();
                var blue = state.GetMainPhaseLegalSelections(PlayerId.PlayerTwo).Last();
                Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(
                    new(PlayerId.PlayerOne, red, state.RoundId)));
                Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(
                    new(PlayerId.PlayerTwo, blue, state.RoundId)));
            }

            void Compare(GameSessionSnapshot state)
            {
                foreach (var level in Enum.GetValues<CpuDifficulty>())
                {
                    var current = CpuDifficultyProfiles.Get(level);
                    bool calibratedMain = level == CpuDifficulty.VeryHard && rules == RuleSet.KnightSupport &&
                        state.Phase == MatchPhase.Main;
                    foreach (var side in new[] { PlayerId.PlayerOne, PlayerId.PlayerTwo })
                    {
                        var actual = engine.DecideWithDiagnostics(state, side,
                            calibratedMain ? priorVeryHard : current, new Random(451));
                        if (calibratedMain)
                        {
                            var promoted = engine.DecideWithDiagnostics(state, side, current, new Random(451));
                            Assert.NotNull(promoted.Decision);
                            Assert.Contains(promoted.Decision.Value.Position, state.GetMainPhaseLegalSelections(side));
                            Assert.InRange(promoted.Search!.ResolvedPairs, 0, limits.PairBudget);
                            if (promoted.Decision != actual.Decision) changedDecisions++;
                        }
                        var expected = frozen.DecideWithDiagnostics(state, side,
                            Frozen.CpuDifficultyProfiles.Get((Frozen.CpuDifficulty)level), new Random(451));
                        Assert.NotNull(actual.Decision);
                        Assert.Equal(expected.Decision?.Position, actual.Decision.Value.Position);
                        Assert.Contains(actual.Decision.Value.Position, state.Phase == MatchPhase.Opening
                            ? state.EligiblePositions.AsEnumerable() : state.GetMainPhaseLegalSelections(side));
                        Assert.Equal(expected.Search?.ResolvedPairs, actual.Search?.ResolvedPairs);
                        Assert.Equal(expected.Search?.Depth, actual.Search?.Depth);
                        Assert.Equal(expected.Candidates.Select(c => (c.Position, c.Score, c.Probability)),
                            actual.Candidates.Select(c => (c.Position, c.Score, c.Probability)));
                    }
                }
            }
        }
        if (rules == RuleSet.KnightSupport) Assert.True(changedDecisions > 0);
    }
}
