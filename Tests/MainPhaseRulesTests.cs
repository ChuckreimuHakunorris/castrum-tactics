using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class MainPhaseRulesTests
{
    [Theory]
    [InlineData("..1..", true)]
    [InlineData("..A..", false)]
    public void KnightsAndCastlesInfluenceAllEightAdjacentCells(string pieceRow, bool canUpgrade)
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse(".....", pieceRow, ".....", "....2"));
        IReadOnlySet<BoardPosition> legal = state.GetLegalSelections(PlayerId.PlayerOne);
        BoardPosition[] neighbors =
        [
            new(1, 0), new(2, 0), new(3, 0), new(1, 1), new(3, 1), new(1, 2), new(2, 2), new(3, 2)
        ];

        Assert.Equal(canUpgrade ? 9 : 8, legal.Count);
        Assert.All(neighbors, neighbor => Assert.Contains(neighbor, legal));
        Assert.Equal(canUpgrade, legal.Contains(new BoardPosition(2, 1)));
        Assert.DoesNotContain(new BoardPosition(4, 2), legal);
    }

    [Fact]
    public void OppositeCornerKnightsHaveDistinctSetsAndCanUpgradeInIsolation()
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("1..", "...", "..2"));

        Assert.True(state.GetLegalSelections(PlayerId.PlayerOne).SetEquals([new(0, 0), new(1, 0), new(0, 1), new(1, 1)]));
        Assert.True(state.GetLegalSelections(PlayerId.PlayerTwo).SetEquals([new(2, 2), new(1, 2), new(2, 1), new(1, 1)]));
    }

    [Fact]
    public void BarriersCastlesSecuredAndNeutralSquaresAreUnavailableToBothPlayers()
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("1B...2#2..#...", ".gA...#...#..."));
        IReadOnlySet<BoardPosition> first = state.GetLegalSelections(PlayerId.PlayerOne);
        IReadOnlySet<BoardPosition> second = state.GetLegalSelections(PlayerId.PlayerTwo);

        Assert.True(first.SetEquals([new(0, 0), new(0, 1), new(2, 0), new(3, 0), new(3, 1)]));
        Assert.Contains(new BoardPosition(0, 0), second); // The blue castle can target the red knight.
        BoardPosition[] unavailable =
        [
            new(1, 0), new(2, 1), new(1, 1), new(6, 0), new(7, 0), new(8, 0), new(11, 0)
        ];
        Assert.All(unavailable, position =>
        {
            Assert.DoesNotContain(position, first);
            Assert.DoesNotContain(position, second);
        });
    }

    [Fact]
    public void OnlyThePlayerInRangeCanCaptureAnEnemyKnight()
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("12..1"));
        IReadOnlySet<BoardPosition> one = state.GetLegalSelections(PlayerId.PlayerOne);
        IReadOnlySet<BoardPosition> two = state.GetLegalSelections(PlayerId.PlayerTwo);

        Assert.Contains(new BoardPosition(1, 0), one);
        Assert.Contains(new BoardPosition(0, 0), two);
        Assert.DoesNotContain(new BoardPosition(4, 0), two);
    }

    [Fact]
    public void PlacementsAndUpgradesPreserveGroundHealth()
    {
        var left = new BoardPosition(1, 0);
        var right = new BoardPosition(2, 0);
        BoardState board = ResolvedBoard.Parse("1..2").AtHealth(left, 2);
        var state = MainPhaseState.Evaluate(board);

        BoardState placed = state.ResolveTurn(new(PlayerId.PlayerOne, left), new(PlayerId.PlayerTwo, right));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerOne, 2), placed.GetCell(left));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerTwo, 3), placed.GetCell(right));

        BoardState upgraded = MainPhaseState.Evaluate(placed)
            .ResolveTurn(new(PlayerId.PlayerOne, left), new(PlayerId.PlayerTwo, right));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Castle, PlayerId.PlayerOne, 2), upgraded.GetCell(left));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Castle, PlayerId.PlayerTwo, 3), upgraded.GetCell(right));
        Assert.Equal(PieceType.Knight, placed.GetCell(left).Piece);
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.None, null, 2), board.GetCell(left));
    }

    [Theory]
    [InlineData(3, 2, PieceType.Knight, PlayerId.PlayerOne)]
    [InlineData(2, 1, PieceType.Knight, PlayerId.PlayerOne)]
    [InlineData(1, 0, PieceType.Grave, null)]
    public void CaptureUsesPreTurnRangeForBothPlayers(int health, int remaining, PieceType piece, PlayerId? owner)
    {
        var defender = new BoardPosition(1, 0);
        var defenderTarget = new BoardPosition(2, 0);
        BoardState board = ResolvedBoard.Parse("12.").AtHealth(defender, health);
        var state = MainPhaseState.Evaluate(board);
        var attack = new MainPhaseSelection(PlayerId.PlayerOne, defender);
        var place = new MainPhaseSelection(PlayerId.PlayerTwo, defenderTarget);

        BoardState next = state.ResolveTurn(attack, place);

        Assert.Equal(new CellState(TerrainType.Playable, piece, owner, remaining), next.GetCell(defender));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerTwo, 3), next.GetCell(defenderTarget));
        Assert.Equal(next.Cells(), state.ResolveTurn(place, attack).Cells());
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerTwo, health), board.GetCell(defender));
        Assert.Same(board, state.Board);
        Assert.Contains(defenderTarget, state.GetLegalSelections(PlayerId.PlayerTwo));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void SameSquareCollisionTakesPrecedenceOverCaptureAndUpgrade(int health, int remaining)
    {
        var target = new BoardPosition(0, 0);
        BoardState board = ResolvedBoard.Parse("12").AtHealth(target, health);
        var state = MainPhaseState.Evaluate(board);
        var upgrade = new MainPhaseSelection(PlayerId.PlayerOne, target);
        var capture = new MainPhaseSelection(PlayerId.PlayerTwo, target);

        BoardState next = state.ResolveTurn(upgrade, capture);

        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, remaining), next.GetCell(target));
        Assert.False(next.IsPlayable(target));
        Assert.Equal(board.GetCell(new BoardPosition(1, 0)), next.GetCell(new BoardPosition(1, 0)));
        Assert.Equal(next.Cells(), state.ResolveTurn(capture, upgrade).Cells());
        Assert.Equal(PieceType.Knight, board.GetCell(target).Piece);
    }

    [Fact]
    public void SameEmptyTargetCreatesAGraveInsteadOfEitherPlacement()
    {
        var target = new BoardPosition(1, 0);
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("1.2"));

        BoardState next = state.ResolveTurn(new(PlayerId.PlayerOne, target), new(PlayerId.PlayerTwo, target));

        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 2), next.GetCell(target));
    }

    [Fact]
    public void SimultaneousCapturesOnDifferentSquaresBothResolve()
    {
        var left = new BoardPosition(0, 0);
        var right = new BoardPosition(1, 0);
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("12").AtHealth(left, 2));
        var first = new MainPhaseSelection(PlayerId.PlayerOne, right);
        var second = new MainPhaseSelection(PlayerId.PlayerTwo, left);

        BoardState next = state.ResolveTurn(first, second);

        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerTwo, 1), next.GetCell(left));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerOne, 2), next.GetCell(right));
        Assert.Equal(next.Cells(), state.ResolveTurn(second, first).Cells());
    }

    [Fact]
    public void InvalidOrDuplicateSelectionsAreRejectedWithoutChangingTheSnapshot()
    {
        BoardState board = ResolvedBoard.Parse("1...2");
        var state = MainPhaseState.Evaluate(board);
        CellState[] before = board.Cells();
        var first = new MainPhaseSelection(PlayerId.PlayerOne, new(1, 0));
        var second = new MainPhaseSelection(PlayerId.PlayerTwo, new(3, 0));

        Assert.Throws<ArgumentException>("first", () => state.ResolveTurn(first with { Position = new(-1, 0) }, second));
        Assert.Throws<ArgumentException>("second", () => state.ResolveTurn(first, second with { Position = first.Position }));
        Assert.Throws<ArgumentException>("second", () => state.ResolveTurn(first, first));
        Assert.Throws<ArgumentException>("first", () => state.ResolveTurn(first with { Player = (PlayerId)0 }, second));
        Assert.Throws<ArgumentOutOfRangeException>("player", () => state.GetLegalSelections((PlayerId)0));
        Assert.Equal(before, state.Board.Cells());
        Assert.Contains(first.Position, state.GetLegalSelections(first.Player));
        Assert.Contains(second.Position, state.GetLegalSelections(second.Player));
    }
}
