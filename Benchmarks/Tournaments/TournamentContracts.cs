using System.Text.Json;
using System.Text.Json.Serialization;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

internal static partial class Program
{
    internal const int TournamentSchema = 1;
    internal static readonly JsonSerializerOptions TournamentJson = new(Json)
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

    internal sealed record Manifest(int Schema, TournamentConfig Config, MapIdentity[] Maps, ScheduledMatch[] Matches,
        Dictionary<string, string> Opponents, Dictionary<string, string> Binaries, string? Revision,
        string Runtime, string OS, string Architecture, string Processor, int LogicalProcessors,
        bool DedicatedPerformance, string Signature, DateTimeOffset Created);

    internal sealed record Progress(int Scheduled, int Completed, int Failed, int Limited, int Pending,
        string State, int Workers, double LastRunSeconds);

    internal sealed record ResultEnvelope(int Schema, string Checksum, JsonElement Payload);

    internal delegate DecisionSample TournamentDecision(GameSessionSnapshot state, PlayerId player, Random random,
        bool diagnostics, CancellationToken token);

    internal sealed record TournamentOpponent(ParticipantSpec Spec, string Version, TournamentDecision Decide, string? TablebaseMap = null, bool SeparatePhases = false);
    internal sealed record DecisionSample(string Move, double Milliseconds = 0, long AllocatedBytes = 0,
        int? Pairs = null, int? Depth = null, bool? BudgetExhausted = null, bool? TimeLimitReached = null,
        bool? Fallback = null, object? Diagnostics = null, bool Measured = false, int? ReusedPairs = null,
        int? CompletedDepth = null);
    internal sealed record TournamentRound(string Phase, int Number, DecisionSample Red, DecisionSample Blue,
        int RedScore, int BlueScore);
    internal sealed record TournamentResult(string Id, string Status, string Opening, PlayerId? Winner,
        int RedScore, int BlueScore, int MainTurns, int DraftRounds, int Collisions, int ForcedCollisions,
        int RedCastles, int BlueCastles, bool RedAvoidableLoss, bool BlueAvoidableLoss, int RedInterior,
        int BlueInterior, int RedPlacements, int BluePlacements, string? HistoryDigest, List<TournamentRound> Rounds,
        string? Error = null)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? RedInteriorWithBetterAlternative { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? BlueInteriorWithBetterAlternative { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? OpeningBoardDigest { get; init; }
    }
}
