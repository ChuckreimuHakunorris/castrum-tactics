using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class TerritoryAnalyzerTests
{
    [Fact]
    public void TotalsIncludeSecuredEmptySquaresKnightsAndCastlesOnly()
    {
        var board = ResolvedBoard.Parse("A.1#..#12g#B.2");
        var analysis = TerritoryAnalyzer.Analyze(board);
        Assert.Equal((3, 3), (analysis.PlayerOneScore, analysis.PlayerTwoScore));
        Assert.Equal(TerritoryControl.Neutral, analysis.GetControl(new(4, 0)));
        Assert.Equal(TerritoryControl.Contested, analysis.GetControl(new(7, 0)));
        Assert.Equal(TerritoryControl.Contested, analysis.GetControl(new(8, 0)));
        Assert.Equal(TerritoryControl.Neutral, analysis.GetControl(new(9, 0)));
        Assert.Equal(TerritoryControl.Inaccessible, analysis.GetControl(new(10, 0)));
    }
    [Fact]
    public void WholeComponentsAreClassifiedSeparatelyFromPieceOwnership()
    {
        BoardState board = ResolvedBoard.Parse("1.#.2#..", "..#..#..");
        CellState[] before = board.Cells();
        TerritoryAnalysis territory = TerritoryAnalyzer.Analyze(board);

        AssertRow(territory, 0, "11#22#..");
        AssertRow(territory, 1, "11#22#..");
        Assert.Equal(PieceType.None, board.GetCell(new BoardPosition(1, 0)).Piece);
        Assert.Null(board.GetCell(new BoardPosition(1, 0)).Owner);
        Assert.Equal(before, board.Cells());
    }

    [Fact]
    public void ComponentsConnectDiagonallyAcrossBlockedOrthogonalNeighbors()
    {
        BoardState board = ResolvedBoard.Parse("1##", "#2#", "##.");
        TerritoryAnalysis territory = TerritoryAnalyzer.Analyze(board);

        AssertRow(territory, 0, "?##");
        AssertRow(territory, 1, "#?#");
        AssertRow(territory, 2, "##?");
    }

    [Fact]
    public void AdjacentCastleSuppliesItsOwnerToEachSideWithoutJoiningComponents()
    {
        BoardState board = ResolvedBoard.Parse("1.A.2#B");

        AssertRow(TerritoryAnalyzer.Analyze(board), 0, "111??#2");
    }

    [Fact]
    public void AnUpgradeCanSplitATraversableRegionButTheCastleStillSecuresItsAdjacentCells()
    {
        BoardState board = ResolvedBoard.Parse("21...");
        var state = MainPhaseState.Evaluate(board);
        AssertRow(state.Territory, 0, "?????");

        BoardState next = state.ResolveTurn(new(PlayerId.PlayerOne, new(1, 0)), new(PlayerId.PlayerTwo, new(0, 0)));

        AssertRow(TerritoryAnalyzer.Analyze(next), 0, "21111");
        AssertRow(state.Territory, 0, "?????");
    }

    [Fact]
    public void AGraveCanIsolateAnEmptyRegionWithoutChangingUnderlyingTerrain()
    {
        BoardState board = ResolvedBoard.Parse("12...#.");
        var state = MainPhaseState.Evaluate(board);
        AssertRow(state.Territory, 0, "?????#.");

        BoardState next = state.ResolveTurn(new(PlayerId.PlayerOne, new(1, 0)), new(PlayerId.PlayerTwo, new(1, 0)));

        AssertRow(TerritoryAnalyzer.Analyze(next), 0, "1....#.");
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 2), next.GetCell(new BoardPosition(1, 0)));
    }

    [Fact]
    public void AHealthOneKnightStillConnectsItsComponent()
    {
        BoardState board = ResolvedBoard.Parse("1.2..").AtHealth(new BoardPosition(2, 0), 1);

        AssertRow(TerritoryAnalyzer.Analyze(board), 0, "?????");
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 2)]
    public void AnalysisRejectsPositionsOutsideARectangularBoard(int x, int y)
    {
        TerritoryAnalysis territory = TerritoryAnalyzer.Analyze(ResolvedBoard.Parse("....", "...."));

        Assert.Throws<ArgumentOutOfRangeException>("position", () => territory.GetControl(new BoardPosition(x, y)));
    }

    private static void AssertRow(TerritoryAnalysis territory, int row, string expected)
    {
        for (int x = 0; x < expected.Length; x++)
        {
            TerritoryControl control = expected[x] switch
            {
                '1' => TerritoryControl.SecuredByPlayerOne,
                '2' => TerritoryControl.SecuredByPlayerTwo,
                '?' => TerritoryControl.Contested,
                '.' => TerritoryControl.Neutral,
                '#' => TerritoryControl.Inaccessible,
                _ => throw new ArgumentException("Unknown territory symbol.", nameof(expected))
            };
            Assert.Equal(control, territory.GetControl(new BoardPosition(x, row)));
        }
    }
}
