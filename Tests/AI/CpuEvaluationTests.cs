using CastrumTactics.Domain;
using CastrumTactics.Domain.Tests;
using Xunit;

namespace CastrumTactics.AI.Tests;

public sealed class CpuEvaluationTests
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;

    [Fact]
    public void OpeningValuesFreeDiagonalAccessAndLargerRegionsOverIsolatedStarts()
    {
        var board = new TacticalBoard(ResolvedBoard.Parse(".##.#.", "#.####", "####.g"), true, default);
        CpuFeatures reachable = Opening(board, 0, 0);
        CpuFeatures isolated = Opening(board, 3, 0);
        Assert.Equal(0.125, reachable.Expansion);
        Assert.Equal(0, isolated.Expansion);
        Assert.True(reachable.Territory > isolated.Territory);
    }

    [Fact]
    public void OpeningUsesExistingFriendlyAndEnemyKnightsWhenAssessingStarts()
    {
        var board = new TacticalBoard(ResolvedBoard.Parse(".....#.....", ".1...#...2.", ".....#....."), true, default);
        CpuFeatures nearFriendly = Opening(board, 0, 0);
        CpuFeatures farFriendly = Opening(board, 4, 0);
        CpuFeatures nearEnemy = Opening(board, 10, 0);
        Assert.True(farFriendly.Spread > nearFriendly.Spread);
        Assert.True(nearEnemy.Spread > farFriendly.Spread);
        Assert.True(nearEnemy.Territory > farFriendly.Territory);
    }

    [Fact]
    public void ExpansionReachingNewContestedCellsOutranksRedundantReachWithExpansionWeight()
    {
        var board = ResolvedBoard.Parse(".....", ".1..2", ".....");
        var snapshot = CpuDecisionTests.Snapshot(board);
        var profile = new CpuProfile(default(CpuEvaluationWeights) with { Expansion = 2 }, 1, 1, 0, 1);
        var report = new CpuDecisionEngine().DecideWithDiagnostics(snapshot, One, profile, new Random(1));
        var redundant = report.Candidates.Single(candidate => candidate.Position == new BoardPosition(0, 1));
        var frontier = report.Candidates.Single(candidate => candidate.Position == new BoardPosition(2, 1));
        Assert.True(frontier.Features.Expansion > redundant.Features.Expansion);
        Assert.True(frontier.Score > redundant.Score);
        Assert.Equal(2 * frontier.Features.Expansion, frontier.Score);
        var zero = new CpuDecisionEngine().DecideWithDiagnostics(snapshot, One, new CpuProfile(default, 1, 1, 0, 1), new Random(1));
        Assert.All(zero.Candidates, candidate => Assert.Equal(0, candidate.Score));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CapturesDistinguishRetainingAKnightFromCreatingAGrave(int health)
    {
        var position = new BoardPosition(2, 1);
        var board = ResolvedBoard.Parse(".....", ".12..", ".....");
        CpuFeatures healthy = EvaluateMain(board, position);
        var graveCapture = board.AtHealth(position, health);
        var state = MainPhaseState.Evaluate(graveCapture);
        var enemyChoice = state.GetLegalSelections(Two).First(choice => choice != position);
        Assert.Equal(PieceType.Grave, state.ResolveTurn(new(One, position), new(Two, enemyChoice)).GetCell(position).Piece);
        CpuFeatures lethal = EvaluateMain(graveCapture, position);
        Assert.True(healthy.Capture > lethal.Capture && lethal.Capture > 0);
        Assert.True(healthy.Expansion > 0);
        Assert.Equal(0, lethal.Expansion);
        Assert.Equal(0, lethal.Territory);
        Assert.Equal(healthy.Mobility, lethal.Mobility);
        Assert.True(healthy.Mobility > 0);
        Assert.True(healthy.Risk > lethal.Risk);
    }

    [Fact]
    public void CastleUpgradesAccountForGatesAndPublicCollisionThreatsWithoutAddingReach()
    {
        var target = new BoardPosition(2, 2);
        var gate = ResolvedBoard.Parse("..#..", "..#..", "..12.", "..#..", "..#..");
        var open = ResolvedBoard.Parse(".....", ".....", "..12.", ".....", ".....");
        var safe = ResolvedBoard.Parse(".....", ".....", "..1.2", ".....", ".....");
        CpuFeatures exposed = EvaluateMain(gate, target);
        CpuFeatures openUpgrade = EvaluateMain(open, target);
        CpuFeatures safeUpgrade = EvaluateMain(safe, target);
        Assert.True(exposed.Bottleneck > openUpgrade.Bottleneck);
        Assert.True(exposed.Fortification > safeUpgrade.Fortification);
        Assert.True(exposed.Risk > safeUpgrade.Risk);
        Assert.Equal(0, exposed.Expansion);
        Assert.Equal(0, exposed.Mobility);
        Assert.Equal(0.65, exposed.Risk);
        var state = MainPhaseState.Evaluate(gate);
        Assert.Equal(PieceType.Grave, state.ResolveTurn(new(One, target), new(Two, target)).GetCell(target).Piece);
    }

    [Fact]
    public void SupportCountsCastlesAndKnightsWhileMobilityPreservesEnemyUpgrades()
    {
        var board = ResolvedBoard.Parse(".....", ".122.", "..B..", ".....");
        var tactical = new TacticalBoard(board, opening: false, default);
        Assert.Equal(3, tactical.Support(tactical.Index(new(3, 2)), Two));
        Assert.Equal(1, tactical.Support(tactical.Index(new(2, 0)), One));

        var adjacentKnights = ResolvedBoard.Parse("122");
        var state = MainPhaseState.Evaluate(adjacentKnights);
        var independentUpgrade = new BoardPosition(2, 0);
        Assert.Contains(independentUpgrade, state.GetLegalSelections(Two));
        // The capture removes support for attacking our knight at x=0, while the upgrade at x=2 remains available.
        Assert.Equal(1.0 / 8, EvaluateMain(adjacentKnights, new(1, 0)).Mobility);
    }

    [Fact]
    public void MainEvaluationNeverRanksSecuredInaccessibleOrGraveCells()
    {
        var board = ResolvedBoard.Parse("..#.....", "1.#.12g.", "..#.....");
        var snapshot = CpuDecisionTests.Snapshot(board);
        var result = new CpuDecisionEngine().DecideWithDiagnostics(snapshot, One,
            CpuDifficultyProfiles.Get(CpuDifficulty.Hard), new Random(2));
        Assert.NotEmpty(result.Candidates);
        Assert.All(result.Candidates, candidate =>
        {
            Assert.Equal(TerritoryControl.Contested, snapshot.Territory!.GetControl(candidate.Position));
            Assert.True(board.IsPlayable(candidate.Position));
        });
        Assert.Equal(snapshot.GetMainPhaseLegalSelections(One).Count, result.Candidates.Count);
    }

    private static CpuFeatures Opening(TacticalBoard board, int x, int y) =>
        CpuDecisionEngine.OpeningFeatures(board, board.Index(new BoardPosition(x, y)), One, Two);

    private static CpuFeatures EvaluateMain(BoardState board, BoardPosition position)
    {
        var state = MainPhaseState.Evaluate(board);
        Assert.Contains(position, state.GetLegalSelections(One));
        return CpuDecisionEngine.MainFeatures(new TacticalBoard(board, opening: false, default), position, One, Two,
            state.Territory, state.GetLegalSelections(One), state.GetLegalSelections(Two));
    }
}
