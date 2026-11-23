using CastrumTactics.Domain;
using Godot;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class FactionPieceArtTests
{
    [Theory]
    [InlineData(PlayerId.PlayerOne, "#f06c68")]
    [InlineData(PlayerId.PlayerTwo, "#69a4ff")]
    public void PlayerIdsMapToTheirConfiguredColors(PlayerId player, string hex)
    {
        Assert.Equal(new Color(hex), FactionPalette.GetColor(player));
    }

    [Theory]
    [InlineData(756, 940, 12, 5, 360, 640, 0.94f)]
    [InlineData(940, 756, 5, 12, 1120, 400, 0.94f)]
    [InlineData(1234, 1179, 7, 5, 700, 500, 0.98f)]
    public void SpriteFitsTheTileWithoutChangingShape(int textureWidth, int textureHeight,
        int columns, int rows, int controlWidth, int controlHeight, float cellFootprint)
    {
        var geometry = new BoardGeometry(new Vector2(controlWidth, controlHeight), columns, rows);
        Rect2 tile = geometry.GetCellRect(new BoardPosition(columns - 1, rows - 1));
        var textureSize = new Vector2(textureWidth, textureHeight);
        Vector2 footprint = textureSize * FactionPieceArt.GetScale(textureSize, tile.Size, cellFootprint);

        Assert.Equal(textureWidth / (float)textureHeight, footprint.X / footprint.Y, 5);
        Assert.Equal(tile.Size.X * cellFootprint, Math.Max(footprint.X, footprint.Y), 4);
    }

    [Fact]
    public void ZeroWidthTileCollapsesTheSprite()
    {
        Assert.Equal(0f, FactionPieceArt.GetScale(new Vector2(756, 940), new Vector2(0, 20), 0.94f));
    }
}
