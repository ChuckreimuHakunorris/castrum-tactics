using System.Globalization;
using System.Text.Json;
using CastrumTactics.Development;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

internal static partial class Program
{
    private static int RunTournamentCommand(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "--help")
        {
            Console.WriteLine("Tournament commands: dry-run, run, performance, report, find, replay, inspect. See Documentation/CpuTournaments.md for configuration and options.");
            return 0;
        }
        try
        {
            string command = args[0];
            var options = new Options(args[1..]);
            string? output = options.Take("out");
            if (command is "run" or "performance" or "dry-run")
            {
                string? file = options.Take("config");
                TournamentConfig? config = file is null ? null : ReadJson<TournamentConfig>(file);
                if (config is null && output is not null && File.Exists(ManifestPath(output)))
                    config = ReadJson<Manifest>(ManifestPath(output)).Config;
                config = ApplyOptions(options, config);
                bool dedicated = command == "performance";
                if (dedicated) config = config with { Workers = 1 };
                int? stop = options.Int("stop-after");
                options.Check();
                output ??= config.OutputDirectory ?? Path.Combine("exports", "tournaments", config.Id);
                var plan = PlanTournament(config);
                if (command == "dry-run")
                {
                    if (stop is not null) throw new ArgumentException("Stop-after is only supported for execution.");
                    Write(new { config, output, plan.Maps, MatchCount = plan.Matches.Length,
                        Pairings = plan.Matches.Select(m => new { m.First, m.Second }).Distinct(),
                        FirstMatch = plan.Matches.First() });
                    return 0;
                }
                using var cancellation = new CancellationTokenSource();
                ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
                Console.CancelKeyPress += handler;
                try
                {
                    int[]? beforeGc = dedicated ? Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray() : null;
                    var progress = ExecuteTournament(output, plan, dedicated, stop, cancellation.Token);
                    if (beforeGc is not null)
                        SaveJson(Path.Combine(output, "process-gc.json"), new
                        {
                            collections = beforeGc.Select((count, generation) => GC.CollectionCount(generation) - count).ToArray(),
                            peakWorkingSetBytes = System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64
                        });
                    Write(progress);
                    return progress.State == "cancelled" ? 130 : progress.Failed != 0 ? 2 : 0;
                }
                finally { Console.CancelKeyPress -= handler; }
            }

            if (command is not ("find" or "replay" or "inspect" or "report"))
                throw new ArgumentException($"Unknown tournament command '{command}'.");
            if (output is null || !File.Exists(ManifestPath(output))) throw new ArgumentException("Use --out with an existing tournament directory.");
            using var directoryLock = LockTournament(output);
            var manifest = ReadJson<Manifest>(ManifestPath(output));
            var states = CheckResults(output, manifest);
            if (command == "report")
            {
                options.Check();
                string checkpoint = Path.Combine(output, "progress.json");
                var saved = File.Exists(checkpoint) ? ReadJson<Progress>(checkpoint) : null;
                var actual = new Progress(manifest.Matches.Length, states.Values.Count(s => s == "completed"),
                    states.Values.Count(s => s == "failed"), states.Values.Count(s => s == "limit"),
                    manifest.Matches.Length - states.Count, saved is null || saved.State == "running" ? "interrupted" : saved.State,
                    saved?.Workers ?? manifest.Config.Workers, saved?.LastRunSeconds ?? 0);
                WriteReports(output, manifest, actual, states);
                Write(actual);
                return 0;
            }
            string? id = options.Take("id");
            if (command == "find")
            {
                string? player = options.Take("opponent"), map = options.Take("map"), outcome = options.Take("result"), opening = options.Take("opening");
                int? seed = options.Int("seed");
                options.Check();
                if (outcome is not (null or "win" or "loss" or "red" or "blue" or "draw" or "limit" or "failed" or "retry" or "pending"))
                    throw new ArgumentException("Unknown find result filter.");
                if (outcome is "win" or "loss" && player is null) throw new ArgumentException("Win/loss filters require --opponent.");
                var matchingPlayers = manifest.Config.Participants.Where(spec => spec.Id == player || spec.Opponent == player).Select(spec => spec.Id).ToHashSet();
                foreach (var match in manifest.Matches.Where(match =>
                    (id is null || match.Id == id) && (map is null || match.Map == map) && (seed is null || match.Seed == seed) &&
                    (player is null || matchingPlayers.Contains(match.First) || matchingPlayers.Contains(match.Second))))
                {
                    var result = states.ContainsKey(match.Id) ? LoadResult(output, match.Id) : null;
                    string label = result is null ? "pending" : result.Status != "completed" ? result.Status :
                        result.Opening == "retry" ? "retry" : result.Winner is null ? "draw" : result.Winner == Red ? "red" : "blue";
                    string? winner = result?.Winner is PlayerId side ? (side == Red) ^ match.SeatsSwapped ? match.First : match.Second : null;
                    string? loser = winner is null ? null : winner == match.First ? match.Second : match.First;
                    if (opening is not null && result?.Opening != opening) continue;
                    if (outcome is not null && outcome != label &&
                        !(outcome == "win" && winner is not null && matchingPlayers.Contains(winner)) &&
                        !(outcome == "loss" && loser is not null && matchingPlayers.Contains(loser))) continue;
                    Write(new { match.Id, match.Map, match.First, match.Second, match.Seed, match.SeatsSwapped,
                        match.StartsSwapped, Result = label, Winner = winner, result?.Opening, result?.HistoryDigest, result?.Error });
                }
                return 0;
            }
            var selected = manifest.Matches.SingleOrDefault(match => match.Id == id && states.ContainsKey(match.Id))
                ?? throw new ArgumentException("Provide --id for a stored match.");
            var record = LoadResult(output, selected.Id);
            if (command == "replay")
            {
                options.Check();
                Reconstruct(manifest, selected, record);
                Console.WriteLine(JsonSerializer.Serialize(record, TournamentJson));
                return 0;
            }
            string phase = options.Take("phase") ?? "main";
            int number = options.Int("round") ?? throw new ArgumentException("Inspect requires --round.");
            string? requested = options.Take("opponents");
            int inspectSeed = options.Int("seed") ?? selected.Seed;
            options.Check();
            if (phase is not ("draft" or "main")) throw new ArgumentException("Inspect phase must be draft or main.");
            var snapshot = Reconstruct(manifest, selected, record, phase, number);
            ParticipantSpec[] probes = requested is null ? manifest.Config.Participants.Where(p => p.Id == selected.First || p.Id == selected.Second).ToArray()
                : ParseParticipants(requested);
            foreach (var spec in probes)
            foreach (var side in new[] { Red, Blue })
            {
                var opponent = CreateOpponent(spec);
                int stream = StreamSeed(inspectSeed, spec.Id, side.ToString());
                var decision = Measure(opponent, snapshot, side, new Random(stream), true, CancellationToken.None);
                Console.WriteLine(JsonSerializer.Serialize(new { spec.Id, Side = side, phase, number, stream, decision }, TournamentJson));
            }
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or JsonException or
            InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    internal static GameSessionSnapshot Reconstruct(Manifest manifest, ScheduledMatch match, TournamentResult record,
        string? targetPhase = null, int targetRound = 0)
    {
        var map = HistoricalBattlefields.Resolve(match.Map);
        if (Identify(map, manifest.Config.Maps.HeldOut).Digest != manifest.Maps.Single(m => m.Id == match.Map).Digest)
            throw new InvalidDataException($"Map definition changed for '{match.Map}'.");
        var session = new LocalGameSession(map.Definition, manifest.Config.Ruleset);
        GameSessionSnapshot? target = null;
        int forced = 0, collisions = 0;
        for (int i = 0; i < record.Rounds.Count; i++)
        {
            var round = record.Rounds[i];
            var before = session.Current;
            bool draft = before.Phase == MatchPhase.Opening;
            string phase = draft ? "draft" : "main";
            int number = (draft ? before.ResolvedRounds : before.ResolvedMainTurns) + 1;
            if (!IsPlayable(before.Phase) || round.Phase != phase || round.Number != number)
                throw new InvalidDataException($"Unexpected phase or round at action {i + 1}.");
            if (phase == targetPhase && number == targetRound) target = before;
            Play(session, round.Red.Move, round.Blue.Move, match.BlueSubmitsFirst);
            var after = session.Current;
            CheckScores(round, after);
            if (draft && round.Red.Move == round.Blue.Move) collisions++;
            if (draft && after.ResolvedRounds == before.ResolvedRounds + 2)
            {
                forced++;
                string last = Coordinate(before.EligiblePositions.Single(p => p != Position(round.Red.Move) && p != Position(round.Blue.Move)));
                if (++i == record.Rounds.Count) throw new InvalidDataException("Missing forced draft collision.");
                var auto = record.Rounds[i];
                if (auto.Phase != "auto-draft-collision" || auto.Number != after.ResolvedRounds || auto.Red.Move != last || auto.Blue.Move != last)
                    throw new InvalidDataException("Forced draft collision does not reproduce.");
                CheckScores(auto, after);
            }
        }
        var final = session.Current;
        if (record.DraftRounds != final.ResolvedRounds || record.MainTurns != final.ResolvedMainTurns || record.Opening != OpeningOutcome(final) ||
            record.Collisions != collisions || record.ForcedCollisions != forced || record.RedScore != final.PlayerOneScore || record.BlueScore != final.PlayerTwoScore ||
            (record.Status == "completed" && (IsPlayable(final.Phase) || record.Winner != final.FinalResult?.Winner ||
                record.HistoryDigest != HistoryDigest(match.Map, manifest.Config.Ruleset, record.Rounds))))
            throw new InvalidDataException("Stored result does not reproduce.");
        return targetPhase is null ? final : target ?? throw new ArgumentException($"No recorded {targetPhase} round {targetRound}.");

        static void CheckScores(TournamentRound step, GameSessionSnapshot state)
        {
            if (step.RedScore != state.PlayerOneScore || step.BlueScore != state.PlayerTwoScore)
                throw new InvalidDataException("Round score does not reproduce.");
        }
    }

    private static TournamentConfig ApplyOptions(Options options, TournamentConfig? config)
    {
        string? id = options.Take("id"), participants = options.Take("participants");
        config ??= new() { Id = id ?? throw new ArgumentException("Provide --config or --id."),
            Participants = participants is null ? throw new ArgumentException("Provide --participants.") : ParseParticipants(participants) };
        if (config.Maps is null || config.Opening is null) throw new ArgumentException("Map and opening settings cannot be null.");
        string? rules = options.Take("ruleset"), maps = options.Take("maps"), references = options.Take("references"), seeds = options.Take("seeds");
        int? firstSeed = options.Int("first-seed"), seedCount = options.Int("seed-count");
        if (seeds is not null && (firstSeed is not null || seedCount is not null))
            throw new ArgumentException("Choose explicit seeds or a range.");
        return config with
        {
            Id = id ?? config.Id, Format = options.Take("format") ?? config.Format,
            Participants = participants is null ? config.Participants : ParseParticipants(participants),
            References = references is null ? config.References : Split(references),
            Maps = config.Maps with { Ids = maps is null ? config.Maps.Ids : maps == "all" ? [] : Split(maps),
                Sample = options.Int("sample") ?? config.Maps.Sample, SampleSeed = options.Int("sample-seed") ?? config.Maps.SampleSeed,
                Split = options.Take("split") ?? config.Maps.Split },
            Ruleset = rules is null ? config.Ruleset : Enum.Parse<RuleSet>(rules, ignoreCase: true),
            Opening = config.Opening with { Mode = options.Take("opening") ?? config.Opening.Mode,
                Pairs = options.Int("pairs") ?? config.Opening.Pairs, Seed = options.Int("opening-seed") ?? config.Opening.Seed },
            Seeds = seeds is not null ? Split(seeds).Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray()
                : firstSeed is not null || seedCount is not null ? null : config.Seeds,
            FirstSeed = firstSeed ?? config.FirstSeed, SeedCount = seedCount ?? config.SeedCount,
            SwapSeats = options.Bool("swap-seats") ?? config.SwapSeats, SwapStarts = options.Bool("swap-starts") ?? config.SwapStarts,
            AlternateSubmission = options.Bool("alternate-submission") ?? config.AlternateSubmission,
            Workers = options.Int("workers") ?? config.Workers, MaxMatches = options.Int("max-matches") ?? config.MaxMatches,
            MaxRounds = options.Int("max-rounds") ?? config.MaxRounds, Diagnostics = options.Bool("diagnostics") ?? config.Diagnostics,
            StrategicMetrics = options.Bool("strategic-metrics") ?? config.StrategicMetrics
        };
    }

    private static string[] Split(string list) => list.Split(',', StringSplitOptions.TrimEntries);
    private static ParticipantSpec[] ParseParticipants(string list) => Split(list).Select(entry =>
    {
        var pair = entry.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2) throw new ArgumentException("Participants must use id=opponent.");
        return new ParticipantSpec(pair[0], pair[1]);
    }).ToArray();

    private sealed class Options
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);
        internal Options(string[] args)
        {
            for (int i = 0; i < args.Length; i += 2)
                if (i + 1 == args.Length || !args[i].StartsWith("--", StringComparison.Ordinal) || args[i + 1].StartsWith("--", StringComparison.Ordinal) ||
                    !values.TryAdd(args[i][2..], args[i + 1]))
                    throw new ArgumentException($"Expected a unique --option value at '{args[i]}'.");
        }
        internal string? Take(string name) => values.Remove(name, out var value) ? value : null;
        internal int? Int(string name) => Take(name) is string value ? int.Parse(value, CultureInfo.InvariantCulture) : null;
        internal bool? Bool(string name) => Take(name) is string value ? bool.Parse(value) : null;
        internal void Check() { if (values.Count != 0) throw new ArgumentException($"Unknown options: {string.Join(", ", values.Keys)}."); }
    }
}
