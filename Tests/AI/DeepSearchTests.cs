using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using CastrumTactics.Domain.Tests;
using Xunit;

namespace CastrumTactics.AI.Tests;

public sealed class DeepSearchTests
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;
    private static readonly CpuDecisionEngine Engine = new();

    [Fact]
    public void ThreeTurnsFindAProvenWinUnavailableAtTwoTurns()
    {
        var board = ResolvedBoard.Parse(".AgAB", "AA1BB", "AAB2g", "gB.Bg");
        string[] health = ["33233", "33133", "33322", "03331"];
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            var cell = new BoardPosition(x, y);
            if (board.GetCell(cell).GroundHealth != health[y][x] - '0')
                board = board.AtHealth(cell, health[y][x] - '0');
        }
        var snapshot = CpuDecisionTests.Snapshot(board, RuleSet.KnightSupport);
        var earlier = Decide(snapshot, Profile(2, 5000));
        var deeper = Decide(snapshot, Profile(3, 5000));
        Assert.Equal(2, earlier.Search!.Depth);
        Assert.Equal(3, deeper.Search!.Depth);
        Assert.Equal(earlier.Search.Replies, deeper.Search.Replies);
        Assert.Equal(new BoardPosition(2, 1), earlier.Decision!.Value.Position);
        Assert.Equal(new BoardPosition(3, 2), deeper.Decision!.Value.Position);
        Assert.True(deeper.Search.Actions[0].ProvenWin);
        var state = snapshot.MainState!;
        Assert.False(WinWithin(state, 2));
        Assert.False(WinWith(state, earlier.Decision.Value.Position, 3));
        Assert.True(WinWith(state, deeper.Decision.Value.Position, 3));

        var production = Decide(snapshot, CpuDifficultyProfiles.Get(CpuDifficulty.Monster));
        Assert.Equal(deeper.Decision, production.Decision);
        Assert.Equal(3, production.Search!.Depth);
        Assert.Equal(3, production.Search.CompletedDepth);
        Assert.True(production.Search.Actions[0].ProvenWin);
    }

    [Fact]
    public void IncompleteDeeperPassPreservesTheCompletedComparison()
    {
        var snapshot = OpenMain();
        var three = Decide(snapshot, Profile(3, 9000, 2));
        Assert.Equal(3, three.Search!.Depth);
        var limited = Decide(snapshot, Profile(4, three.Search.ResolvedPairs + 1, 2));
        Assert.True(limited.Search!.BudgetExhausted);
        Assert.Equal(3, limited.Search.Depth);
        Assert.Equal(3, limited.Search.CompletedDepth);
        Assert.Equal(three.Search.ResolvedPairs + 1, limited.Search.ResolvedPairs);
        Assert.Equal(three.Decision, limited.Decision);
        Assert.Equal(three.Candidates, limited.Candidates);
        Assert.All(limited.Search.Actions.Where(a => a.Depth >= 3), action =>
            Assert.Equal(3, action.Depth));
    }

    [Fact]
    public async Task DepthFourReusesOnlyDecisionLocalPairsAndIsStableInParallel()
    {
        var snapshot = OpenMain();
        var profile = Profile(4, 9000, 2);
        var principal = Decide(snapshot, profile);
        Assert.Equal(4, principal.Search!.Depth);
        Assert.True(principal.Search.ReusedPairs > 0);
        Assert.InRange(principal.Search.ResolvedPairs, 1, 9000);
        var tasks = Enumerable.Range(0, 3).Select(_ => Task.Run(() => Decide(snapshot, profile))).ToArray();
        foreach (var result in await Task.WhenAll(tasks))
        {
            Assert.Equal(principal.Decision, result.Decision);
            Assert.Equal(principal.Candidates, result.Candidates);
            Assert.Equal(principal.Search.ResolvedPairs, result.Search!.ResolvedPairs);
            Assert.Equal(principal.Search.ReusedPairs, result.Search.ReusedPairs);
        }

        var classic = CpuDecisionTests.Snapshot(snapshot.Board, RuleSet.Classic);
        var classicResult = Decide(classic, profile);
        Assert.Null(classicResult.Search);
        Assert.Contains(classicResult.Decision!.Value.Position, classic.GetMainPhaseLegalSelections(One));
        Assert.Equal(principal.Decision, Decide(snapshot, profile).Decision);
    }

    [Fact]
    public void CoverageGatePreservesTheTwoTurnTierWhenRepliesAreOmitted()
    {
        var snapshot = OpenMain();
        var shallow = Decide(snapshot, Profile(2, 3000, 2, replies: 2));
        var gated = Decide(snapshot, Profile(3, 3000, 2, replies: 2, fullRoot: true));
        Assert.True(snapshot.GetMainPhaseLegalSelections(Two).Count > 2);
        Assert.Equal(2, gated.Search!.Depth);
        Assert.Equal(shallow.Decision, gated.Decision);
        Assert.Equal(shallow.Search!.ResolvedPairs, gated.Search.ResolvedPairs);
        Assert.Equal(0, gated.Search.ReusedPairs);
    }

    [Fact]
    public async Task DeeperDecisionCanBeCancelledAndTimedFallbackIsLegal()
    {
        var snapshot = OpenMain();
        var profile = Profile(6, 32768, 3);
        using var started = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var work = Task.Run(() =>
        {
            started.Set();
            return Engine.DecideWithDiagnostics(snapshot, One, profile, new Random(42), cancellation.Token);
        });
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await work; });
        var timed = new CpuDecisionEngine(TimeSpan.FromTicks(1))
            .DecideWithDiagnostics(snapshot, One, profile, new Random(42));
        Assert.True(timed.Search!.TimeLimitReached);
        Assert.Equal(0, timed.Search.Depth);
        Assert.Contains(timed.Decision!.Value.Position, snapshot.GetMainPhaseLegalSelections(One));
    }

    private static bool WinWithin(MainPhaseState state, int turns) =>
        state.Result is { } result ? result.Winner == One :
        turns > 0 && state.GetLegalSelections(One).Any(move => WinWith(state, move, turns));

    private static bool WinWith(MainPhaseState state, BoardPosition move, int turns) =>
        state.GetLegalSelections(Two).All(reply =>
            WinWithin(state.Advance(new(One, move), new(Two, reply)), turns - 1));

    private static GameSessionSnapshot OpenMain() => CpuDecisionTests.Snapshot(
        ResolvedBoard.Parse("A1......", "11......", "..1.....", "........", "........", ".....2..", "......22", "......2B"),
        RuleSet.KnightSupport);

    private static CpuDecisionReport Decide(GameSessionSnapshot snapshot, CpuProfile profile) =>
        Engine.DecideWithDiagnostics(snapshot, One, profile, new Random(42));

    private static CpuProfile Profile(int depth, int pairs, int continuation = 6, int replies = 10, bool fullRoot = false) =>
        new(CpuDifficultyProfiles.Get(CpuDifficulty.Monster).Weights, 16, 1, 0, 0.5)
        {
            Search = new(depth, 18, replies, pairs, .4, 1, continuation, continuation, 3,
                verifyTerminalWins: false, requireFullRootReplies: fullRoot)
        };
}
