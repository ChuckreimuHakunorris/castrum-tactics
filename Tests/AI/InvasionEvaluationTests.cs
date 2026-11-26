using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Tests;
using Xunit;

namespace CastrumTactics.AI.Tests;

public sealed class InvasionEvaluationTests
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;

    [Fact]
    public void ResilientEvaluationRespectsReflectionAndPlayerExchange()
    {
        var a = Features(ResolvedBoard.Parse("A11..#.", "...g...", ".....2B"));
        var reflected = Features(ResolvedBoard.Parse(".#..11A", "...g...", "B2....."));
        var exchanged = Features(ResolvedBoard.Parse("B22..#.", "...g...", ".....1A"));
        Assert.Equal(a.Score(One), reflected.Score(One), 12);
        Assert.Equal(a.Score(One), exchanged.Score(Two), 12);
    }

    [Fact]
    public void LongInvasionRouteEarnsMoreCreditThanAnExposedFront()
    {
        var distant = Features(ResolvedBoard.Parse("A1.........", "...........", "..........B"));
        var near = Features(ResolvedBoard.Parse("A1.........", "...........", "....B......"));
        Assert.True(distant.Influence > near.Influence);
        Assert.Equal(distant.Secured, near.Secured);
    }

    [Fact]
    public void LowHealthEnemyKnightBlocksAProjectedFootholdWithoutChangingTerritory()
    {
        var board = ResolvedBoard.Parse(".A...B.", "###1###", ".......", ".......");
        var fragile = board.AtHealth(new(3, 1), 1);
        var normal = Features(board);
        var exhausted = Features(fragile);
        Assert.Equal(normal.Secured, exhausted.Secured);
        Assert.True(exhausted.Influence > normal.Influence);
    }

    [Fact]
    public void FrontierReachOutweighsRedundantInteriorAndRewardsCastleBackingUnderAttack()
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse(
            "A1.....", ".1.....", ".1.....", ".......", "......B"), RuleSet.KnightSupport);
        var reply = new MainPhaseSelection(Two, new(5, 3));
        var frontier = Features(state.Advance(new(One, new(2, 3)), reply).Board);
        var interior = Features(state.Advance(new(One, new(0, 1)), reply).Board);
        Assert.True(frontier.Influence > interior.Influence);
        Assert.True(frontier.Score(One) > interior.Score(One));

        var pair = Features(ResolvedBoard.Parse("A122.B."));
        var backed = Features(ResolvedBoard.Parse("A122B.."));
        Assert.True(backed.Contributions(Two).Formation > pair.Contributions(Two).Formation);
    }

    private static StrategicPosition.PositionFeatures Features(BoardState board)
    {
        var state = MainPhaseState.Evaluate(board, RuleSet.KnightSupport);
        return new StrategicPosition(board, RuleSetDefinition.Get(state.RuleSet), default).Evaluate(board,
            state.Territory, state.GetLegalSelections(One), state.GetLegalSelections(Two), default,
            resilient: true, prepareFrontier: false);
    }
}
