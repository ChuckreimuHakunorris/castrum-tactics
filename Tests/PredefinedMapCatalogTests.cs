using System.Security.Cryptography;
using System.Text;
using CastrumTactics.Development;
using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class PredefinedMapCatalogTests
{
    private const string Sample = """{"id":"sample","name":"Sample","width":4,"height":3,"layout":["S.#.","...S","##.."]}""";
    private const string OriginalIds = """
        banner-plain barrow-ring blockhouse braided-ridges broken-corner corner-watch crag-cross
        crescent-quarry deep-steps demonstration edge-harbor four-posts gate-of-reeds great-divide
        highland-forks island-keep lantern-lane little-crossing long-pasture long-ravine market-march
        narrow-yard open-meadow outer-wall pocket-meadow quarry-mouth ridge-gates ring-field
        ring-of-stone river-bend saddle-pass salt-way scattered-cairns serpent-cut seven-cairns
        sixteen-standards split-orchard stone-center stone-islands the-commons the-crossroads three-banners
        three-fiefs three-terraces toll-gate twelve-towers twin-basins twin-ledges watch-road
        western-cut
        """;
    private const string ReplacementIds = """
        bellflower-reach chalkwater-banks dovetail-green elder-steppe farside-orchard glassfen-terrace
        harebell-common ivory-ford kestrel-overlook limestone-crescent marlstone-downs northwind-heath
        oatgrass-rise pewter-hollow rookery-vale saffron-bluffs thistlewater-plain umber-step
        verdigris-meadow wrenfield yarrow-crown
        """;
    private static readonly HashSet<string> Replacements =
        ReplacementIds.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

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
    public void BuiltInMapsAreUniqueAndExcludeRetiredBattlefields()
    {
        var catalog = PredefinedMapCatalog.BuiltIn;
        Assert.Equal(100, catalog.Maps.Count);
        Assert.Equal(100, catalog.Maps.Select(map => map.Id).Distinct().Count());
        Assert.Equal(100, catalog.Maps.Select(Layout).Distinct().Count());
        Assert.All(catalog.Maps, map => Assert.True(Math.Max(map.Definition.Width, map.Definition.Height) > 4, map.Id));
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("serpent-cut"));
        Assert.All(HistoricalBattlefields.Maps, map => Assert.Throws<KeyNotFoundException>(() => catalog.Get(map.Id)));
        Assert.True(catalog.Maps.Count(map => (map.Definition.Width, map.Definition.Height) == (12, 12)) >= 3);
        Assert.Contains(catalog.Maps, map => map.Definition.Width >= 2 * map.Definition.Height);
        Assert.Contains(catalog.Maps, map => map.Definition.Height > map.Definition.Width);
        Assert.Contains(catalog.Maps, map => RockCount(map.Definition) == 0);
        Assert.Contains(catalog.Maps, map => RockCount(map.Definition) >= 20);
        Assert.Contains(catalog.Maps, map => map.Definition.StartingPositions.Count == 3);
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
    public void RetainedAndHistoricalDefinitionsMatchTheirBaselines()
    {
        var originals = OriginalIds.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(HistoricalBattlefields.Resolve).ToArray();
        // Include names, dimensions and starts as well as terrain in each canonical digest.
        Assert.Equal(50, originals.Length);
        Assert.Equal("B796113653E09CC32EBB40EE4C2BFC7DA27956CC6BF0D47AB7F85A283780D95F", Fingerprint(originals));
        var retained = PredefinedMapCatalog.BuiltIn.Maps.Where(map => !Replacements.Contains(map.Id)).ToArray();
        Assert.Equal(79, retained.Length);
        Assert.Equal("5D8A7FAA7E638FC32153A910B9ABA7E23319F569826A39B0C0F5A5C5C61755A9", Fingerprint(retained));
        Assert.Equal(21, HistoricalBattlefields.Maps.Count);
        Assert.Equal("A9CE9717D61BFE5134582EE58768172BE619FAFCE74136876C96AA72214F4708",
            Fingerprint(HistoricalBattlefields.Maps));
    }

    [Fact]
    public void ExpansionContainsLargeBoardsAndVariedStartingPositions()
    {
        var originalIds = OriginalIds.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var added = PredefinedMapCatalog.BuiltIn.Maps.Where(map => !originalIds.Contains(map.Id) && !Replacements.Contains(map.Id))
            .Select(map => map.Definition).ToArray();
        Assert.Equal(50, added.Length);
        Assert.All(added, map =>
        {
            Assert.InRange(map.Width, 8, 12);
            Assert.InRange(map.Height, 8, 12);
        });
        Assert.Contains(added, map => map.Width == map.Height);
        Assert.Contains(added, map => map.Width > map.Height);
        Assert.Contains(added, map => map.Width < map.Height);
        Assert.Contains(added, map => InteriorCount(map) == map.StartingPositions.Count);
        Assert.Contains(added, map => InteriorCount(map) * 2 > map.StartingPositions.Count &&
            InteriorCount(map) < map.StartingPositions.Count);
        Assert.Contains(added, map => InteriorCount(map) * 2 <= map.StartingPositions.Count);
        Assert.True(added.Select(map => map.StartingPositions.Count).Distinct().Count() > 1);
    }

    [Fact]
    public void SeededSelectionIsRepeatableAndReturnsCatalogEntries()
    {
        var catalog = PredefinedMapCatalog.BuiltIn;
        var first = new Random(47);
        var second = new Random(47);
        var seen = new HashSet<string>();
        for (int i = 0; i < 2000; i++)
        {
            PredefinedMap map = catalog.SelectRandom(first);
            Assert.Same(map, catalog.SelectRandom(second));
            Assert.Same(map, catalog.Get(map.Id));
            seen.Add(map.Id);
        }

        Assert.Equal(catalog.Maps.Count, seen.Count);
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("unknown-map"));
    }

    [Theory]
    [InlineData(RuleSet.Classic)]
    [InlineData(RuleSet.KnightSupport)]
    public void EveryMapAdmitsAnOpeningThatReachesNonterminalMainPlay(RuleSet ruleSet)
    {
        foreach (PredefinedMap map in PredefinedMapCatalog.BuiltIn.Maps)
        {
            // Preserve one owned piece per faction and resolve the remaining starts as collisions.
            bool playable = map.Definition.StartingPositions.SelectMany((first, index) =>
                map.Definition.StartingPositions.Skip(index + 1).Select(second => (first, second)))
                .Any(pair => FinishOpening(map.Definition, pair.first, pair.second, ruleSet).Current.Phase == MatchPhase.Main);
            Assert.True(playable, $"No tested opening on '{map.Id}' reached Main.");
        }
    }

    [Fact]
    public void ExpansionSupportsSpreadAndCenterPriorityKnightSupportDrafts()
    {
        var originalIds = OriginalIds.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        foreach (PredefinedMap map in PredefinedMapCatalog.BuiltIn.Maps.Where(map => !originalIds.Contains(map.Id)))
        {
            MapDefinition definition = map.Definition;
            for (int axis = 0; axis < 3; axis++)
            {
                var session = new LocalGameSession(definition, RuleSet.KnightSupport);
                while (session.Current.Phase == MatchPhase.Opening)
                {
                    var current = session.Current;
                    BoardPosition[] ranked = axis switch
                    {
                        0 => current.EligiblePositions.OrderBy(position => position.X).ToArray(),
                        1 => current.EligiblePositions.OrderBy(position => position.Y).ToArray(),
                        _ => current.EligiblePositions.OrderBy(position => CenterDistance(position, definition)).ToArray()
                    };
                    BoardPosition second = axis == 2 ? ranked[1] : ranked[^1];
                    Assert.Equal(SessionCommandResult.Accepted,
                        session.SubmitOpeningSelection(new(PlayerId.PlayerOne, ranked[0], current.RoundId)));
                    Assert.Equal(SessionCommandResult.Accepted,
                        session.SubmitOpeningSelection(new(PlayerId.PlayerTwo, second, current.RoundId)));
                }

                Assert.True(session.Current.Phase == MatchPhase.Main, $"Draft {axis} was terminal on '{map.Id}'.");
                var cells = Positions(definition).Select(session.Current.Board.GetCell).ToArray();
                foreach (PlayerId player in Enum.GetValues<PlayerId>())
                    Assert.True(cells.Count(cell => cell.Piece == PieceType.Castle && cell.Owner == player) >= 2);
            }
        }

        static int CenterDistance(BoardPosition position, MapDefinition map) =>
            Math.Abs(2 * position.X - map.Width + 1) + Math.Abs(2 * position.Y - map.Height + 1);
    }

    [Fact]
    public void ReplacementsSupportEveryDistinctKnightSupportStartPairAndAlternativeRoutes()
    {
        var catalog = PredefinedMapCatalog.BuiltIn;
        Assert.Equal(HistoricalBattlefields.Maps.Count, Replacements.Count);
        var variants = catalog.Maps.Where(map => !Replacements.Contains(map.Id)).SelectMany(Orientations).ToHashSet();
        foreach (string id in Replacements.Order(StringComparer.Ordinal))
        {
            var map = catalog.Get(id);
            var definition = map.Definition;
            Assert.InRange(definition.Width, 8, 12);
            Assert.InRange(definition.Height, 8, 12);
            Assert.True(InteriorCount(definition) >= 2, id);
            Assert.DoesNotContain(Layout(map), variants);
            variants.UnionWith(Orientations(map));

            var starts = definition.StartingPositions;
            var ground = Positions(definition).Where(position =>
                definition.GetTerrain(position) == TerrainType.Playable && !starts.Contains(position)).ToHashSet();
            Assert.True(IsConnected(ground), $"{id} loses connectivity when all starts are occupied.");
            // Removing one more cell must still leave an alternative route through the non-start terrain.
            foreach (var cell in ground.ToArray())
            {
                ground.Remove(cell);
                Assert.True(IsConnected(ground), $"{id} has a single-cell gate at {cell}.");
                ground.Add(cell);
            }

            for (int first = 0; first < starts.Count; first++)
            for (int second = first + 1; second < starts.Count; second++)
            {
                var snapshot = FinishOpening(definition, starts[first], starts[second], RuleSet.KnightSupport).Current;
                Assert.True(snapshot.Phase == MatchPhase.Main, $"{id} draft {first}/{second} did not reach Main.");
                foreach (PlayerId player in Enum.GetValues<PlayerId>())
                    Assert.InRange(snapshot.GetMainPhaseLegalSelections(player).Count, 3, 8);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestartRestoresTheSelectedMapAndInitialCells(bool completeMatch)
    {
        MapDefinition map = (completeMatch ? HistoricalBattlefields.Get("pocket-meadow") :
            PredefinedMapCatalog.BuiltIn.Get("the-crossroads")).Definition;
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

    private static LocalGameSession FinishOpening(MapDefinition map, BoardPosition first, BoardPosition second,
        RuleSet ruleSet = RuleSet.Classic)
    {
        var session = new LocalGameSession(map, ruleSet);
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

    private static int InteriorCount(MapDefinition map) => map.StartingPositions.Count(position =>
        position.X > 0 && position.Y > 0 && position.X < map.Width - 1 && position.Y < map.Height - 1);

    private static string Layout(PredefinedMap map)
    {
        var definition = map.Definition;
        var starts = definition.StartingPositions.ToHashSet();
        return string.Join('/', Positions(definition).Chunk(definition.Width).Select(row => new string(row.Select(position =>
            starts.Contains(position) ? 'S' : definition.GetTerrain(position) == TerrainType.Inaccessible ? '#' : '.').ToArray())));
    }

    private static string Fingerprint(IEnumerable<PredefinedMap> maps)
    {
        string baseline = string.Concat(maps.Select(map =>
            $"{map.Id}|{map.Name}|{map.Definition.Width}|{map.Definition.Height}|{Layout(map)}\n"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(baseline)));
    }

    private static IEnumerable<string> Orientations(PredefinedMap map)
    {
        string[] rows = Layout(map).Split('/');
        for (int rotation = 0; rotation < 4; rotation++)
        {
            yield return string.Join('/', rows);
            yield return string.Join('/', rows.Select(row => new string(row.Reverse().ToArray())));
            rows = Enumerable.Range(0, rows[0].Length)
                .Select(column => new string(rows.Reverse().Select(row => row[column]).ToArray())).ToArray();
        }
    }

    private static bool IsConnected(HashSet<BoardPosition> ground)
    {
        var pending = new Queue<BoardPosition>();
        var visited = new HashSet<BoardPosition> { ground.First() };
        pending.Enqueue(ground.First());
        while (pending.TryDequeue(out var position))
        {
            for (int y = position.Y - 1; y <= position.Y + 1; y++)
            for (int x = position.X - 1; x <= position.X + 1; x++)
            {
                var neighbor = new BoardPosition(x, y);
                if (ground.Contains(neighbor) && visited.Add(neighbor)) pending.Enqueue(neighbor);
            }
        }
        return visited.Count == ground.Count;
    }

    private static IEnumerable<BoardPosition> Positions(MapDefinition map) =>
        from y in Enumerable.Range(0, map.Height)
        from x in Enumerable.Range(0, map.Width)
        select new BoardPosition(x, y);
}
