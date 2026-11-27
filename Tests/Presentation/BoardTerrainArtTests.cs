using CastrumTactics.Domain;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class BoardTerrainArtTests
{
    [Theory]
    [InlineData(0, "grass-plain-3.png")]
    [InlineData(1, "grass-plain-2.png")]
    [InlineData(2, "grass-plain-1.png")]
    [InlineData(3, "grass-plain.png")]
    [InlineData(4, "grass-plain.png")]
    public void PlayableTerrainUsesHealthIndependentlyOfTheGrave(int health, string filename)
    {
        var empty = new CellState(TerrainType.Playable, PieceType.None, null, health);
        var grave = new CellState(TerrainType.Playable, PieceType.Grave, null, health);
        string expected = $"res://Assets/Sprites/Board/{filename}";

        Assert.Equal(expected, BoardTerrainArt.GetTexturePath(empty));
        Assert.Equal(expected, BoardTerrainArt.GetTexturePath(grave));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    [InlineData(6, 0)]
    public void SameDamageCueForEveryPlayableFamilyAndPositiveHealthGraves(int health, int damage)
    {
        foreach (var (type, descriptor) in TerrainCatalog.Types.Where(entry => entry.Value.Playable))
        {
            var cell = new CellState(TerrainType.Playable, PieceType.Grave, null, health);
            Assert.Equal(damage, TerrainCatalog.Damage(cell));
            string suffix = damage == 0 ? "" : $"-{damage}";
            var visual = new TileVisual(TileVariant.Original, false, false, type, descriptor.Variants[0]);
            Assert.EndsWith($"{type}-{descriptor.Variants[0]}{suffix}.png", BoardTerrainArt.GetTexturePath(cell, visual));
        }
    }

    [Fact]
    public void InaccessibleTerrainUsesRockAtZeroHealth()
    {
        var cell = new CellState(TerrainType.Inaccessible, PieceType.None, null, 0);

        Assert.Equal(0, TerrainCatalog.Damage(cell));
        Assert.Equal("res://Assets/Sprites/Board/rock-plain.png", BoardTerrainArt.GetTexturePath(cell));
    }
}
