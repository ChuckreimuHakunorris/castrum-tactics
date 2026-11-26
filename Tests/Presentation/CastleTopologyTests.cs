using CastrumTactics.Domain;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class CastleTopologyTests
{
    [Fact]
    public void AllMasksUseOnlyFriendlyCardinalCastles()
    {
        BoardPosition[] sides = [new(1, 0), new(2, 1), new(1, 2), new(0, 1)];
        var friendly = new CellState(TerrainType.Playable, PieceType.Castle, PlayerId.PlayerOne, 3);
        var enemy = new CellState(TerrainType.Playable, PieceType.Castle, PlayerId.PlayerTwo, 3);
        var knight = new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerOne, 3);
        var grave = new CellState(TerrainType.Playable, PieceType.Grave, null, 2);
        int expected = 0;
        for (; expected < 16; expected++)
        {
            Assert.Equal(expected, CastleTopology.Mask(new(1, 1), PlayerId.PlayerOne, 3, 3, At));
        }

        int edge = CastleTopology.Mask(new(0, 0), PlayerId.PlayerOne, 3, 3, side =>
        {
            Assert.True(side.X >= 0 && side.Y >= 0);
            return friendly;
        });
        Assert.Equal(6, edge);

        CellState At(BoardPosition position)
        {
            int side = Array.IndexOf(sides, position);
            if (side < 0 || (expected & (1 << side)) != 0) return friendly;
            return side switch { 0 => enemy, 1 => knight, 2 => grave, _ => default };
        }
    }
}
