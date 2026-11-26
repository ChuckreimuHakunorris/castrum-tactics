using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class BoardVisualTests
{
    private static PredefinedMap Map => PredefinedMapCatalog.BuiltIn.Get("open-meadow");

    [Fact]
    public void OptionalVisualsHaveStableVariantsAndLeaveLegacyCellsAlone()
    {
        const string json = """
            {"version":1,"width":6,"height":5,"seed":19,"tiles":[
              {"x":0,"y":0,"surface":"grass","variant":"weathered","transition":"north-rock-edge","prop":"stone-accent"},
              {"x":1,"y":0,"surface":"grass"}
            ]}
            """;
        Assert.Null(BoardVisuals.Load(Map, null, _ => Assert.Fail("Unexpected warning.")));
        var first = Assert.IsType<BoardVisuals>(BoardVisuals.Load(Map, json, _ => Assert.Fail("Unexpected warning.")));
        var second = Assert.IsType<BoardVisuals>(BoardVisuals.Load(Map, json, _ => Assert.Fail("Unexpected warning.")));
        Assert.Equal(new TileVisual(TileVariant.Weathered, true, true), first.At(new(0, 0)));
        Assert.Equal(TileVariant.Weathered, first.At(new(1, 0)).Variant);
        Assert.Equal(first.At(new(1, 0)), second.At(new(1, 0)));
        Assert.Equal(default, first.At(new(2, 0)));
        Assert.Equal("open-meadow", Map.Id);
        Assert.Equal(100, PredefinedMapCatalog.BuiltIn.Maps.Count);
    }

    [Theory]
    [InlineData("{", "map defaults")]
    [InlineData("""{"version":3,"width":6,"height":5,"tiles":[]}""", "version 1")]
    [InlineData("""{"version":1,"width":5,"height":5,"tiles":[]}""", "dimensions")]
    [InlineData("""{"version":1,"width":6,"height":5,"tiles":[{"x":6,"y":0}]}""", "outside")]
    [InlineData("""{"version":1,"width":6,"height":5,"tiles":[{"x":0,"y":0,"surface":"rock"}]}""", "unsupported")]
    [InlineData("""{"version":1,"width":6,"height":5,"tiles":[{"x":0,"y":0,"prop":"castle"}]}""", "Unknown prop")]
    [InlineData("""{"version":1,"width":6,"height":5,"tiles":[{"x":0,"y":0},{"x":0,"y":0}]}""", "Duplicate tile")]
    [InlineData("""{"version":1,"width":6,"height":5,"tiles":[],"width":6}""", "map defaults")]
    public void InvalidVisualsFallBackForTheWholeMap(string json, string reason)
    {
        var messages = new List<string>();
        Assert.Null(BoardVisuals.Load(Map, json, messages.Add));
        Assert.Contains(reason, Assert.Single(messages), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rock", "", "", "incompatible")]
    [InlineData("bridge", "", "", "requires an explicit")]
    [InlineData("snow", "pebbles", "flowers", "incompatible")]
    [InlineData("grass", "unknown", "", "Unknown variant")]
    public void CatalogRejectsBadVersionTwoPairings(string type, string variant, string prop, string reason)
    {
        string variantField = variant.Length == 0 ? "" : $",\"variant\":\"{variant}\"";
        string propField = prop.Length == 0 ? "" : $",\"prop\":\"{prop}\"";
        string json = $$"""
            {"version":2,"width":6,"height":5,"tiles":[{"x":0,"y":0,"terrain":"{{type}}"{{variantField}}{{propField}}}]}
            """;
        var warnings = new List<string>();
        Assert.Null(BoardVisuals.Load(Map, json, warnings.Add));
        Assert.Contains(reason, Assert.Single(warnings));
    }

    [Fact]
    public void EmbeddedDescriptorsMatchTheCatalogAndRepeatTheirVariants()
    {
        const string prefix = "CastrumTactics.VisualMaps.";
        string[] documents = typeof(BoardVisuals).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(documents);
        foreach (string resource in documents)
        {
            string id = resource[prefix.Length..^5];
            var map = PredefinedMapCatalog.BuiltIn.Get(id);
            string? json = BoardVisuals.ReadEmbedded(id);
            var first = Assert.IsType<BoardVisuals>(BoardVisuals.Load(map, json, Assert.Fail));
            var second = Assert.IsType<BoardVisuals>(BoardVisuals.Load(map, json, Assert.Fail));
            for (int y = 0; y < map.Definition.Height; y++)
                for (int x = 0; x < map.Definition.Width; x++)
                    Assert.Equal(first.At(new(x, y)), second.At(new(x, y)));
        }
    }

    [Fact]
    public void ObservationBindsTheSessionAndUsesOnlyPublicLegalSets()
    {
        IGameSession first = new LocalGameSession(Map.Definition);
        var observer = new MatchPresentation(first);
        first.StateChanged += observer.Observe;
        var (draft, opposingDraft) = MatchPresentation.Targets(first.Current, PlayerId.PlayerOne);
        Assert.Same(draft, opposingDraft);
        Assert.Equal(SessionCommandResult.Accepted, first.SubmitOpeningSelection(new(PlayerId.PlayerOne, new(0, 0), 1)));
        Assert.Null(observer.Latest);
        Assert.Equal(SessionCommandResult.Accepted, first.SubmitOpeningSelection(new(PlayerId.PlayerTwo, new(2, 2), 1)));
        var turn = Assert.IsType<ResolvedTurn>(observer.Latest);
        observer.Observe(first.Current);
        Assert.Same(turn, observer.Latest);

        var (active, opposing) = MatchPresentation.Targets(first.Current, PlayerId.PlayerTwo);
        Assert.Same(first.Current.GetMainPhaseLegalSelections(PlayerId.PlayerTwo), active);
        Assert.Same(first.Current.GetMainPhaseLegalSelections(PlayerId.PlayerOne), opposing);
        var replacement = new MatchPresentation(new LocalGameSession(Map.Definition));
        replacement.Observe(first.Current);
        Assert.Null(replacement.Latest);
    }
}
