extern alias frozen8e;

using System.Text.Json;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Xunit;
using Frozen = frozen8e::CastrumTactics.AI;

namespace CastrumTactics.AI.Tests;

public sealed class Frozen8EParityTests
{
    private const PlayerId Red = PlayerId.PlayerOne;
    private const PlayerId Blue = PlayerId.PlayerTwo;

    [Theory]
    [InlineData(RuleSet.Classic)]
    [InlineData(RuleSet.KnightSupport)]
    public void ProductionMatchesFrozenSourceAcrossCatalogAndResolvedPlay(RuleSet rules)
    {
        var production = new CpuDecisionEngine();
        var reference = new Frozen.CpuDecisionEngine();
        foreach (var map in PredefinedMapCatalog.BuiltIn.Maps.Concat(
            CastrumTactics.Development.HistoricalBattlefields.Maps.Where(map =>
                map.Id is "pocket-meadow" or "corner-watch" or "stone-center" or "three-banners")))
        {
            var session = new LocalGameSession(map.Definition, rules);
            Compare(session.Current);
            var start = session.Current.EligiblePositions[0];
            var collision = session.Current.PreviewDraft(start, start);
            Compare(collision);
            if (collision.Phase == MatchPhase.OpeningRestartRequired)
            {
                SubmitDraft(session, start, start);
                Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(session.Current.RoundId));
                Compare(session.Current);
            }

            while (session.Current.Phase == MatchPhase.Opening)
            {
                var starts = session.Current.EligiblePositions;
                SubmitDraft(session, starts[0], starts[^1]);
                Compare(session.Current);
            }

            if (map.Id is "the-commons" or "honeystone-green" or "twelve-towers" or "pocket-meadow"
                or "corner-watch" or "stone-center" or "three-banners")
            {
                var checkpoints = new List<GameSessionSnapshot>();
                var stream = new Random(5953);
                while (session.Current.Phase == MatchPhase.Main)
                {
                    var current = session.Current;
                    checkpoints.Add(current);
                    Play(Red);
                    Play(Blue);

                    void Play(PlayerId player)
                    {
                        var legal = current.GetMainPhaseLegalSelections(player).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
                        Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(
                            new(player, legal[stream.Next(legal.Length)], current.RoundId)));
                    }
                }
                if (checkpoints.Count > 0)
                {
                    Compare(checkpoints[checkpoints.Count / 2]);
                    Compare(checkpoints[^1]);
                    Compare(session.Current);
                }
            }
        }

        void Compare(GameSessionSnapshot state)
        {
            foreach (var level in Enum.GetValues<CpuDifficulty>())
            foreach (var side in new[] { Red, Blue })
            {
                var expectedRandom = new Random(947);
                var actualRandom = new Random(947);
                var expected = reference.DecideWithDiagnostics(state, side,
                    Frozen.CpuDifficultyProfiles.Get((Frozen.CpuDifficulty)level), expectedRandom);
                var actual = production.DecideWithDiagnostics(state, side, CpuDifficultyProfiles.Get(level), actualRandom);
                Assert.Equal(expectedRandom.Next(), actualRandom.Next());
                if (actual.Decision is { } action)
                {
                    IEnumerable<BoardPosition> legal = state.Phase == MatchPhase.Opening
                        ? state.EligiblePositions : state.GetMainPhaseLegalSelections(side);
                    Assert.Contains(action.Position, legal);
                }

                // These records have separate assembly identities. Compare their full serialized values,
                // ignoring only wall-clock fields that do not participate in deterministic selection.
                var frozenSearch = expected.Search is { } oldSearch ? oldSearch with
                {
                    Elapsed = TimeSpan.Zero, Matrix = oldSearch.Matrix is { } m ? m with { Elapsed = TimeSpan.Zero } : null
                } : null;
                var liveSearch = actual.Search is { } newSearch ? newSearch with
                {
                    Elapsed = TimeSpan.Zero, Matrix = newSearch.Matrix is { } matrix ? matrix with { Elapsed = TimeSpan.Zero } : null
                } : null;
                Assert.Equal(JsonSerializer.Serialize(new { expected.Decision, expected.Candidates, Search = frozenSearch }),
                    JsonSerializer.Serialize(new { actual.Decision, actual.Candidates, Search = liveSearch }));
            }
        }
    }

    private static void SubmitDraft(LocalGameSession session, BoardPosition red, BoardPosition blue)
    {
        long round = session.Current.RoundId;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Blue, blue, round)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Red, red, round)));
    }
}
