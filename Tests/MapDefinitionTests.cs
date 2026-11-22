using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class MapDefinitionTests
{
    [Fact]
    public void ConstructorRejectsNullTerrain()
    {
        Assert.Throws<ArgumentNullException>("terrain", () => new MapDefinition(1, 1, null!, []));
    }

    [Fact]
    public void ConstructorRejectsNullStartingPositions()
    {
        TerrainType[,] terrain = { { TerrainType.Playable } };

        Assert.Throws<ArgumentNullException>("startingPositions",
            () => new MapDefinition(1, 1, terrain, null!));
    }

    [Fact]
    public void SquareMapAcceptsEmptyStartingPositions()
    {
        TerrainType[,] terrain =
        {
            { TerrainType.Playable, TerrainType.Inaccessible },
            { TerrainType.Playable, TerrainType.Playable }
        };

        var map = new MapDefinition(2, 2, terrain, []);

        Assert.Empty(map.StartingPositions);
        Assert.Equal(TerrainType.Inaccessible, map.GetTerrain(new BoardPosition(1, 0)));
        Assert.Equal(TerrainType.Playable, map.GetTerrain(new BoardPosition(0, 1)));
    }

    [Fact]
    public void RectangularMapUsesColumnsForXAndRowsForY()
    {
        TerrainType[,] terrain =
        {
            { TerrainType.Playable, TerrainType.Playable, TerrainType.Inaccessible },
            { TerrainType.Inaccessible, TerrainType.Playable, TerrainType.Playable }
        };
        BoardPosition[] starts = [new(2, 1), new(0, 0)];

        var map = new MapDefinition(3, 2, terrain, starts);

        Assert.Equal((3, 2), (map.Width, map.Height));
        Assert.Equal(starts, map.StartingPositions);
        Assert.Equal(TerrainType.Inaccessible, map.GetTerrain(new BoardPosition(2, 0)));
        Assert.Equal(TerrainType.Playable, map.GetTerrain(new BoardPosition(2, 1)));
    }

    [Theory]
    [InlineData(0, 1, "width")]
    [InlineData(-1, 1, "width")]
    [InlineData(1, 0, "height")]
    [InlineData(1, -1, "height")]
    public void DimensionsMustBePositive(int width, int height, string parameter)
    {
        Assert.Throws<ArgumentOutOfRangeException>(parameter,
            () => new MapDefinition(width, height, new TerrainType[1, 1], []));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 3)]
    [InlineData(3, 2)]
    public void TerrainDimensionsMustMatchTheDeclaredHeightAndWidth(int rows, int columns)
    {
        Assert.Throws<ArgumentException>("terrain",
            () => new MapDefinition(3, 2, new TerrainType[rows, columns], []));
    }

    [Fact]
    public void UndefinedTerrainValuesAreRejected()
    {
        TerrainType[,] terrain = { { (TerrainType)42 } };

        Assert.Throws<ArgumentException>("terrain", () => new MapDefinition(1, 1, terrain, []));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    [InlineData(0, 1)]
    public void OutOfBoundsPositionsAreRejectedForStartsAndTerrainLookups(int x, int y)
    {
        TerrainType[,] terrain = { { TerrainType.Playable, TerrainType.Playable } };
        var position = new BoardPosition(x, y);
        var map = new MapDefinition(2, 1, terrain, []);

        Assert.False(map.IsWithinBounds(position));
        Assert.Throws<ArgumentOutOfRangeException>("position", () => map.GetTerrain(position));
        Assert.Throws<ArgumentException>("startingPositions", () => new MapDefinition(2, 1, terrain, [position]));
    }

    [Fact]
    public void DuplicateStartingPositionsAreRejected()
    {
        TerrainType[,] terrain = { { TerrainType.Playable } };

        Assert.Throws<ArgumentException>("startingPositions",
            () => new MapDefinition(1, 1, terrain, [new(0, 0), new(0, 0)]));
    }

    [Fact]
    public void InaccessibleStartingPositionsAreRejected()
    {
        TerrainType[,] terrain = { { TerrainType.Inaccessible } };

        Assert.Throws<ArgumentException>("startingPositions",
            () => new MapDefinition(1, 1, terrain, [new(0, 0)]));
    }

    [Fact]
    public void TerrainAndStartingPositionsCannotBeChangedAfterConstruction()
    {
        TerrainType[,] terrain = { { TerrainType.Playable, TerrainType.Inaccessible } };
        var start = new BoardPosition(0, 0);
        var replacement = new BoardPosition(1, 0);
        var starts = new List<BoardPosition> { start };
        var map = new MapDefinition(2, 1, terrain, starts);

        terrain[0, 0] = TerrainType.Inaccessible;
        starts[0] = replacement;

        Assert.Equal(TerrainType.Playable, map.GetTerrain(start));
        Assert.Equal(start, Assert.Single(map.StartingPositions));
        IList<BoardPosition> exposedStarts = Assert.IsAssignableFrom<IList<BoardPosition>>(map.StartingPositions);
        Assert.Throws<NotSupportedException>(() => { exposedStarts[0] = replacement; });
        Assert.Equal(start, Assert.Single(map.StartingPositions));
    }

    [Fact]
    public void TerrainModelDistinguishesOnlyGameplayAccessibility()
    {
        Assert.Equal(
            new HashSet<TerrainType> { TerrainType.Playable, TerrainType.Inaccessible },
            Enum.GetValues<TerrainType>().ToHashSet());
        Assert.Equal(2, Enum.GetNames<TerrainType>().Length);
    }
}
