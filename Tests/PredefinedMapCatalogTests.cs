using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class PredefinedMapCatalogTests
{
    private const string Sample = """{"id":"sample","name":"Sample","width":4,"height":3,"layout":["S.#.","...S","##.."]}""";

    [Fact]
    public void ParsesRectangularTerrainAndStartsInRowColumnOrder()
    {
        var (map, _) = PredefinedMapCatalog.Parse(Sample, "sample.json");
        Assert.Equal((4, 3), (map.Definition.Width, map.Definition.Height));
        Assert.Equal(new BoardPosition[] { new(0, 0), new(3, 1) }, map.Definition.StartingPositions);
        Assert.Equal(TerrainType.Inaccessible, map.Definition.GetTerrain(new(2, 0)));
        Assert.Equal(TerrainType.Playable, map.Definition.GetTerrain(new(3, 1)));
    }

    [Fact]
    public void ParsesAnOpenSquareMap()
    {
        var (map, _) = PredefinedMapCatalog.Parse(
            """{"id":"open-test","name":"Open Test","width":3,"height":3,"layout":["S..","...","..S"]}""", "open-test.json");
        Assert.Equal((3, 3), (map.Definition.Width, map.Definition.Height));
        Assert.Equal(new BoardPosition[] { new(0, 0), new(2, 2) }, map.Definition.StartingPositions);
        Assert.Equal(0, RockCount(map.Definition));
    }

    [Theory]
    [InlineData(@"""id"":""sample"",", "", "identifier")]
    [InlineData(@"""name"":""Sample"",", "", "name")]
    [InlineData(@"""width"":4,", "", "width and height")]
    [InlineData(@"""height"":3,", "", "width and height")]
    [InlineData(@",""layout"":[""S.#."",""...S"",""##..""]", "", "layout rows")]
    [InlineData("sample", "Bad ID", "identifier")]
    [InlineData("Sample", "  ", "display name")]
    [InlineData(@"""width"":4", @"""width"":13", "width and height")]
    [InlineData(@",""##..""", "", "layout rows")]
    [InlineData("S.#.", "S.#", "row 0")]
    [InlineData("S.#.", "S.K.", "unsupported character")]
    [InlineData("...S", "....", "at least two")]
    [InlineData("...S", "null", "unsupported character")]
    public void ReportsInvalidMaps(string oldValue, string replacement, string reason)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            PredefinedMapCatalog.Parse(Sample.Replace(oldValue, replacement, StringComparison.Ordinal), "broken.json"));
        Assert.Contains("broken.json", error.Message);
        Assert.Contains(reason, error.Message);
    }

    [Fact]
    public void RejectsDuplicateIdentifiersAndLayouts()
    {
        var duplicateId = Assert.Throws<InvalidDataException>(() =>
            new PredefinedMapCatalog([("a.json", Sample), ("b.json", Sample.Replace("S.#.", "S..."))]));
        Assert.Contains("duplicate identifier", duplicateId.Message);
        var duplicateLayout = Assert.Throws<InvalidDataException>(() =>
            new PredefinedMapCatalog([("a.json", Sample), ("b.json", Sample.Replace("sample", "another"))]));
        Assert.Contains("duplicates the layout", duplicateLayout.Message);
    }

    [Fact]
    public void BuiltInMapsCoverEverySizeBandAndPreserveTheDemonstration()
    {
        var catalog = PredefinedMapCatalog.BuiltIn;
        Assert.Equal(50, catalog.Maps.Count);
        Assert.Equal(50, catalog.Maps.Select(map => map.Id).Distinct().Count());
        var sizes = catalog.Maps.Select(map => Math.Max(map.Definition.Width, map.Definition.Height)).ToArray();
        Assert.Equal(10, sizes.Count(size => size <= 4));
        Assert.Equal(15, sizes.Count(size => size is >= 5 and <= 7));
        Assert.Equal(15, sizes.Count(size => size is >= 8 and <= 10));
        Assert.Equal(10, sizes.Count(size => size >= 11));
        Assert.True(catalog.Maps.Count(map => (map.Definition.Width, map.Definition.Height) == (3, 3)) >= 3);
        Assert.True(catalog.Maps.Count(map => (map.Definition.Width, map.Definition.Height) == (12, 12)) >= 3);
        Assert.Contains(catalog.Maps, map => map.Definition.Width >= 2 * map.Definition.Height);
        Assert.Contains(catalog.Maps, map => map.Definition.Height >= 2 * map.Definition.Width);
        Assert.Contains(catalog.Maps, map => RockCount(map.Definition) == 0);
        Assert.Contains(catalog.Maps, map => RockCount(map.Definition) >= 20);
        Assert.Contains(catalog.Maps, map => map.Definition.StartingPositions.Count == 2);
        Assert.Contains(catalog.Maps, map => map.Definition.StartingPositions.Count == 16);
        Assert.Contains(catalog.Maps, map => map.Definition.StartingPositions.Count % 2 == 1);

        MapDefinition demonstration = catalog.Get("demonstration").Definition;
        Assert.Equal((7, 5), (demonstration.Width, demonstration.Height));
        Assert.Equal(new BoardPosition[] { new(4, 0), new(1, 1), new(3, 2), new(5, 3), new(1, 4) },
            demonstration.StartingPositions);
        Assert.Equal(new BoardPosition[] { new(2, 0), new(2, 1), new(5, 1), new(5, 2), new(1, 3), new(4, 4) },
            Positions(demonstration).Where(position => demonstration.GetTerrain(position) == TerrainType.Inaccessible));
    }

    [Fact]
    public void SeededSelectionIsRepeatableAndReturnsCatalogEntries()
    {
        var catalog = PredefinedMapCatalog.BuiltIn;
        var first = new Random(47);
        var second = new Random(47);
        for (int i = 0; i < 100; i++)
        {
            PredefinedMap map = catalog.SelectRandom(first);
            Assert.Same(map, catalog.SelectRandom(second));
            Assert.Same(map, catalog.Get(map.Id));
        }

        Assert.Throws<KeyNotFoundException>(() => catalog.Get("unknown-map"));
    }

    [Fact]
    public void EveryMapAdmitsAnOpeningThatReachesNonterminalMainPlay()
    {
        foreach (PredefinedMap map in PredefinedMapCatalog.BuiltIn.Maps)
        {
            // Preserve one pair of knights and resolve the remaining starts as collisions.
            bool playable = map.Definition.StartingPositions.SelectMany((first, index) =>
                map.Definition.StartingPositions.Skip(index + 1).Select(second => (first, second)))
                .Any(pair => FinishOpening(map.Definition, pair.first, pair.second).Current.Phase == MatchPhase.Main);
            Assert.True(playable, $"No tested opening on '{map.Id}' reached Main.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestartRestoresTheSelectedMapAndInitialCells(bool completeMatch)
    {
        MapDefinition map = PredefinedMapCatalog.BuiltIn.Get(completeMatch ? "pocket-meadow" : "the-crossroads").Definition;
        LocalGameSession session = completeMatch
            ? FinishOpening(map, map.StartingPositions[0], map.StartingPositions[1])
            : FinishOpening(map, map.StartingPositions[0], map.StartingPositions[0]);

        for (int turn = 0; completeMatch && session.Current.Phase == MatchPhase.Main && turn < 40; turn++)
        {
            var snapshot = session.Current;
            var first = snapshot.GetMainPhaseLegalSelections(PlayerId.PlayerOne).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            var second = snapshot.GetMainPhaseLegalSelections(PlayerId.PlayerTwo).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            BoardPosition? collision = first.Intersect(second).Select(position => (BoardPosition?)position).FirstOrDefault();
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(PlayerId.PlayerOne, collision ?? first[0], snapshot.RoundId)));
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(PlayerId.PlayerTwo, collision ?? second[0], snapshot.RoundId)));
        }

        Assert.Equal(completeMatch ? MatchPhase.Finished : MatchPhase.OpeningRestartRequired, session.Current.Phase);
        Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(session.Current.RoundId));
        var restored = session.Current;
        Assert.Equal(MatchPhase.Opening, restored.Phase);
        Assert.Equal((map.Width, map.Height), (restored.Board.Width, restored.Board.Height));
        Assert.Equal(map.StartingPositions, restored.EligiblePositions);
        foreach (BoardPosition position in Positions(map))
        {
            TerrainType terrain = map.GetTerrain(position);
            Assert.Equal(new CellState(terrain, PieceType.None, null, terrain == TerrainType.Playable ? 3 : 0),
                restored.Board.GetCell(position));
        }
    }

    private static LocalGameSession FinishOpening(MapDefinition map, BoardPosition first, BoardPosition second)
    {
        var session = new LocalGameSession(map);
        Resolve(first, second);
        while (session.Current.Phase == MatchPhase.Opening)
        {
            var target = session.Current.EligiblePositions[0];
            Resolve(target, target);
        }

        return session;

        void Resolve(BoardPosition one, BoardPosition two)
        {
            long round = session.Current.RoundId;
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(PlayerId.PlayerOne, one, round)));
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(PlayerId.PlayerTwo, two, round)));
        }
    }

    private static int RockCount(MapDefinition map) =>
        Positions(map).Count(position => map.GetTerrain(position) == TerrainType.Inaccessible);

    private static IEnumerable<BoardPosition> Positions(MapDefinition map) =>
        from y in Enumerable.Range(0, map.Height)
        from x in Enumerable.Range(0, map.Width)
        select new BoardPosition(x, y);
}
