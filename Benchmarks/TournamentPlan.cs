using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CastrumTactics.AI;
using CastrumTactics.Development;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

internal static partial class Program
{
    private const int TournamentSchema = 1;
    private static readonly JsonSerializerOptions TournamentJson = new(Json)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    internal sealed record TournamentConfig
    {
        public required string Id { get; init; }
        public string Format { get; init; } = "round-robin";
        public required ParticipantSpec[] Participants { get; init; }
        public string[] References { get; init; } = [];
        public MapSubset Maps { get; init; } = new();
        public RuleSet Ruleset { get; init; } = RuleSet.KnightSupport;
        public OpeningSpec Opening { get; init; } = new();
        public int FirstSeed { get; init; } = 1;
        public int SeedCount { get; init; } = 1;
        public int[]? Seeds { get; init; }
        public bool SwapSeats { get; init; } = true;
        public bool SwapStarts { get; init; }
        public bool AlternateSubmission { get; init; } = true;
        public int Workers { get; init; } = Math.Clamp(Environment.ProcessorCount, 1, 2);
        public string? OutputDirectory { get; init; }
        public int? MaxMatches { get; init; }
        public int? MaxRounds { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool SeparatePhaseStreams { get; init; }
        public bool Diagnostics { get; init; }
        public bool StrategicMetrics { get; init; } = true;
    }

    internal sealed record ParticipantSpec(string Id, string Opponent, int? PairBudget = null,
        CpuEvaluationWeights? Weights = null, double? TimeLimitMs = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SearchOverrides? Search = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Tablebase = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MainOpponent = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CpuOpeningOptions? OpeningSearch = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? LegacyOpening = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StreamId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? MainTemperature = null);

    internal sealed record SearchOverrides(int? Depth = null, int? OwnCandidates = null, int? OpponentCandidates = null,
        int? ContinuationOwnCandidates = null, int? ContinuationOpponentCandidates = null, int? DeepFinalists = null,
        double? Risk = null, double? ReplyTemperature = null, bool? VerifyTerminalWins = null,
        bool? RequireFullRootReplies = null, bool? ProofOnlyDeepening = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? ResilientEvaluation = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BoundedUtility = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MatrixPolicy? Equilibrium = null);

    internal sealed record MapSubset
    {
        public string[] Ids { get; init; } = [];
        public string[] HeldOut { get; init; } = [];
        public string Split { get; init; } = "all";
        public int? Sample { get; init; }
        public int SampleSeed { get; init; } = 1;
        public int? MinWidth { get; init; }
        public int? MaxWidth { get; init; }
        public int? MinHeight { get; init; }
        public int? MaxHeight { get; init; }
        public int? MinRocks { get; init; }
        public int? MaxRocks { get; init; }
        public int? MinStarts { get; init; }
        public int? MaxStarts { get; init; }
        public bool? Square { get; init; }
    }

    internal sealed record OpeningSpec
    {
        public string Mode { get; init; } = "natural";
        public int Pairs { get; init; } = 1;
        public int Seed { get; init; } = 1;
        public Dictionary<string, string[]> Fixed { get; init; } = new(StringComparer.Ordinal);
    }

    internal sealed record ScheduledMatch(string Id, string Map, string First, string Second, int Seed,
        int FirstStream, int SecondStream, bool SeatsSwapped, bool StartsSwapped, bool BlueSubmitsFirst, string[] Draft);

    internal sealed record MapIdentity(string Id, int Width, int Height, int Rocks, int Starts, bool Historical,
        bool HeldOut, string Digest);

    internal sealed record TournamentPlan(TournamentConfig Config, MapIdentity[] Maps, ScheduledMatch[] Matches,
        Dictionary<string, TournamentOpponent> Opponents);

    internal static TournamentPlan PlanTournament(TournamentConfig config)
    {
        Identifier(config.Id, "Tournament ID");
        if (!Enum.IsDefined(config.Ruleset) || config.Workers < 1 || config.MaxMatches is <= 0 ||
            config.MaxRounds is <= 0 || config.Participants is not { Length: > 0 } || config.Maps is null ||
            config.Opening is null || config.References is null)
            throw new ArgumentException("Invalid tournament ruleset, participants, map settings, or limits.");
        var participants = new SortedDictionary<string, TournamentOpponent>(StringComparer.Ordinal);
        foreach (var spec in config.Participants)
        {
            ArgumentNullException.ThrowIfNull(spec);
            Identifier(spec.Id, "Participant ID");
            if (spec.StreamId is not null) Identifier(spec.StreamId, "Stream ID");
            if (!participants.TryAdd(spec.Id, CreateOpponent(spec)))
                throw new ArgumentException($"Duplicate participant '{spec.Id}'.");
        }

        var pairings = BuildPairings(config, participants.Keys.ToArray()).ToArray();
        if (pairings.Length == 0) throw new ArgumentException("The tournament has no opponent pairings.");
        if (config.Seeds is null && config.SeedCount <= 0)
            throw new ArgumentException("Seed count must be positive.");
        int[] seeds = config.Seeds ?? Enumerable.Range(config.FirstSeed, config.SeedCount).ToArray();
        if (seeds.Length == 0 || seeds.Distinct().Count() != seeds.Length)
            throw new ArgumentException("Expected a nonempty set of distinct seeds.");
        var maps = SelectMaps(config.Maps);
        var mapIds = maps.Select(map => map.Id).ToHashSet(StringComparer.Ordinal);
        var opening = config.Opening;
        if (opening.Mode is not ("natural" or "fixed") || opening.Pairs < 1 || opening.Fixed is null ||
            (opening.Mode == "natural" && (config.SwapStarts || opening.Fixed.Count != 0)) ||
            opening.Fixed.Keys.Any(id => !mapIds.Contains(id)))
            throw new ArgumentException("Invalid opening settings. Explicit drafts and start swaps require fixed mode and selected maps.");

        if (participants.Values.Any(p => p.Spec.Tablebase is not null) && opening.Mode != "fixed")
            throw new ArgumentException("Tablebase participants require fixed successful openings.");

        var drafts = maps.ToDictionary(map => map.Id, map => opening.Mode == "natural" ? [] :
            BuildFixedDraft(map, config.Ruleset, opening), StringComparer.Ordinal);
        var scheduled = (from map in maps
                         from seed in seeds.Order()
                         from pair in pairings
                         from seat in Variants(config.SwapSeats)
                         from start in Variants(config.SwapStarts)
                         select Schedule(map.Id, pair.First, pair.Second, seed, seat, start))
            .Take(config.MaxMatches ?? int.MaxValue).ToArray();
        if (scheduled.Select(match => match.Id).Distinct().Count() != scheduled.Length)
            throw new InvalidOperationException("A match ID collision occurred.");
        return new(config, maps.Select(map => Identify(map, config.Maps.HeldOut)).ToArray(), scheduled,
            new(participants, StringComparer.Ordinal));

        ScheduledMatch Schedule(string map, string first, string second, int seed, bool seat, bool start)
        {
            int firstSeed = StreamSeed(seed, participants[first].Spec.StreamId ?? first, "first");
            int secondSeed = StreamSeed(seed, participants[second].Spec.StreamId ?? second, "second");
            if (firstSeed == secondSeed) secondSeed = (secondSeed + 1) & int.MaxValue;
            string[] draft = drafts[map];
            string id = Digest(FormattableString.Invariant(
                $"{config.Id}|{config.Ruleset}|{opening.Mode}|{map}|{first}|{second}|{seed}|{seat}|{start}|{string.Join(',', draft)}"))[..24];
            return new(id, map, first, second, seed, firstSeed, secondSeed, seat, start,
                config.AlternateSubmission && seat, draft);
        }
    }

    private static IEnumerable<(string First, string Second)> BuildPairings(TournamentConfig config, string[] ids)
    {
        var references = config.References.ToHashSet(StringComparer.Ordinal);
        if (references.Count != config.References.Length || references.Except(ids).Any() ||
            (config.Format != "baseline" && references.Count != 0))
            throw new ArgumentException("Baseline reference IDs must be distinct participants.");
        return config.Format switch
        {
            "round-robin" => ids.SelectMany((first, index) => ids.Skip(index + 1).Select(second => (first, second))),
            "head-to-head" when ids.Length == 2 => [(ids[0], ids[1])],
            "self-play" => ids.Select(id => (id, id)),
            "baseline" => from first in ids where !references.Contains(first)
                          from second in ids where references.Contains(second)
                          select (first, second),
            _ => throw new ArgumentException("Expected round-robin, baseline, self-play, or head-to-head with two participants.")
        };
    }

    private static PredefinedMap[] SelectMaps(MapSubset filter)
    {
        if (filter.Ids is null || filter.HeldOut is null || filter.Sample is <= 0 ||
            filter.Ids.Distinct().Count() != filter.Ids.Length || filter.HeldOut.Distinct().Count() != filter.HeldOut.Length ||
            filter.Split is not ("all" or "tuning" or "held-out"))
            throw new ArgumentException("Invalid map selection or split.");
        foreach (var id in filter.HeldOut) PredefinedMapCatalog.BuiltIn.Get(id);
        foreach (var (min, max) in new[] { (filter.MinWidth, filter.MaxWidth), (filter.MinHeight, filter.MaxHeight),
                     (filter.MinRocks, filter.MaxRocks), (filter.MinStarts, filter.MaxStarts) })
            if (min < 0 || max < 0 || min > max) throw new ArgumentException("Invalid map filter bounds.");
        IEnumerable<PredefinedMap> candidates = filter.Ids.Length == 0
            ? PredefinedMapCatalog.BuiltIn.Maps : filter.Ids.Select(StudyBattlefields.Resolve);
        var maps = candidates.Where(map =>
        {
            var info = Identify(map, filter.HeldOut);
            return InRange(info.Width, filter.MinWidth, filter.MaxWidth) &&
                InRange(info.Height, filter.MinHeight, filter.MaxHeight) &&
                InRange(info.Rocks, filter.MinRocks, filter.MaxRocks) &&
                InRange(info.Starts, filter.MinStarts, filter.MaxStarts) &&
                (filter.Square is null || filter.Square == (info.Width == info.Height)) &&
                (filter.Split == "all" || info.HeldOut == (filter.Split == "held-out"));
        }).ToArray();
        if (maps.Length == 0 || filter.Sample > maps.Length) throw new ArgumentException("The map filters cannot satisfy the requested subset.");
        return (filter.Sample is int count ? maps.OrderBy(map => Digest(FormattableString.Invariant(
            $"{filter.SampleSeed}|{map.Id}")), StringComparer.Ordinal).Take(count) : maps)
            .OrderBy(map => map.Id, StringComparer.Ordinal).ToArray();
    }

    private static MapIdentity Identify(PredefinedMap map, string[] heldOut)
    {
        var definition = map.Definition;
        var terrain = new StringBuilder();
        int rocks = 0;
        for (int y = 0; y < definition.Height; y++)
        for (int x = 0; x < definition.Width; x++)
        {
            bool rock = definition.GetTerrain(new(x, y)) == TerrainType.Inaccessible;
            terrain.Append(rock ? '#' : '.');
            if (rock) rocks++;
        }
        string starts = string.Join('/', definition.StartingPositions.Select(Coordinate));
        return new(map.Id, definition.Width, definition.Height, rocks, definition.StartingPositions.Count,
            HistoricalBattlefields.Maps.Any(item => item.Id == map.Id), heldOut.Contains(map.Id),
            Digest(FormattableString.Invariant($"{map.Id}|{map.Name}|{definition.Width}|{definition.Height}|{terrain}|{starts}")));
    }

    private static string[] BuildFixedDraft(PredefinedMap map, RuleSet rules, OpeningSpec options)
    {
        if (options.Fixed.TryGetValue(map.Id, out var given))
        {
            if (given is null || given.Length == 0 || !Playable(given))
                throw new ArgumentException($"Explicit opening on '{map.Id}' does not reach playable Main.");
            return given.ToArray();
        }
        if (options.Pairs > map.Definition.StartingPositions.Count / 2)
            throw new ArgumentException($"Too many fixed pairs for '{map.Id}'.");
        if (options.Pairs == 1) return [FindStarts(map.Definition, rules)];
        var random = new Random(StreamSeed(options.Seed, map.Id, "draft"));
        for (int attempt = 0; attempt < 128; attempt++)
        {
            var starts = map.Definition.StartingPositions.ToArray();
            random.Shuffle(starts);
            string[] pairs = starts.Take(2 * options.Pairs).Chunk(2)
                .Select(pair => $"{Coordinate(pair[0])}/{Coordinate(pair[1])}").ToArray();
            if (Playable(pairs)) return pairs;
        }
        throw new ArgumentException($"No playable generated opening on '{map.Id}'. Supply an explicit draft.");

        bool Playable(string[] rounds)
        {
            var state = new LocalGameSession(map.Definition, rules).Current;
            foreach (string pair in rounds)
            {
                var (first, second) = ParsePair(pair);
                if (state.Phase != MatchPhase.Opening || !state.EligiblePositions.Contains(first) ||
                    !state.EligiblePositions.Contains(second)) return false;
                state = state.PreviewDraft(first, second);
            }
            while (state.Phase == MatchPhase.Opening)
                state = state.PreviewDraft(state.EligiblePositions[0], state.EligiblePositions[0]);
            return state.Phase == MatchPhase.Main;
        }
    }

    private static (BoardPosition First, BoardPosition Second) ParsePair(string pair)
    {
        string[] parts = pair.Split('/');
        if (parts.Length != 2 || parts.Any(part => !Regex.IsMatch(part, @"\A[A-Z](?:[1-9]|1[0-2])\z")))
            throw new ArgumentException($"Invalid draft pair '{pair}'. Expected A1/C3.");
        return (Position(parts[0]), Position(parts[1]));
    }

    private static bool InRange(int value, int? min, int? max) => value >= (min ?? 0) && value <= (max ?? int.MaxValue);
    private static bool[] Variants(bool swap) => swap ? [false, true] : [false];
    private static int StreamSeed(int seed, string participant, string slot) =>
        BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(
            FormattableString.Invariant($"8d.1|{seed}|{participant}|{slot}")))) & int.MaxValue;
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static void Identifier(string value, string label)
    {
        if (value is null || !Regex.IsMatch(value, @"\A[a-z0-9][a-z0-9-]{0,63}\z"))
            throw new ArgumentException($"{label} must use 1–64 lowercase letters, digits, or hyphens.");
    }
}
