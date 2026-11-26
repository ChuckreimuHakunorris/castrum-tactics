extern alias benchmarks;

using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using CastrumTactics.Domain.Tests;
using benchmarks::CastrumTactics.Development;
using benchmarks::CastrumTactics.Development.Solver;
using Xunit;

namespace CastrumTactics.AI.Tests;

public sealed class EquilibriumCpuTests
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;
    private static readonly CpuDecisionEngine Engine = new();

    [Fact]
    public void RectangularMatrixMatchesExactPoliciesAndHandlesDuplicateReplies()
    {
        Rational[,] exact = { { -(Rational)2 / 3, -1, -1 }, { -1, 0, 0 } };
        var result = CompareWithExact(exact);
        Assert.Equal(0.75, result.Row[0], 10);
        Assert.Equal(0.25, result.Row[1], 10);
        Assert.Equal(0.75, result.Column[0], 10);
        Assert.Equal(-0.75, result.Value, 10);
        var tied = CompareWithExact(new Rational[,] { { 0, 0 }, { 0, 0 }, { 0, 0 } });
        Assert.Equal(0, tied.Pivots);
        Assert.All(tied.Row, probability => Assert.Equal(1.0 / 3, probability, 10));
    }

    [Fact]
    public void MixedSolverMatchesExactValuesOnSeededRectangularAndDegenerateMatrices()
    {
        var random = new Random(3542);
        for (int sample = 0; sample < 75; sample++)
        {
            var exact = new Rational[random.Next(1, 10), random.Next(1, 10)];
            for (int i = 0; i < exact.GetLength(0); i++)
            for (int j = 0; j < exact.GetLength(1); j++)
                exact[i, j] = (Rational)random.Next(-4, 5) / 4;
            CompareWithExact(exact);
        }
    }

    [Fact]
    public void CancellationAndElapsedStopInterruptTheMatrixSolver()
    {
        double[,] matrix = { { 1, -1 }, { -1, 1 } };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => NumericalMatrixGame.Solve(matrix, cancellation.Token));
        int checks = 0;
        Assert.Null(NumericalMatrixGame.Solve(matrix, default, () => ++checks == 3));
    }

    [Fact]
    public void OneTurnMatrixFindsPocketMeadowsUniqueCenterWithCompleteCoverage()
    {
        var snapshot = PocketMeadow().Current;
        var report = Engine.DecideWithDiagnostics(snapshot, One, Profile(1, 6, 1, 64), new(42));
        var center = new BoardPosition(1, 1);
        Assert.Equal(center, report.Decision!.Value.Position);
        Assert.Equal(1, report.Candidates.Single(c => c.Position == center).Probability);
        Assert.True(report.Search!.Matrix!.Value.FullReplies);
        Assert.All(report.Search.Actions, a => Assert.Equal(
            snapshot.GetMainPhaseLegalSelections(Two).Contains(a.Position), a.CollisionChecked));
        Assert.Equal(9, report.Search.ResolvedPairs);
    }

    [Fact]
    public async Task MixedPolicyUsesTheSeededStreamAndSurvivesAnUnaffordableSecondTier()
    {
        var snapshot = PocketMeadow().Current;
        var one = Profile(1, 6, 6, 9);
        var two = Profile(2, 6, 6, 9);
        var first = Engine.DecideWithDiagnostics(snapshot, One, one, new(42));
        var fallback = Engine.DecideWithDiagnostics(snapshot, One, two, new(42));
        Assert.Equal(first.Decision, fallback.Decision);
        Assert.Equal(first.Candidates, fallback.Candidates);
        Assert.Equal(1, fallback.Search!.Depth);
        Assert.Equal(9, fallback.Search.ResolvedPairs);
        Assert.True(fallback.Search.BudgetExhausted);

        var mixed = Profile(2, 6, 6, 850, MatrixPolicy.Continuations);
        var runs = Enumerable.Range(0, 24).Select(seed => Task.Run(() =>
            Engine.DecideWithDiagnostics(snapshot, One, mixed, new(seed)))).ToArray();
        var results = await Task.WhenAll(runs);
        Assert.Equal(2, results.Select(r => r.Decision!.Value.Position).Distinct().Count());
        for (int seed = 0; seed < results.Length; seed++)
        {
            var result = results[seed];
            Assert.Equal(2, result.Candidates.Count(c => c.Probability > 0));
            Assert.Equal(1, result.Candidates.Sum(c => c.Probability), 12);
            Assert.Equal(Engine.Decide(snapshot, One, mixed, new(seed)), result.Decision);
        }
    }

    [Fact]
    public void ShortlistedMatrixIncludesCollisionColumnsAndNeverCallsItsSecurityAProof()
    {
        var snapshot = CpuDecisionTests.Snapshot(ResolvedBoard.Parse(
            "A11.2B...", ".11.2....", ".........", "........."), RuleSet.KnightSupport);
        var report = Engine.DecideWithDiagnostics(snapshot, One, Profile(2, 12, 1, 120), new(7));
        Assert.False(report.Search!.Matrix!.Value.FullReplies);
        Assert.True(report.Search.Replies.Count > 1);
        foreach (var action in report.Search.Actions)
        {
            if (snapshot.GetMainPhaseLegalSelections(Two).Contains(action.Position))
                Assert.Contains(report.Search.Replies, reply => reply.Position == action.Position);
            Assert.False(action.ProvenWin);
        }
    }

    [Fact]
    public void ArchivedMixedFixtureKeepsOutcomeMarginAndIndividualGuaranteesSeparate()
    {
        var match = PocketMeadow();
        foreach (var (a, b) in new (BoardPosition, BoardPosition)[]
        {
            (new(0, 1), new(1, 1)), (new(1, 1), new(0, 1)), (new(1, 0), new(1, 1))
        }) Pair(match, a, b);
        var graph = new SmallBoardGraph(SolveLimits.Default, default);
        var lost = graph.Solve(match.Current);
        Assert.Equal((Rational)(-1), lost.Outcome!.Value);
        Assert.Equal((false, true), graph.CanAvoidLoss(lost));
        Assert.False(graph.CanForceDraw(lost).Blue);
        var report = Engine.DecideWithDiagnostics(match.Current, One, Profile(2, 10, 10, 850), new(42));
        Assert.DoesNotContain(report.Search!.Actions, move => move.ProvenWin);

        Pair(match, new(2, 0), new(0, 2));
        Pair(match, new(2, 0), new(1, 0));
        var mixed = new SmallBoardGraph(SolveLimits.Default, default).Solve(match.Current);
        var state = match.Current.MainState!;
        var legacy = new StrategicPosition(state.Board, RuleSetDefinition.Get(state.RuleSet), default);
        Assert.True(legacy.Evaluate(state.Board, state.Territory, state.GetLegalSelections(One),
            state.GetLegalSelections(Two), default).Score(One) > 0);
        var outcome = mixed.Matrix(SolverUtility.Outcome);
        var margin = mixed.Matrix(SolverUtility.Margin);
        Assert.Equal(-(Rational)3 / 4, mixed.Outcome!.Value);
        Assert.Equal(-(Rational)9 / 11, mixed.Margin!.Value);
        var centerUp = Array.IndexOf(mixed.Red, new BoardPosition(1, 0));
        Assert.Equal(0.75, CompareWithExact(outcome).Row[centerUp], 10);
        for (int i = 0; i < margin.GetLength(0); i++)
        for (int j = 0; j < margin.GetLength(1); j++)
            margin[i, j] /= 9;
        Assert.Equal(6.0 / 11, CompareWithExact(margin).Row[centerUp], 10);
    }

    private static NumericalMatrixGame.Solution CompareWithExact(Rational[,] exact)
    {
        var numerical = new double[exact.GetLength(0), exact.GetLength(1)];
        for (int i = 0; i < exact.GetLength(0); i++)
        for (int j = 0; j < exact.GetLength(1); j++)
            numerical[i, j] = exact[i, j].Approximate();
        var expected = MatrixGame.Solve(exact);
        var result = Assert.IsType<NumericalMatrixGame.Solution>(NumericalMatrixGame.Solve(numerical, default));
        Assert.InRange(result.Upper - result.Lower, -1e-12, NumericalMatrixGame.MaximumGap);
        Assert.InRange(Math.Abs(expected.Value.Approximate() - result.Value), 0, NumericalMatrixGame.MaximumGap);
        Assert.InRange(result.Pivots, 0, 256);
        Assert.Equal(1, result.Row.Sum(), 12);
        Assert.Equal(1, result.Column.Sum(), 12);
        return result;
    }

    private static CpuProfile Profile(int depth, int own, int reply, int budget, MatrixPolicy mode = MatrixPolicy.Root) =>
        new(CpuDifficultyProfiles.Get(CpuDifficulty.Monster).Weights, 16, 1, 0, 0.5)
        {
            Search = new(depth, own, reply, budget, 0.4, 1) { Equilibrium = mode }
        };

    private static LocalGameSession PocketMeadow()
    {
        var session = new LocalGameSession(HistoricalBattlefields.Get("pocket-meadow").Definition, RuleSet.KnightSupport);
        var snapshot = session.Current;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(One, new(0, 0), snapshot.RoundId)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Two, new(2, 2), snapshot.RoundId)));
        return session;
    }

    private static void Pair(LocalGameSession session, BoardPosition one, BoardPosition two)
    {
        long round = session.Current.RoundId;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(One, one, round)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(Two, two, round)));
    }
}
