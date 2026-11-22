using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class BoardStateTests
{
    [Fact]
    public void RectangularBoardInitializesEveryCornerAndEdgeFromTheMap()
    {
        TerrainType[,] terrain =
        {
            { TerrainType.Playable, TerrainType.Playable, TerrainType.Inaccessible },
            { TerrainType.Inaccessible, TerrainType.Playable, TerrainType.Playable }
        };
        var map = new MapDefinition(3, 2, terrain, [new(0, 0), new(2, 1)]);
        var board = new BoardState(map);

        Assert.Equal((3, 2), (board.Width, board.Height));
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                var position = new BoardPosition(x, y);
                CellState cell = board.GetCell(position);
                bool playable = terrain[y, x] == TerrainType.Playable;

                Assert.True(board.IsWithinBounds(position));
                Assert.Equal(terrain[y, x], cell.Terrain);
                Assert.Equal(PieceType.None, cell.Piece);
                Assert.Null(cell.Owner);
                Assert.Equal(playable ? 3 : 0, cell.GroundHealth);
                Assert.Equal(playable, board.IsPlayable(position));
            }
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(0, 1)]
    public void OutOfBoundsPositionFailsBothDirectQueries(int x, int y)
    {
        TerrainType[,] terrain = { { TerrainType.Playable, TerrainType.Playable } };
        var board = new BoardState(new MapDefinition(2, 1, terrain, []));
        var position = new BoardPosition(x, y);

        Assert.False(board.IsWithinBounds(position));
        Assert.Throws<ArgumentOutOfRangeException>("position", () => board.GetCell(position));
        Assert.Throws<ArgumentOutOfRangeException>("position", () => board.IsPlayable(position));
    }

    [Fact]
    public void CellLookupReturnsSnapshotsByValueWithoutWritableProperties()
    {
        TerrainType[,] terrain = { { TerrainType.Playable } };
        var board = new BoardState(new MapDefinition(1, 1, terrain, []));

        object snapshot = board.GetCell(new BoardPosition(0, 0));

        Assert.True(snapshot.GetType().IsValueType);
        Assert.All(snapshot.GetType().GetProperties(), property => Assert.False(property.CanWrite));
        Assert.False(typeof(BoardState).GetMethod(nameof(BoardState.GetCell))!.ReturnType.IsByRef);
    }
}
