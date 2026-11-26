extern alias frozen;

using System.Diagnostics;
using System.Text.Json;
using CastrumTactics.AI;
using CastrumTactics.Development;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Frozen = frozen::CastrumTactics.AI;

internal static partial class Program
{
    private const PlayerId Red = PlayerId.PlayerOne;
    private const PlayerId Blue = PlayerId.PlayerTwo;
    private static readonly CpuDecisionEngine Engine = new();
    private static readonly Frozen.CpuDecisionEngine Baseline = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, string> ArchivedStarts = new()
    {
        ["pocket-meadow"] = "A1/C3", ["little-crossing"] = "B1/D4", ["seven-cairns"] = "C5/G5",
        ["twin-basins"] = "C4/H4", ["sixteen-standards"] = "E3/E8", ["banner-plain"] = "A1/L12"
    };

    private static int Main(string[] args)
    {
        try
        {
            switch (args.FirstOrDefault())
            {
                case "match": RunMatches(args[1..]); break;
                case "fixtures": RunFixtures(); break;
                case "replay" when args.Length == 2: Replay(args[1]); break;
                case "tournament": return RunTournamentCommand(args[1..]);
                case "opening-study": return RunOpeningStudy(args[1..]);
                case "battlefield-audit": return RunBattlefieldAudit(args[1..]);
                case "snapshot-performance": return RunSnapshotPerformance(args[1..]);
                case "action-sensitivity": return RunActionSensitivity(args[1..]);
                case "solver-query": return QuerySolver(args[1..]);
                case "solver": return RunSolver(args[1..]);
                default:
                    Console.Error.WriteLine("Use match [--maps id,id --first strategic:Monster --second probe --seed 1 " +
                        "--count 2 --mode fixed|draft --starts A1/C3 --rules knight-support|classic --swap true|false], " +
                        "fixtures, replay <matches.jsonl>, solver, battlefield-audit, action-sensitivity, snapshot-performance, opening-study <config> <output>, or tournament --help.");
                    return 2;
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static void RunMatches(string[] args)
    {
        var options = new Dictionary<string, string>();
        string[] allowed = ["--maps", "--first", "--second", "--seed", "--count", "--mode", "--starts", "--rules", "--swap"];
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length || !allowed.Contains(args[i]) || !options.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException($"Invalid or repeated option {args[i]}.");
        }

        string[] maps = options.GetValueOrDefault("--maps", "pocket-meadow,twin-basins,banner-plain").Split(',');
        var first = Opponent.Parse(options.GetValueOrDefault("--first", "strategic:Monster"));
        var second = Opponent.Parse(options.GetValueOrDefault("--second", "probe"));
        int seed = int.Parse(options.GetValueOrDefault("--seed", "1"));
        int count = int.Parse(options.GetValueOrDefault("--count", "2"));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        bool swap = bool.Parse(options.GetValueOrDefault("--swap", "true"));
        string? starts = options.GetValueOrDefault("--starts");
        bool fixedDraft = options.GetValueOrDefault("--mode", "fixed") switch
        {
            "fixed" => true, "draft" => false, _ => throw new ArgumentException("Expected fixed or draft mode.")
        };
        if (!fixedDraft && starts is not null) throw new ArgumentException("--starts requires fixed mode.");
        if (!fixedDraft && (first.Kind == "probe" || second.Kind == "probe"))
            Console.Error.WriteLine("Probe uses frozen Normal for the draft and archived Probe for Main.");
        RuleSet rules = options.GetValueOrDefault("--rules", "knight-support") switch
        {
            "knight-support" => RuleSet.KnightSupport, "classic" => RuleSet.Classic,
            _ => throw new ArgumentException("Expected knight-support or classic rules.")
        };

        var warmup = new LocalGameSession(HistoricalBattlefields.Get("pocket-meadow").Definition, rules);
        var warmRandom = new Random(42);
        for (int i = 0; i < 16; i++) _ = Decide(warmup.Current, Red, first, warmRandom);
        Play(warmup, "A1", "C3");
        for (int i = 0; i < 32 && warmup.Current.Phase == MatchPhase.Main; i++)
        {
            _ = Decide(warmup.Current, Red, first, warmRandom);
            _ = Decide(warmup.Current, Blue, second, warmRandom);
        }

        foreach (string id in maps)
        {
            MapDefinition map = HistoricalBattlefields.Resolve(id).Definition;
            string? fixedStarts = fixedDraft ? starts ?? ArchivedStarts.GetValueOrDefault(id) ?? FindStarts(map, rules) : null;
            for (int i = 0; i < count; i++)
            {
                int index = checked(seed + i);
                Write(Match(id, map, rules, first, second, Red, index, fixedStarts));
                if (swap) Write(Match(id, map, rules, first, second, Blue, index, fixedStarts));
            }
        }
    }

    private static MatchRecord Match(string id, MapDefinition map, RuleSet rules, Opponent first, Opponent second,
        PlayerId testedSide, int seed, string? fixedStarts)
    {
        int firstSeed = checked(seed * 104729 + 67), secondSeed = checked(seed * 10949 + 1109);
        var firstRandom = new Random(firstSeed);
        var secondRandom = new Random(secondSeed);
        var session = new LocalGameSession(map, rules);
        var rounds = new List<RoundRecord>();
        bool firstIsRed = testedSide == Red;
        bool avoidableImmediateDefeat = false;
        int interiorPlacements = 0;
        int placements = 0;
        var position = new StrategicPosition(session.Current.Board, RuleSetDefinition.Get(rules), default);
        while (session.Current.Phase is MatchPhase.Opening or MatchPhase.Main)
        {
            if (rounds.Count >= 8L * map.Width * map.Height) throw new InvalidOperationException($"Runaway match on {id}.");
            GameSessionSnapshot before = session.Current;
            bool draft = before.Phase == MatchPhase.Opening;
            DecisionRecord a, b;
            if (draft && fixedStarts is not null)
            {
                string[] pair = fixedStarts.Split('/');
                if (pair.Length != 2 || pair[0] == pair[1])
                    throw new ArgumentException("Fixed starts must identify two distinct squares.");
                string forced = Coordinate(before.EligiblePositions[0]);
                a = new(before.ResolvedRounds == 0 ? pair[0] : forced, 0, 0, 0, 0);
                b = new(before.ResolvedRounds == 0 ? pair[1] : forced, 0, 0, 0, 0);
            }
            else
            {
                a = Decide(before, testedSide, first, firstRandom);
                b = Decide(before, Other(testedSide), second, secondRandom);
            }
            var red = firstIsRed ? a : b;
            var blue = firstIsRed ? b : a;
            Play(session, red.Move, blue.Move);
            GameSessionSnapshot after = session.Current;
            if (before.MainState is { } prior && after.MainState is { } next)
            {
                BoardPosition action = Position(a.Move);
                bool placement = prior.Board.GetCell(action).Piece == PieceType.None;
                if (placement)
                {
                    placements++;
                    if (IsInteriorPlacement(position, prior, next, testedSide, action)) interiorPlacements++;
                }
                if (next.Result?.Winner == Other(testedSide))
                {
                    var reply = new MainPhaseSelection(Other(testedSide), Position(b.Move));
                    avoidableImmediateDefeat = prior.GetLegalSelections(testedSide).Any(candidate => candidate != action &&
                        prior.Advance(new(testedSide, candidate), reply).Result?.Winner != Other(testedSide));
                }
            }
            int round = draft ? before.ResolvedRounds + 1 : before.ResolvedMainTurns + 1;
            rounds.Add(new(draft ? "draft" : "main", round, red, blue, after.PlayerOneScore, after.PlayerTwoScore));
            if (draft && after.ResolvedRounds == before.ResolvedRounds + 2)
            {
                var last = before.EligiblePositions.Single(p => p != Position(red.Move) && p != Position(blue.Move));
                var forced = new DecisionRecord(Coordinate(last), 0, 0, 0, 0);
                rounds.Add(new("auto-draft-collision", round + 1, forced, forced, after.PlayerOneScore, after.PlayerTwoScore));
            }
        }
        var end = session.Current;
        if (fixedStarts is not null && end.ResolvedMainTurns == 0)
            throw new InvalidOperationException($"Fixed draft {fixedStarts} did not reach playable Main on {id}.");
        string opening = end.Phase == MatchPhase.OpeningRestartRequired ? "retry" :
            end.ResolvedMainTurns == 0 ? "draftscore" : "main";
        return new(id, rules.ToString(), first.ToString(), second.ToString(), seed, firstSeed, secondSeed,
            firstIsRed ? "red" : "blue", fixedStarts, opening, end.FinalResult?.Winner?.ToString() ??
            (end.FinalResult.HasValue ? "Draw" : null), end.PlayerOneScore, end.PlayerTwoScore, end.ResolvedMainTurns,
            avoidableImmediateDefeat, placements, interiorPlacements, rounds);
    }

    // A conservative descriptive proxy, evaluated outside the decision stopwatch.
    private static bool IsInteriorPlacement(StrategicPosition position, MainPhaseState before, MainPhaseState after,
        PlayerId side, BoardPosition action, CancellationToken token = default)
    {
        if (after.Board.GetCell(action).Owner != side) return false;
        var one = before.GetLegalSelections(Red);
        var two = before.GetLegalSelections(Blue);
        var initial = position.Evaluate(before.Board, before.Territory, one, two, token).Contributions(side);
        if (position.InfluenceLead(action, side) < 3) return false;
        var successor = position.Evaluate(after.Board, after.Territory, after.GetLegalSelections(Red),
            after.GetLegalSelections(Blue), token).Contributions(side);
        return successor.Secured <= initial.Secured && successor.Influence <= initial.Influence + 0.1 &&
            successor.Mobility <= initial.Mobility + 0.1 && successor.Formation <= initial.Formation + 0.25;
    }

    private static DecisionRecord Decide(GameSessionSnapshot snapshot, PlayerId player, Opponent opponent, Random random,
        CancellationToken token = default)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        int depth = 0, pairs = 0;
        BoardPosition choice;
        switch (opponent.Kind)
        {
            case "strategic":
                CpuDecisionReport decision = Engine.DecideWithDiagnostics(snapshot, player,
                    CpuDifficultyProfiles.Get(opponent.Difficulty), random, token);
                choice = decision.Decision!.Value.Position;
                depth = decision.Search?.Depth ?? 0;
                pairs = decision.Search?.ResolvedPairs ?? 0;
                break;
            case "probe" when snapshot.Phase == MatchPhase.Main:
                token.ThrowIfCancellationRequested();
                choice = ArchivedProbe.Probe(snapshot, player, random, out pairs);
                depth = 1;
                break;
            default:
                var level = opponent.Kind == "probe" ? Frozen.CpuDifficulty.Normal :
                    (Frozen.CpuDifficulty)opponent.Difficulty;
                choice = Baseline.Decide(snapshot, player, Frozen.CpuDifficultyProfiles.Get(level), random, token)!.Value.Position;
                break;
        }
        double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        int legal = snapshot.Phase == MatchPhase.Opening ? snapshot.EligiblePositions.Count :
            snapshot.GetMainPhaseLegalSelections(player).Count;
        return new(Coordinate(choice), depth, pairs, elapsed, bytes, legal);
    }

    private static string FindStarts(MapDefinition map, RuleSet rules)
    {
        var start = new LocalGameSession(map, rules).Current;
        var pairs = from a in start.EligiblePositions from b in start.EligiblePositions
            where a.Y < b.Y || a.Y == b.Y && a.X < b.X
            orderby Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) descending, a.Y, a.X, b.Y, b.X
            select (a, b);
        foreach (var (a, b) in pairs)
        {
            GameSessionSnapshot next = start.PreviewDraft(a, b);
            while (next.Phase == MatchPhase.Opening)
                next = next.PreviewDraft(next.EligiblePositions[0], next.EligiblePositions[0]);
            if (next.Phase == MatchPhase.Main) return $"{Coordinate(a)}/{Coordinate(b)}";
        }
        throw new InvalidOperationException("Could not find a single-pair playable draft.");
    }

    private static void RunFixtures()
    {
        string reference = Path.Combine(AppContext.BaseDirectory, "Reference");
        var pocket = JsonSerializer.Deserialize<PocketFixture>(File.ReadAllText(Path.Combine(reference, "pocket-meadow.json")), Json)!;
        var session = Start(pocket.Map);
        Play(session, pocket.Draft[0], pocket.Draft[1]);
        foreach (var pair in pocket.Main) Play(session, pair[0], pair[1]);
        var snapshot = session.Current;
        var report = Engine.DecideWithDiagnostics(snapshot, Red, CpuDifficultyProfiles.Get(CpuDifficulty.Monster), new(42));
        var outcomes = (from a in Sorted(snapshot.GetMainPhaseLegalSelections(Red))
                        from b in Sorted(snapshot.GetMainPhaseLegalSelections(Blue))
                        select Outcome(snapshot.MainState!, Coordinate(a), Coordinate(b))).ToArray();
        Verify(outcomes.Single(o => o.Red == pocket.Historical[0] && o.Blue == pocket.Historical[1])
            is { RedScore: 1, BlueScore: 5, Finished: true }, "Pocket Meadow historical outcome");
        Verify(outcomes.Single(o => o.Red == pocket.RecordedAlternative[0] && o.Blue == pocket.RecordedAlternative[1])
            is { RedScore: 1, BlueScore: 1, Finished: false }, "Pocket Meadow recorded alternative");
        Write(new { fixture = pocket.Map, selected = Coordinate(report.Decision!.Value.Position), report.Search, outcomes });

        var lines = File.ReadLines(Path.Combine(reference, "manual-moves.jsonl"))
            .Select(line => JsonSerializer.Deserialize<ManualRound>(line, Json)!).GroupBy(r => r.Map);
        foreach (var match in lines)
        {
            session = Start(match.Key);
            int branchCount = 0;
            foreach (ManualRound step in match)
            {
                if (step.Phase == "auto-draft-collision") continue;
                snapshot = session.Current;
                int decisionSeed = step.CpuDecisionSeed ?? throw new InvalidOperationException("Missing manual decision seed.");
                var frozen = Decide(snapshot, Blue, new("static", CpuDifficulty.Monster), new(decisionSeed));
                Verify(frozen.Move == step.Blue, $"{step.Map} {step.Phase} {step.Round} frozen decision");
                var strategic = Decide(snapshot, Blue, new("strategic", CpuDifficulty.Monster), new(decisionSeed));
                if (snapshot.MainState is { } state)
                {
                    var alternative = Outcome(state, step.Red, strategic.Move);
                    var historical = Outcome(state, step.Red, step.Blue);
                    Write(new { fixture = step.Map, step.Round, step.RedScore, step.BlueScore, strategic, historical, alternative });
                    branchCount += strategic.Move == step.Blue ? 0 : 1;
                }
                Play(session, step.Red, step.Blue);
                Verify(session.Current.PlayerOneScore == step.RedScore && session.Current.PlayerTwoScore == step.BlueScore,
                    $"{step.Map} {step.Phase} {step.Round} scores");
            }
            Verify(session.Current.Phase == MatchPhase.Finished, $"{match.Key} final phase");
            Write(new { fixture = match.Key, historicalFinal = session.Current.FinalResult, strategicBranches = branchCount });
        }
    }

    private static PairOutcome Outcome(MainPhaseState state, string red, string blue)
    {
        BoardPosition redTarget = Position(red);
        BoardPosition blueTarget = Position(blue);
        var next = state.Advance(new(Red, redTarget), new(Blue, blueTarget));
        var indirect = new List<string>();
        for (int y = 0; y < state.Board.Height; y++)
        {
            for (int x = 0; x < state.Board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                if (position != redTarget && position != blueTarget && state.Board.GetCell(position).Piece == PieceType.Knight &&
                    next.Board.GetCell(position).Piece == PieceType.Grave)
                    indirect.Add(Coordinate(position));
            }
        }
        return new(red, blue, next.Territory.PlayerOneScore, next.Territory.PlayerTwoScore, next.IsFinished, indirect);
    }

    private static void Replay(string path)
    {
        int count = 0;
        foreach (string line in File.ReadLines(path))
        {
            var record = JsonSerializer.Deserialize<MatchRecord>(line, Json)!;
            var session = new LocalGameSession(HistoricalBattlefields.Resolve(record.Map).Definition,
                Enum.Parse<RuleSet>(record.Ruleset));
            RoundRecord? forced = null;
            foreach (RoundRecord round in record.Rounds)
            {
                var before = session.Current;
                if (round.Phase == "auto-draft-collision")
                {
                    Verify(forced is not null && forced.Round == round.Round && forced.Red.Move == round.Red.Move &&
                        forced.Blue.Move == round.Blue.Move, "automatic draft collision");
                    forced = null;
                }
                else
                {
                    bool draft = round.Phase == "draft";
                    Verify(forced is null && (draft || round.Phase == "main") &&
                        before.Phase == (draft ? MatchPhase.Opening : MatchPhase.Main) &&
                        round.Round == (draft ? before.ResolvedRounds : before.ResolvedMainTurns) + 1, "recorded phase and round");
                    Play(session, round.Red.Move, round.Blue.Move);
                    if (draft && session.Current.ResolvedRounds == before.ResolvedRounds + 2)
                    {
                        string last = Coordinate(before.EligiblePositions.Single(p =>
                            p != Position(round.Red.Move) && p != Position(round.Blue.Move)));
                        var decision = new DecisionRecord(last, 0, 0, 0, 0);
                        forced = round with { Round = round.Round + 1, Red = decision, Blue = decision };
                    }
                }
                Verify(session.Current.PlayerOneScore == round.RedScore && session.Current.PlayerTwoScore == round.BlueScore,
                    $"{record.Map} {round.Phase} {round.Round} replay scores");
            }
            var end = session.Current;
            string opening = end.Phase == MatchPhase.OpeningRestartRequired ? "retry" :
                end.ResolvedMainTurns == 0 ? "draftscore" : "main";
            string? outcome = end.FinalResult?.Winner?.ToString() ?? (end.FinalResult.HasValue ? "Draw" : null);
            Verify(forced is null && end.Phase is MatchPhase.Finished or MatchPhase.OpeningRestartRequired &&
                end.ResolvedMainTurns == record.Turns && opening == record.Opening && outcome == record.Result &&
                end.PlayerOneScore == record.RedScore && end.PlayerTwoScore == record.BlueScore, "replay final outcome");
            count++;
        }
        Console.WriteLine($"Verified {count} match histories.");
    }

    private static LocalGameSession Start(string map) =>
        new(HistoricalBattlefields.Resolve(map).Definition, RuleSet.KnightSupport);

    private static void Play(LocalGameSession session, string red, string blue, bool blueFirst = false)
    {
        var snapshot = session.Current;
        bool opening = snapshot.Phase == MatchPhase.Opening;
        if (blueFirst)
        {
            Submit(Blue, blue);
            Submit(Red, red);
        }
        else
        {
            Submit(Red, red);
            Submit(Blue, blue);
        }

        void Submit(PlayerId player, string move)
        {
            var result = opening ? session.SubmitOpeningSelection(new(player, Position(move), snapshot.RoundId)) :
                session.SubmitMainPhaseSelection(new(player, Position(move), snapshot.RoundId));
            Verify(result == SessionCommandResult.Accepted, $"{player} {move} on {snapshot.Phase} {snapshot.RoundId}: {result}");
        }
    }

    private static void Verify(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException($"Verification failed: {description}");
    }

    private static IEnumerable<BoardPosition> Sorted(IEnumerable<BoardPosition> positions) =>
        positions.OrderBy(p => p.Y).ThenBy(p => p.X);
    private static BoardPosition Position(string text) => new(char.ToUpperInvariant(text[0]) - 'A', int.Parse(text[1..]) - 1);
    private static string Coordinate(BoardPosition p) => $"{(char)('A' + p.X)}{p.Y + 1}";
    private static PlayerId Other(PlayerId side) => side == Red ? Blue : Red;
    private static void Write<T>(T row) => Console.WriteLine(JsonSerializer.Serialize(row, Json));

    private sealed record Opponent(string Kind, CpuDifficulty Difficulty)
    {
        internal static Opponent Parse(string value)
        {
            if (value == "probe") return new(value, CpuDifficulty.Normal);
            string[] parts = value.Split(':');
            if (parts.Length == 2 && parts[0] is "strategic" or "static" &&
                Enum.TryParse(parts[1], true, out CpuDifficulty level) && Enum.IsDefined(level))
                return new(parts[0], level);
            throw new ArgumentException($"Invalid opponent {value}.");
        }
        public override string ToString() => Kind == "probe" ? Kind : $"{Kind}:{Difficulty}";
    }

    private sealed record MatchRecord(string Map, string Ruleset, string First, string Second, int SeedIndex,
        int FirstSeed, int SecondSeed, string FirstSide, string? FixedStarts, string Opening, string? Result,
        int RedScore, int BlueScore, int Turns, bool AvoidableImmediateDefeat, int Placements, int InteriorPlacements,
        List<RoundRecord> Rounds);
    private sealed record RoundRecord(string Phase, int Round, DecisionRecord Red, DecisionRecord Blue, int RedScore, int BlueScore);
    private sealed record DecisionRecord(string Move, int Depth, int Pairs, double Milliseconds, long AllocatedBytes,
        int Legal = 0);
    private sealed record PairOutcome(string Red, string Blue, int RedScore, int BlueScore, bool Finished,
        List<string> OffTargetEliminations);
    private sealed record ManualRound(string Map, string Phase, int Round, string Red, string Blue,
        int? CpuDecisionSeed, int RedScore, int BlueScore);
    private sealed record PocketFixture(string Map, string[] Draft, string[][] Main, string[] Historical, string[] RecordedAlternative);
}
