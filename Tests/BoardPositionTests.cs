using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class BoardPositionTests
{
    [Fact]
    public void EqualityUsesBothCoordinatesAndEqualValuesHaveEqualHashes()
    {
        var position = new BoardPosition(1, 2);
        var samePosition = new BoardPosition(1, 2);

        Assert.Equal(position, samePosition);
        Assert.Equal(position.GetHashCode(), samePosition.GetHashCode());
        Assert.NotEqual(position, new BoardPosition(0, 2));
        Assert.NotEqual(position, new BoardPosition(1, 0));
        Assert.Contains(samePosition, new HashSet<BoardPosition> { position });
    }

    [Fact]
    public void NegativeCoordinatesAreValidPositionValues()
    {
        var position = new BoardPosition(-1, -2);

        Assert.Equal((-1, -2), (position.X, position.Y));
    }
}
