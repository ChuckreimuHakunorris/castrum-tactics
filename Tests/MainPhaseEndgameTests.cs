using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class MainPhaseEndgameTests
{
    [Fact]
    public void OpeningBoardCanFinishImmediatelyAndAnUnreachableRegionScoresZero()
    {
        const TerrainType ground = TerrainType.Playable;
        const TerrainType blocked = TerrainType.Inaccessible;
        TerrainType[,] terrain = { { ground, blocked, ground, ground, blocked, ground, ground } };
        var firstStart = new BoardPosition(0, 0);
        var secondStart = new BoardPosition(2, 0);
        var session = new LocalGameSession(new MapDefinition(7, 1, terrain, [firstStart, secondStart]));
        long round = session.Current.RoundId;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(PlayerId.PlayerOne, firstStart, round)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(PlayerId.PlayerTwo, secondStart, round)));
        Assert.Equal(MatchPhase.Main, session.Current.Phase);

        var state = MainPhaseState.Evaluate(session.Current.Board);

        AssertFinished(state, 1, 2, PlayerId.PlayerTwo);
        Assert.Equal(TerritoryControl.Neutral, state.Territory.GetControl(new BoardPosition(5, 0)));
        Assert.Equal(TerritoryControl.Neutral, state.Territory.GetControl(new BoardPosition(6, 0)));
    }

    [Theory]
    [InlineData(PlayerId.PlayerOne, "21", 0, 1, PlayerId.PlayerTwo)]
    [InlineData(PlayerId.PlayerTwo, "12", 1, 0, PlayerId.PlayerOne)]
    public void LosingEitherPlayersLastKnightEndsTheMatch(PlayerId eliminated, string row, int one, int two, PlayerId winner)
    {
        var target = new BoardPosition(1, 0);
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse(row).AtHealth(target, 1));
        Assert.False(state.IsFinished);
        Assert.Null(state.Result);
        Assert.Contains(target, state.GetLegalSelections(eliminated));

        BoardState next = state.ResolveTurn(new(PlayerId.PlayerOne, target), new(PlayerId.PlayerTwo, target));

        AssertFinished(MainPhaseState.Evaluate(next), one, two, winner);
    }

    [Theory]
    [InlineData("1.A.#g..#2.B", 4, 3, PlayerId.PlayerOne)]
    [InlineData("1.A#g..#2..B", 3, 4, PlayerId.PlayerTwo)]
    [InlineData("1.A#g..#2.B", 3, 3, null)]
    public void OnlySecuredPlayableCellsScoreIncludingEmptyGroundAndOwnedPieces(string row, int one, int two, PlayerId? winner)
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse(row));

        AssertFinished(state, one, two, winner);
        Assert.Throws<InvalidOperationException>(() =>
            state.ResolveTurn(new(PlayerId.PlayerOne, new(0, 0)), new(PlayerId.PlayerTwo, new(1, 0))));
    }

    [Fact]
    public void InaccessibleTerrainScoresZeroEvenWhenSurroundedByOnePlayersSecuredGround()
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("1..###2", ".#.###.", "..A###B"));

        Assert.Equal(TerritoryControl.Inaccessible, state.Territory.GetControl(new BoardPosition(1, 1)));
        AssertFinished(state, 8, 3, PlayerId.PlayerOne);
    }

    [Fact]
    public void BothPlayersCanRunOutOfMovesAfterMutualCaptures()
    {
        var left = new BoardPosition(0, 0);
        var right = new BoardPosition(1, 0);
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("12").AtHealth(left, 1).AtHealth(right, 1));

        BoardState next = state.ResolveTurn(new(PlayerId.PlayerOne, right), new(PlayerId.PlayerTwo, left));

        Assert.All(next.Cells(), cell => Assert.Equal(PieceType.Grave, cell.Piece));
        AssertFinished(MainPhaseState.Evaluate(next), 0, 0, null);
    }

    [Fact]
    public void AContestedComponentBorderedOnlyByCastlesGivesEachPlayerALegalSelection()
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("A.B"));
        var target = new BoardPosition(1, 0);

        Assert.Equal(TerritoryControl.Contested, state.Territory.GetControl(target));
        Assert.False(state.IsFinished);
        Assert.Equal(target, Assert.Single(state.GetLegalSelections(PlayerId.PlayerOne)));
        Assert.Equal(target, Assert.Single(state.GetLegalSelections(PlayerId.PlayerTwo)));
    }

    [Fact]
    public void AllTwoByTwoBoardsWithEveryPieceTypePreserveTheContestedEndgameInvariant()
    {
        const string symbols = ".#g12AB";
        string[] rows = (from left in symbols from right in symbols select $"{left}{right}").ToArray();
        BoardPosition[] positions = [new(0, 0), new(1, 0), new(0, 1), new(1, 1)];
        foreach (string top in rows)
        {
            foreach (string bottom in rows)
            {
                var state = MainPhaseState.Evaluate(ResolvedBoard.Parse(top, bottom));
                bool contested = positions.Any(position => state.Territory.GetControl(position) == TerritoryControl.Contested);
                bool oneCanSelect = state.GetLegalSelections(PlayerId.PlayerOne).Count > 0;
                bool twoCanSelect = state.GetLegalSelections(PlayerId.PlayerTwo).Count > 0;

                Assert.True(oneCanSelect == contested && twoCanSelect == contested && state.IsFinished == !contested,
                    $"Unexpected territory and legal selection relationship on board '{top}/{bottom}'.");
            }
        }
    }

    private static void AssertFinished(MainPhaseState state, int one, int two, PlayerId? winner)
    {
        Assert.True(state.IsFinished);
        MainPhaseResult result = Assert.IsType<MainPhaseResult>(state.Result);
        Assert.Equal((one, two, winner), (result.PlayerOneScore, result.PlayerTwoScore, result.Winner));
        Assert.Empty(state.GetLegalSelections(PlayerId.PlayerOne));
        Assert.Empty(state.GetLegalSelections(PlayerId.PlayerTwo));
        for (int y = 0; y < state.Board.Height; y++)
        {
            for (int x = 0; x < state.Board.Width; x++)
            {
                Assert.NotEqual(TerritoryControl.Contested, state.Territory.GetControl(new BoardPosition(x, y)));
            }
        }
    }
}
