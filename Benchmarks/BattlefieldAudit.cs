using CastrumTactics.Development.Solver;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

internal static partial class Program
{
    private static int RunBattlefieldAudit(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Usage: battlefield-audit <output.json> [research-map-id ...]");
        string path = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var maps = PredefinedMapCatalog.BuiltIn.Maps.Concat(args.Skip(1).Select(StudyBattlefields.Resolve))
            .DistinctBy(map => map.Id).OrderBy(map => map.Id, StringComparer.Ordinal);
        SaveJson(path, maps.Select(AuditBattlefield).ToArray());
        return 0;
    }

    internal sealed record OpeningSample(string First, string Second, int RedCastles, int BlueCastles,
        string[] RemainingPairs, int RedLegal, int BlueLegal, int SharedLegal, int ContactFronts, int RedScore, int BlueScore, MatchPhase Phase,
        int Graves, int RedMarginalReach, int BlueMarginalReach);

    internal sealed record BattlefieldAuditRow(string Id, int Width, int Height, int Playable,
        int Rocks, double RockDensity, string Digest, string[] Starts, int[] Regions, int RegionsWithoutStarts,
        string[] Bottlenecks, int[] Symmetries, int MinStartDistance, int MaxStartDistance, int DisconnectedStartPairs,
        OpeningSample[] Openings);

    internal static BattlefieldAuditRow AuditBattlefield(PredefinedMap map)
    {
        var definition = map.Definition;
        var identity = Identify(map, []);
        var width = definition.Width;
        var height = definition.Height;
        var starts = definition.StartingPositions.ToHashSet();
        var playable = (from y in Enumerable.Range(0, height)
                        from x in Enumerable.Range(0, width)
                        let p = new BoardPosition(x, y)
                        where definition.GetTerrain(p) != TerrainType.Inaccessible
                        select p).ToHashSet();
        var regions = Regions(playable);
        var cuts = playable.Where(p => Regions(playable.Where(q => q != p).ToHashSet()).Count > regions.Count)
            .OrderBy(p => p.Y).ThenBy(p => p.X).Select(Coordinate).ToArray();
        var symmetries = Enumerable.Range(0, width == height ? 8 : 4)
            .Where(t => playable.All(p => playable.Contains(PositionIdentity.Transform(p, width, height, t))) &&
                        starts.All(p => starts.Contains(PositionIdentity.Transform(p, width, height, t)))).ToArray();

        var distances = new List<int>();
        int disconnected = 0;
        var ordered = definition.StartingPositions;
        var state = new LocalGameSession(definition, RuleSet.KnightSupport).Current;
        var openings = new List<OpeningSample>();
        for (int i = 0; i < ordered.Count; i++)
        {
            var reach = Distances(ordered[i], playable);
            for (int j = i + 1; j < ordered.Count; j++)
            {
                if (reach.TryGetValue(ordered[j], out int distance)) distances.Add(distance);
                else disconnected++;

                var first = state.PreviewDraft(ordered[i], ordered[j]);
                Record(first, ordered[i], ordered[j], fill: false);
                if (first.EligiblePositions.Count >= 2)
                    Record(first, ordered[i], ordered[j], fill: true);
            }
            Record(state.PreviewDraft(ordered[i], ordered[i]), ordered[i], ordered[i], fill: false);
        }

        return new(map.Id, width, height, playable.Count, identity.Rocks, identity.Rocks / (double)(width * height),
            identity.Digest, ordered.Select(Coordinate).ToArray(), regions.Select(r => r.Count).OrderDescending().ToArray(),
            regions.Count(r => !r.Overlaps(starts)), cuts, symmetries, distances.DefaultIfEmpty().Min(),
            distances.DefaultIfEmpty().Max(), disconnected, openings.ToArray());

        void Record(GameSessionSnapshot current, BoardPosition red, BoardPosition blue, bool fill)
        {
            var pairs = new List<string>();
            while (current.Phase == MatchPhase.Opening)
            {
                var one = current.EligiblePositions[0];
                var two = fill ? current.EligiblePositions[^1] : one;
                pairs.Add($"{Coordinate(one)}/{Coordinate(two)}");
                current = current.PreviewDraft(one, two);
            }
            var (redCastles, blueCastles) = CountCastles(current.Board);
            var redLegal = current.GetMainPhaseLegalSelections(Red);
            var blueLegal = current.GetMainPhaseLegalSelections(Blue);
            var redReach = CastleReach(Red);
            var blueReach = CastleReach(Blue);
            var contact = redLegal.Where(blueLegal.Contains).ToHashSet();
            openings.Add(new(Coordinate(red), Coordinate(blue), redCastles, blueCastles, pairs.ToArray(), redLegal.Count, blueLegal.Count,
                contact.Count, Regions(contact).Count, current.PlayerOneScore, current.PlayerTwoScore, current.Phase,
                playable.Count(p => current.Board.GetCell(p).Piece == PieceType.Grave),
                redReach.Count(p => !blueReach.Contains(p)), blueReach.Count(p => !redReach.Contains(p))));

            HashSet<BoardPosition> CastleReach(PlayerId player)
            {
                var reach = new HashSet<BoardPosition>();
                int range = RuleSetDefinition.Get(current.RuleSet).CastlePlacementRange;
                foreach (var p in playable)
                {
                    if (current.Board.GetCell(p) is not { Piece: PieceType.Castle } cell || cell.Owner != player)
                        continue;
                    foreach (var adjacent in current.Board.NeighborsOf(p, range))
                        if (current.Board.IsPlayable(adjacent)) reach.Add(adjacent);
                }
                return reach;
            }
        }
    }

    private static List<HashSet<BoardPosition>> Regions(HashSet<BoardPosition> area)
    {
        var pending = new HashSet<BoardPosition>(area);
        var regions = new List<HashSet<BoardPosition>>();
        while (pending.Count > 0)
        {
            var region = Distances(pending.First(), pending).Keys.ToHashSet();
            pending.ExceptWith(region);
            regions.Add(region);
        }
        return regions;
    }

    private static Dictionary<BoardPosition, int> Distances(BoardPosition start, HashSet<BoardPosition> area)
    {
        var distances = new Dictionary<BoardPosition, int> { [start] = 0 };
        var pending = new Queue<BoardPosition>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var p))
        {
            foreach (var next in Neighbors(p))
                if (area.Contains(next) && distances.TryAdd(next, distances[p] + 1))
                    pending.Enqueue(next);
        }
        return distances;
    }

    private static IEnumerable<BoardPosition> Neighbors(BoardPosition p)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if (dx != 0 || dy != 0) yield return new(p.X + dx, p.Y + dy);
    }
}
