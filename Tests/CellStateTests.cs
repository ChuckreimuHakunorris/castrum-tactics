using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class CellStateTests
{
    [Theory]
    [InlineData(PieceType.Knight, PlayerId.PlayerOne)]
    [InlineData(PieceType.Knight, PlayerId.PlayerTwo)]
    [InlineData(PieceType.Castle, PlayerId.PlayerOne)]
    [InlineData(PieceType.Castle, PlayerId.PlayerTwo)]
    public void KnightsAndCastlesCanBelongToEitherPlayer(PieceType piece, PlayerId owner)
    {
        var cell = new CellState(TerrainType.Playable, piece, owner, 2);

        Assert.Equal(piece, cell.Piece);
        Assert.Equal(owner, cell.Owner);
        Assert.True(cell.IsPlayable);
    }

    [Theory]
    [InlineData(PieceType.Knight)]
    [InlineData(PieceType.Castle)]
    public void KnightsAndCastlesRequireAnOwner(PieceType piece)
    {
        Assert.Throws<ArgumentException>("owner", () => new CellState(TerrainType.Playable, piece, null, 3));
    }

    [Theory]
    [InlineData(PieceType.None)]
    [InlineData(PieceType.Grave)]
    public void EmptyCellsAndGravesMustBeUnowned(PieceType piece)
    {
        Assert.Throws<ArgumentException>("owner", () => new CellState(TerrainType.Playable, piece, PlayerId.PlayerOne, 3));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(1)]
    [InlineData(0)]
    public void AGraveMakesPlayableTerrainUnusableRegardlessOfItsHealth(int health)
    {
        var cell = new CellState(TerrainType.Playable, PieceType.Grave, null, health);

        Assert.Equal(TerrainType.Playable, cell.Terrain);
        Assert.Equal(health, cell.GroundHealth);
        Assert.Null(cell.Owner);
        Assert.False(cell.IsPlayable);
    }

    [Theory]
    [InlineData(PieceType.Knight, PlayerId.PlayerOne)]
    [InlineData(PieceType.Castle, PlayerId.PlayerTwo)]
    [InlineData(PieceType.Grave, null)]
    public void InaccessibleTerrainCannotContainPieces(PieceType piece, PlayerId? owner)
    {
        Assert.Throws<ArgumentException>("piece", () => new CellState(TerrainType.Inaccessible, piece, owner, 0));
    }

    [Fact]
    public void DefaultCellIsAValidInaccessibleSnapshot()
    {
        var inaccessible = new CellState(TerrainType.Inaccessible, PieceType.None, null, 0);

        Assert.Equal(inaccessible, default(CellState));
        Assert.False(inaccessible.IsPlayable);
    }

    [Theory]
    [InlineData(TerrainType.Playable, -1)]
    [InlineData(TerrainType.Inaccessible, -1)]
    public void GroundHealthCannotBeNegative(TerrainType terrain, int health)
    {
        Assert.Throws<ArgumentOutOfRangeException>("groundHealth", () => new CellState(terrain, PieceType.None, null, health));
    }

    [Fact]
    public void InaccessibleTerrainCannotHavePositiveHealth()
    {
        Assert.Throws<ArgumentException>("groundHealth",
            () => new CellState(TerrainType.Inaccessible, PieceType.None, null, 3));
    }

    [Theory]
    [InlineData((TerrainType)42, PieceType.None, null, "terrain")]
    [InlineData(TerrainType.Playable, (PieceType)42, null, "piece")]
    [InlineData(TerrainType.Playable, PieceType.Knight, (PlayerId)42, "owner")]
    public void UndefinedEnumValuesCannotCreateSnapshots(TerrainType terrain, PieceType piece, PlayerId? owner, string parameter)
    {
        Assert.Throws<ArgumentOutOfRangeException>(parameter, () => new CellState(terrain, piece, owner, 0));
    }
}
