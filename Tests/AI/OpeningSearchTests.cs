extern alias benchmarks;

using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Xunit;
using Tournament = benchmarks::Program;

namespace CastrumTactics.AI.Tests;

public sealed class OpeningSearchTests
{
    private const PlayerId Red = PlayerId.PlayerOne;
    private const PlayerId Blue = PlayerId.PlayerTwo;

    [Fact]
    public void EmptyTwoStartGameMixesAndClassifiesRestartsSeparately()
    {
        // Use the archived two-start layout for the matrix fixture.
        var root = Open(CastrumTactics.Development.HistoricalBattlefields.Get("pocket-meadow").Definition);
        var profile = Profile(1, 16, 80);
        var engine = new CpuDecisionEngine();
        var report = engine.DecideWithDiagnostics(root, Red, profile, new Random(867));
        Assert.Equal(2, report.Candidates.Count);
        Assert.All(report.Candidates, candidate => Assert.Equal(0.5, candidate.Probability, 10));
        Assert.Equal(2, report.Search!.Opening!.Value.RestartPairs);
        Assert.True(report.Search.Opening.Value.FullRoot);
        Assert.Equal(-0.04, report.Search.Matrix!.Value.Security, 10);
        var red = root.EligiblePositions[0];
        Assert.Equal(MatchPhase.OpeningRestartRequired, root.PreviewDraft(red, red).Phase);
        Assert.Null(root.PreviewDraft(red, red).FinalResult);
        Assert.Equal(report.Decision, engine.Decide(root, Red, profile, new Random(867)));
    }

    [Fact]
    public void InterruptedDeeperTierKeepsTheCompletedMatrix()
    {
        var root = Open(CastrumTactics.Development.HistoricalBattlefields.Get("four-posts").Definition);
        var engine = new CpuDecisionEngine();
        var one = engine.DecideWithDiagnostics(root, Red, Profile(1, 16, 16), new Random(42));
        var interrupted = engine.DecideWithDiagnostics(root, Red, Profile(3, 16, 16), new Random(42));
        Assert.Equal(one.Decision, interrupted.Decision);
        Assert.Equal(one.Candidates, interrupted.Candidates);
        Assert.Equal(16, interrupted.Search!.ResolvedPairs);
        Assert.Equal(1, interrupted.Search.CompletedDepth);
        Assert.True(interrupted.Search.BudgetExhausted);

        var full = engine.DecideWithDiagnostics(root, Red, Profile(3, 16, 800), new Random(42));
        Assert.Equal(3, full.Search!.Depth);
        Assert.True(full.Search.ReusedPairs > 0);
        Assert.InRange(full.Search.ResolvedPairs, 17, 800);
        Assert.Contains(full.Decision!.Value.Position, root.EligiblePositions);
    }

    [Fact]
    public void ShortlistChecksEveryIncludedCollisionAndReportsIncompleteCoverage()
    {
        var root = Open(CastrumTactics.Development.HistoricalBattlefields.Get("four-posts").Definition);
        var report = new CpuDecisionEngine().DecideWithDiagnostics(root, Blue, Profile(1, 2, 8), new Random(3));
        Assert.Equal(4, report.Search!.ResolvedPairs);
        Assert.False(report.Search.Opening!.Value.FullRoot);
        Assert.Equal(2, report.Search.Replies.Count);
        Assert.All(report.Search.Actions, a => Assert.True(a.CollisionChecked));
        Assert.Equal(2, report.Candidates.Count(c => c.Probability == 0));
    }

    [Fact]
    public void MainPreviewAndAutomaticCollisionShareTheDraftBudget()
    {
        var root = Open(CastrumTactics.Development.HistoricalBattlefields.Get("three-banners").Definition);
        var next = root.PreviewDraft(root.EligiblePositions[0], root.EligiblePositions[1]);
        Assert.Equal(MatchPhase.Main, next.Phase);
        Assert.Equal(2, next.ResolvedRounds);
        Assert.Equal(PieceType.Grave, next.Board.GetCell(root.EligiblePositions[2]).Piece);
        var profile = Profile(1, 16, 256, preview: 1);
        var report = new CpuDecisionEngine().DecideWithDiagnostics(root, Red, profile, new Random(32));
        Assert.Equal(1, report.Search!.Opening!.Value.MainPreviewDepth);
        Assert.True(report.Search.Opening.Value.MainPairs > 0);
        Assert.InRange(report.Search.ResolvedPairs, 10, 256);
    }

    [Fact]
    public void TerrainAndMarginalCoverageRewardAComplementaryCastle()
    {
        var terrain = new TerrainType[3, 9];
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 9; x++)
            terrain[y,x] = x == 4 ? TerrainType.Inaccessible : TerrainType.Playable;
        var template = new BoardState(new MapDefinition(9, 3, terrain, []));
        var evaluator = new OpeningPosition(template);
        var clustered = Layout(new(2,0), false);
        var spread = Layout(new(7,0), false);
        var exchanged = Layout(new(7,0), true);
        var duplicate = evaluator.Evaluate(clustered, null, [], default);
        var complement = evaluator.Evaluate(spread, null, [], default);
        var mirrorColors = evaluator.Evaluate(exchanged, null, [], default);
        var mirrorGeometry = evaluator.Evaluate(Layout(new(7,0), false, reflect: true), null, [], default);
        Assert.True(complement.Coverage > duplicate.Coverage);
        Assert.True(complement.Accessible > duplicate.Accessible);
        Assert.Equal(-complement.Value(1), mirrorColors.Value(1), 10);
        Assert.Equal(-complement.Coverage, mirrorColors.Coverage, 10);
        Assert.Equal(complement.Value(1), mirrorGeometry.Value(1), 10);
        Assert.Equal(complement.Coverage, mirrorGeometry.Coverage, 10);

        BoardState Layout(BoardPosition extra, bool swap, bool reflect = false)
        {
            var friend = swap ? Blue : Red;
            var foe = swap ? Red : Blue;
            return template.WithResolvedCells([
                (Map(new(1,0)), new CellState(TerrainType.Playable, PieceType.Castle, friend, 3)),
                (Map(extra), new CellState(TerrainType.Playable, PieceType.Castle, friend, 3)),
                (Map(new(2,2)), new CellState(TerrainType.Playable, PieceType.Castle, foe, 3))]);

            BoardPosition Map(BoardPosition p) => reflect ? new(8-p.X, p.Y) : p;
        }
    }

    [Fact]
    public void ParallelDecisionsUseOnlyTheirOwnBudgetAndRandomStream()
    {
        var root = Open(CastrumTactics.Development.HistoricalBattlefields.Get("four-posts").Definition);
        var engine = new CpuDecisionEngine();
        var profile = Profile(2, 16, 128);
        var expected = engine.Decide(root, Red, profile, new Random(17));
        Parallel.For(0, 16, _ => Assert.Equal(expected, engine.Decide(root, Red, profile, new Random(17))));
        Assert.Throws<OperationCanceledException>(() =>
            engine.Decide(root, Red, profile, new Random(17), new CancellationToken(true)));
    }

    [Fact]
    public void ChangingDraftRandomConsumptionLeavesTheMainStreamIntact()
    {
        var config = new Tournament.TournamentConfig
        {
            Id = "phase-isolation", Format = "head-to-head",
            Participants = [new("a", "frozen8d3:Easy", MainOpponent: "frozen8d3:Normal"),
                new("b", "frozen8d3:Easy", MainOpponent: "frozen8d3:Normal")],
            Maps = new() { Ids = ["pocket-meadow"] }, SwapSeats = false, StrategicMetrics = false
        };
        var plan = Tournament.PlanTournament(config);
        var match = Assert.Single(plan.Matches);
        var first = plan.Opponents["a"];
        var second = plan.Opponents["b"];
        var a = Play(0);
        var b = Play(20);
        Assert.Equal("completed", a.Status);
        Assert.Equal("completed", b.Status);
        Assert.Equal(a.HistoryDigest, b.HistoryDigest);

        Tournament.TournamentResult Play(int draws)
        {
            plan.Opponents["a"] = WithDraft(first, true);
            plan.Opponents["b"] = WithDraft(second, false);
            return Tournament.PlayTournamentMatch(plan, match, default);

            Tournament.TournamentOpponent WithDraft(Tournament.TournamentOpponent opponent, bool red) =>
                opponent with { Decide = (s, side, random, diagnostics, token) =>
                {
                    if (s.Phase == MatchPhase.Main) return opponent.Decide(s, side, random, diagnostics, token);
                    for (int i = 0; i < draws; i++) random.Next();
                    return new(red ? "A1" : "C3");
                } };
        }
    }

    private static GameSessionSnapshot Open(MapDefinition map) => new LocalGameSession(map, RuleSet.KnightSupport).Current;

    private static CpuProfile Profile(int depth, int width, int pairs, int preview = 0) =>
        new(new CpuEvaluationWeights(2, 2, 2, 2, 2, 2, 2, 2), 16, 1, 0, 0.5)
        { OpeningSearch = new(depth, width, pairs, CpuOpeningPolicy.Equilibrium, mainPreviewDepth: preview) };
}
