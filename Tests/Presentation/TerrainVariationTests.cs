using CastrumTactics.Domain;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class TerrainVariationTests
{
    private static TerrainVisualPool Grass => TerrainCatalog.VariationPool("grass", 1)!;
    private static readonly BoardPosition[] Grid = Enumerable.Range(0, 2400).Select(i => new BoardPosition(i % 60, i / 60)).ToArray();

    [Fact]
    public void CoordinateAndChannelHashUsesFixedPlatformIndependentVectors()
    {
        AssertVector("open-meadow", 0, new(0, 0), "grass",
            [0x6210a6e1, 0xeacf7a30, 0xb7464020, 0x35f95e9c, 0xe39ba617, 0xe3364514]);
        AssertVector("twelve-towers", 29, new(6, 7), "grass",
            [0x50403327, 0x922d273d, 0x781bd5d6, 0xc68b1d53, 0xc88cb0b6, 0x40a60fdc]);
        AssertVector("open-meadow", -1, new(1, 3), "grass",
            [0x53df1107, 0x8df5ae2f, 0x1bd6ab77, 0x999aa5a0, 0xcfed8f52, 0xda7ed6eb]);
        uint seed = TerrainVisualRandom.MapSeed("open-meadow", 0);
        Assert.NotEqual(TerrainVisualRandom.Tile(seed, new(0, 0), TerrainVisualRandom.HashText("grass")),
            TerrainVisualRandom.Tile(seed, new(0, 0), TerrainVisualRandom.HashText("sand")));
    }

    [Fact]
    public void GrassIsStableAcrossTraversalAndChangesWithTheSeed()
    {
        uint seed = TerrainVisualRandom.MapSeed("twelve-towers", 0);
        var first = Grid.ToDictionary(position => position, position => Grass.Select(seed, position));
        foreach (var position in Grid.Reverse()) Assert.Equal(first[position], Grass.Select(seed, position));
        uint changed = TerrainVisualRandom.MapSeed("twelve-towers", 29);
        Assert.Contains(Grid, position => first[position] != Grass.Select(changed, position));
    }

    [Fact]
    public void DefaultsExerciseAllCompatibleBasesAndPlacementRanges()
    {
        var results = Grid.Select(position => Grass.Select(42, position)).ToArray();
        var bare = results.Where(appearance => appearance.Accent is null).ToArray();
        var accented = results.Where(appearance => appearance.Accent is not null).ToArray();
        Assert.Equal(4, bare.Select(item => item.Base.Id).Distinct().Count());
        Assert.InRange(bare.Length / (double)results.Length, 0.72, 0.78);
        foreach (var item in accented)
        {
            Assert.Equal("grass_base_01", item.Base.Id);
            var accent = item.Accent!.Value;
            Assert.InRange(accent.Scale, 0.65f, 1);
            Assert.InRange(accent.CenterX, accent.Width / 2, 1 - accent.Width / 2);
            Assert.InRange(accent.CenterY, accent.Height / 2, 1 - accent.Height / 2);
            Assert.InRange(accent.CenterX, 0.2999f, 0.7001f);
            Assert.InRange(accent.CenterY, 0.2999f, 0.7001f);
        }
        Assert.InRange(accented.Count(item => item.Accent!.Value.Id == "flowers") / (double)results.Length, 0.12, 0.18);
        Assert.Contains(accented, item => item.Accent!.Value.Mirror);
        Assert.Contains(accented, item => !item.Accent!.Value.Mirror);
    }

    [Fact]
    public void WeightsAndMirroringAreConfigurableWithoutChangingOtherChannels()
    {
        TerrainBaseOption[] bases = [new("base", "base.png", AllowsAccent: true)];
        var active = new TerrainAccentOption("active", "active.png", 100, ["base"], 0.9f, 1,
            1, 1, 0.5f, 0.5f, AllowMirror: false);
        var disabled = active with { Id = "disabled", Weight = 0 };
        var pool = new TerrainVisualPool("grass", 0, bases, [disabled, active]);
        var mirror = new TerrainVisualPool("grass", 0, bases, [active with { AllowMirror = true }]);
        foreach (var position in Grid)
        {
            var first = pool.Select(42, position).Accent!.Value;
            var second = mirror.Select(42, position).Accent!.Value;
            Assert.Equal("active", first.Id);
            Assert.False(first.Mirror);
            Assert.Equal(first, second with { Mirror = false });
            Assert.InRange(first.CenterX, 0.45f, 0.55f);
            Assert.InRange(first.CenterY, 0.45f, 0.55f);
        }
        Assert.Throws<ArgumentException>(() => new TerrainVisualPool("grass", 0, bases, [active with { CompatibleBases = [] }]));
    }

    [Fact]
    public void NewPoolsLeaveExistingSelectionsPinnedToVersionOne()
    {
        var versionOne = Grass.Select(42, new(0, 0));
        var pools = new Dictionary<int, TerrainVisualPool> { [1] = Grass };
        pools[2] = new("grass", 100, [new("future", "future.png")], []);
        Assert.Equal("future", pools[2].Select(42, new(0, 0)).Base.Id);
        Assert.Equal(versionOne, pools[TerrainCatalog.DefaultPoolVersion].Select(42, new(0, 0)));
    }

    [Fact]
    public void ExistingMapsAndAuthoredChoicesResolveAndIgnorePieceOrDamageState()
    {
        foreach (var map in PredefinedMapCatalog.BuiltIn.Maps)
        {
            var view = BoardVisuals.Resolve(map, BoardVisuals.ReadEmbedded(map.Id), Assert.Fail);
            for (int y = 0; y < map.Definition.Height; y++)
            {
                for (int x = 0; x < map.Definition.Width; x++)
                {
                    var position = new BoardPosition(x, y);
                    var visual = view.At(position);
                    if (map.Definition.GetTerrain(position) != TerrainType.Playable || visual.Terrain is not (null or "grass"))
                    {
                        Assert.Null(visual.Appearance);
                        continue;
                    }
                    var appearance = visual.Appearance!.Value;
                    Assert.Equal(appearance.Base.Texture, BoardTerrainArt.GetTexturePath(new(TerrainType.Playable, PieceType.None, null, 3), visual));
                    Assert.Equal(appearance.Base.Texture, BoardTerrainArt.GetTexturePath(new(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerOne, 2), visual));
                    Assert.Equal(appearance.Base.Texture, BoardTerrainArt.GetTexturePath(new(TerrainType.Playable, PieceType.Castle, PlayerId.PlayerTwo, 1), visual));
                    Assert.Equal(appearance.Base.Texture, BoardTerrainArt.GetTexturePath(new(TerrainType.Playable, PieceType.Grave, null, 0), visual));
                }
            }
        }

        var demo = PredefinedMapCatalog.BuiltIn.Get("demonstration");
        var authored = BoardVisuals.Resolve(demo, BoardVisuals.ReadEmbedded(demo.Id), Assert.Fail);
        Assert.Equal("flowers", authored.At(new(0, 0)).Appearance?.Accent?.Id);
        Assert.Equal("grass_base_02", authored.At(new(1, 0)).Appearance?.Base.Id);
        Assert.Null(authored.At(new(1, 0)).Appearance?.Accent);
    }

    private static void AssertVector(string id, int salt, BoardPosition position, string terrain, uint[] expected)
    {
        uint tile = TerrainVisualRandom.Tile(TerrainVisualRandom.MapSeed(id, salt), position, TerrainVisualRandom.HashText(terrain));
        Assert.Equal(expected, Enum.GetValues<TerrainRandomChannel>().Select(channel => TerrainVisualRandom.Sample(tile, channel)));
    }
}
