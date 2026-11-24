using CastrumTactics.Domain;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class BoardTerrainArtTests
{
    [Theory]
    [InlineData(0, "ground_health_0.png")]
    [InlineData(1, "ground_health_1.png")]
    [InlineData(2, "ground_health_2.png")]
    [InlineData(3, "ground_health_3.png")]
    [InlineData(4, "ground_health_3.png")]
    public void PlayableTerrainUsesHealthIndependentlyOfTheGrave(int health, string filename)
    {
        var empty = new CellState(TerrainType.Playable, PieceType.None, null, health);
        var grave = new CellState(TerrainType.Playable, PieceType.Grave, null, health);
        string expected = $"res://Assets/Sprites/Terrain/{filename}";

        Assert.Equal(expected, BoardTerrainArt.GetTexturePath(empty));
        Assert.Equal(expected, BoardTerrainArt.GetTexturePath(grave));
    }

    [Fact]
    public void InaccessibleTerrainUsesRockAtZeroHealth()
    {
        var cell = new CellState(TerrainType.Inaccessible, PieceType.None, null, 0);

        Assert.Equal("res://Assets/Sprites/Terrain/rock.png", BoardTerrainArt.GetTexturePath(cell));
    }
}
