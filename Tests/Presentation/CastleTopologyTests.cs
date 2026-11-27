using CastrumTactics.Domain;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class CastleTopologyTests
{
    [Fact]
    public void CastleArtworkUsesTwoFacadesAndOneSharedCrystalMask()
    {
        (int Base, int Tint)[] expected =
        [
            (1, 10), (1, 10), (1, 10), (4, 15), (5, 15),
            (6, 15), (7, 15), (1, 10), (1, 10), (10, 10),
            (10, 10), (12, 15), (13, 15), (14, 15), (15, 15),
        ];
        for (int mask = 1; mask <= expected.Length; mask++)
        {
            Assert.Equal(expected[mask - 1], BoardRenderer.GetWallArtworkMasks(mask));
        }
    }

    [Theory]
    [InlineData(PlayerId.PlayerOne)]
    [InlineData(PlayerId.PlayerTwo)]
    public void RoofArtworkUpdatesIndependentlyAtEachFacadeTransition(PlayerId owner)
    {
        var center = new BoardPosition(1, 1);
        var southWest = new BoardPosition(0, 2);
        var southEast = new BoardPosition(2, 2);
        var opponent = owner == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
        var castle = new CellState(TerrainType.Playable, PieceType.Castle, owner, 3);
        var cells = new Dictionary<BoardPosition, CellState>
        {
            [center] = castle,
            [new(1, 0)] = castle,
            [new(1, 2)] = castle,
            [new(0, 1)] = castle,
            [new(2, 1)] = castle,
            [southWest] = new(TerrainType.Playable, PieceType.Castle, opponent, 3),
            [southEast] = new(TerrainType.Playable, PieceType.Knight, owner, 3),
        };

        AssertArtwork(5); // Both façades: west and east ramparts.
        cells[southEast] = castle;
        AssertArtwork(7); // Left façade only.
        cells[southWest] = castle;
        AssertArtwork(15); // Both neighbors are roofs.
        cells.Remove(southEast);
        AssertArtwork(13); // Right façade only.

        void AssertArtwork(int expectedBase)
        {
            Assert.Equal(15, CastleTopology.Mask(center, owner, 3, 3, At));
            int artwork = CastleTopology.ArtworkMask(center, owner, 3, 3, At);
            Assert.Equal((expectedBase, 15), BoardRenderer.GetWallArtworkMasks(artwork));
        }

        CellState At(BoardPosition position) => cells.GetValueOrDefault(position);
    }

    [Fact]
    public void FacadeTransitionsLeaveTheNorthRampartIntact()
    {
        var castle = new CellState(TerrainType.Playable, PieceType.Castle, PlayerId.PlayerOne, 3);
        CellState At(BoardPosition position) => position.Y == 0 || position.X == 1 ? castle : default;

        Assert.Equal(14, CastleTopology.Mask(new(1, 0), PlayerId.PlayerOne, 3, 2, At));
        Assert.Equal(4, CastleTopology.ArtworkMask(new(1, 0), PlayerId.PlayerOne, 3, 2, At));
        Assert.Equal(2, CastleTopology.ArtworkMask(new(0, 0), PlayerId.PlayerOne, 3, 2, At));
    }

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
