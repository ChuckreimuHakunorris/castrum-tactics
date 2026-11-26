using System.Diagnostics;
using CastrumTactics.Development;
using CastrumTactics.Development.Solver;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

internal static partial class Program
{
    private static int RunSolver(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            return RunSolver(args, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    private static int RunSolver(string[] args, CancellationToken token)
    {
        string[] allowed = ["--map", "--starts", "--path", "--max-states", "--max-seconds", "--max-mib",
            "--output", "--fixture", "--replay", "--analysis", "--tablebase", "--symmetry", "--ignore-locked-health", "--enumerate-only", "--interval-scale", "--fast-policy"];
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
            if (i + 1 == args.Length || !allowed.Contains(args[i]) || !options.TryAdd(args[i], args[i+1]))
                throw new ArgumentException($"Invalid or repeated solver option {args[i]}.");

        string mapId = options.GetValueOrDefault("--map", "pocket-meadow");
        var map = HistoricalBattlefields.Resolve(mapId);
        var session = new LocalGameSession(map.Definition, RuleSet.KnightSupport);
        var draft = session.Current;
        string starts = options.GetValueOrDefault("--starts") ?? ArchivedStarts.GetValueOrDefault(mapId) ??
            FindStarts(map.Definition, RuleSet.KnightSupport);
        var (redStart, blueStart) = ParsePair(starts);
        Play(session, Coordinate(redStart), Coordinate(blueStart));
        if (session.Current.MainState is null) throw new ArgumentException("A successful opening is required.");
        string[] path = options.TryGetValue("--path", out string? pathText) ? pathText.Split(',') : [];
        foreach (string pair in path)
        {
            if (session.Current.Phase != MatchPhase.Main) throw new ArgumentException("The position path continues past Main.");
            var (red, blue) = ParsePair(pair);
            Play(session, Coordinate(red), Coordinate(blue));
        }

        var defaults = SolveLimits.Default;
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        int maxStates = options.TryGetValue("--max-states", out var states) ? int.Parse(states, invariant) : defaults.MaxStates;
        double maxSeconds = options.TryGetValue("--max-seconds", out var seconds) ?
            double.Parse(seconds, invariant) : defaults.MaxTime.TotalSeconds;
        long maxMiB = options.TryGetValue("--max-mib", out var mib) ? long.Parse(mib, invariant) : defaults.MaxManagedBytes / (1024 * 1024);
        if (maxStates <= 0 || !double.IsFinite(maxSeconds) || maxSeconds <= 0 || maxMiB <= 0)
            throw new ArgumentException("Solver limits must be positive.");
        var limits = new SolveLimits(maxStates, TimeSpan.FromSeconds(maxSeconds), checked(maxMiB * 1024 * 1024));
        bool enumerateOnly = bool.Parse(options.GetValueOrDefault("--enumerate-only", "false"));
        bool symmetry = bool.Parse(options.GetValueOrDefault("--symmetry", "true"));
        bool standardPocketSolve = mapId == "pocket-meadow" && path.Length == 0 && !enumerateOnly;
        bool ignoreLockedHealth = options.TryGetValue("--ignore-locked-health", out var projection)
            ? bool.Parse(projection) : standardPocketSolve;
        if (enumerateOnly)
        {
            if (new[] { "--fixture", "--replay", "--analysis", "--tablebase", "--interval-scale", "--fast-policy" }.Any(options.ContainsKey))
                throw new ArgumentException("Enumeration-only mode measures graph statistics without payoffs, policies or tablebase.");
            if (!PackedEnumeration.Supports(session.Current.Board))
                throw new ArgumentException("Packed physical enumeration supports up to nine cells with ground health up to three.");
            return RunPackedEnumeration(options, mapId, starts, path, session.Current.MainState!, limits, symmetry, ignoreLockedHealth, token);
        }
        int intervalScale = options.TryGetValue("--interval-scale", out var scale)
            ? int.Parse(scale, invariant) : standardPocketSolve ? 100_000_000 : 0;
        if (intervalScale is < 0 or 1) throw new ArgumentException("Use 0 for exact or an interval scale of at least 2.");
        bool fastPolicy = options.TryGetValue("--fast-policy", out var fast)
            ? bool.Parse(fast) : standardPocketSolve && intervalScale > 0;
        if (fastPolicy && intervalScale == 0) throw new ArgumentException("Fast policy candidates require interval certification.");
        var graph = new SmallBoardGraph(limits, token, symmetry, ignoreLockedHealth, intervalScale, fastPolicy);
        try
        {
            var root = graph.Solve(session.Current);
            double solveSeconds = graph.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"Explored {graph.Positions.Count:N0} representatives in {solveSeconds:F1} s.");
            Console.Error.WriteLine(graph.IgnoresLockedHealth ? "Counts of positions merge permanent castle/grave health." :
                "Position counts retain all cell health.");
            var statistics = graph.Statistics(root);
            var draftResults = path.Length == 0 && draft.EligiblePositions.Count == 2
                ? DraftResults(draft, root, redStart, blueStart) : null;
            object? fixture = options.TryGetValue("--fixture", out var fixtureFile)
                ? InspectFixture(fixtureFile, s => BuildInspection(s, graph.Lookup), mapId) : null;
            object? replay = options.TryGetValue("--replay", out var replayFile)
                ? InspectReplays(replayFile, s => BuildInspection(s, graph.Lookup), mapId, token) : null;
            object? analysis = options.TryGetValue("--analysis", out string? sampleSize)
                ? AnalyzePolicies(graph, map, starts, path, int.Parse(sampleSize), token) : null;
            string? tablebaseDigest = options.TryGetValue("--tablebase", out var tablebasePath)
                ? SolvedTablebase.Save(tablebasePath, mapId, Identify(map, []).Digest, starts, path, graph, complete: true, token) : null;
            using var process = Process.GetCurrentProcess();
            var forcedDraw = graph.CanForceDraw(root);
            WriteSolverResult(options, new
            {
                map = mapId, rules = RuleSet.KnightSupport.ToString(), starts, path, complete = true,
                ignoresLockedHealth = graph.IgnoresLockedHealth,
                scope = path.Length == 0 ? "every Main/Finished position reachable from specified successful opening" :
                    "every Main/Finished position reachable from specified position",
                status = intervalScale == 0 ? "certified exact rational" : "certified rational intervals", intervalScale, fastPolicy,
                statistics, initial = InspectPosition(root), draft = draftResults, fixture, replay, analysis, tablebaseDigest,
                policyPair = Enum.GetValues<SolverUtility>().Select(utility =>
                {
                    var policy = graph.PolicyPair(root, utility);
                    return new { utility = utility.ToString(), redWin = policy.Win.ToString(),
                        draw = policy.Draw.ToString(), blueWin = policy.Loss.ToString(), turns = policy.Turns.ToString() };
                }).ToArray(),
                canForceDraw = new { red = forcedDraw.Red, blue = forcedDraw.Blue },
                resources = new { solveSeconds, process.PeakWorkingSet64,
                    managedBytes = GC.GetTotalMemory(false) }
            });
            return 0;
        }
        catch (Exception e) when (e is SolveLimitException or OperationCanceledException)
        {
            double interruptedAt = graph.Elapsed.TotalSeconds;
            string? partial = null;
            if (e is SolveLimitException && graph.Postorder.Count > 0 &&
                options.TryGetValue("--tablebase", out var partialPath))
                partial = SolvedTablebase.Save(partialPath, mapId, Identify(map, []).Digest, starts, path,
                    graph, complete: false, token);
            using var process = Process.GetCurrentProcess();
            WriteSolverResult(options, new { map = mapId, starts, path, complete = false, tablebaseDigest = partial,
                reason = e.Message, discovered = graph.Positions.Count, certified = graph.Postorder.Count,
                graph.Pairs, seconds = interruptedAt, managedBytes = GC.GetTotalMemory(false),
                process.PeakWorkingSet64 });
            return 3;
        }
    }

    private static int RunPackedEnumeration(Dictionary<string,string> options, string map, string starts, string[] path,
        MainPhaseState root, SolveLimits limits, bool symmetry, bool ignoreLockedHealth, CancellationToken token)
    {
        using var process = Process.GetCurrentProcess();
        var graph = new PackedEnumeration(root, limits, symmetry, token, ignoreLockedHealth);
        try
        {
            graph.Run();
            double solveSeconds = graph.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"Enumerated {graph.Expanded:N0} representatives in {solveSeconds:F1} s.");
            var statistics = graph.Statistics();
            WriteSolverResult(options, new { map, starts, path, complete = true, enumerationOnly = true, ignoresLockedHealth = ignoreLockedHealth, statistics,
                resources = new { solveSeconds, process.PeakWorkingSet64, managedBytes = GC.GetTotalMemory(false) } });
            return 0;
        }
        catch (Exception e) when (e is SolveLimitException or OperationCanceledException)
        {
            WriteSolverResult(options, new { map, starts, path, complete = false, enumerationOnly = true, ignoresLockedHealth = ignoreLockedHealth,
                reason = e.Message, discovered = Math.Max(graph.Visited, graph.Expanded), expanded = graph.Expanded,
                graph.Pairs, seconds = graph.Elapsed.TotalSeconds, process.PeakWorkingSet64, managedBytes = GC.GetTotalMemory(false) });
            return 3;
        }
    }

    private static SolvedTablebase LoadSolverTablebase(string path)
    {
        var tablebase = SolvedTablebase.Load(path);
        var metadata = tablebase.Info;
        if (Identify(HistoricalBattlefields.Resolve(metadata.Map), []).Digest != metadata.MapFingerprint)
            throw new InvalidDataException("Tablebase battlefield fingerprint differs from the current layout.");
        var session = OpenSolverSession(metadata.Map, metadata.Starts);
        foreach (var pair in metadata.Path)
        {
            var (red, blue) = ParsePair(pair);
            Play(session, Coordinate(red), Coordinate(blue));
        }
        var root = session.Current.MainState ?? throw new InvalidDataException("Tablebase root did not reach Main.");
        if (!metadata.Transforms.SequenceEqual(new[] { default(BoardTransform) }) &&
            !metadata.Transforms.SequenceEqual(BoardSymmetry.ForRoot(root, enabled: true).Transforms))
            throw new InvalidDataException("Tablebase transformations are incompatible with the recorded root.");
        return tablebase;
    }

    private static int QuerySolver(string[] args)
    {
        if (args.Length < 1)
            throw new ArgumentException("Use solver-query <tablebase> [--starts A1/C3] [--path pairs] [--replay file-or-directory] [--output file].");
        var tablebase = LoadSolverTablebase(args[0]);
        var options = new Dictionary<string, string>();
        for (int i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || args[i] is not ("--starts" or "--path" or "--replay" or "--fixture" or "--output" or "--opponent" or "--side" or "--seed") ||
                !options.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException($"Invalid or repeated query option {args[i]}.");
        int replayModes = new[] { "--replay", "--fixture" }.Count(options.ContainsKey);
        if (replayModes > 1 || (replayModes == 1 && new[] { "--starts", "--path", "--opponent", "--side", "--seed" }.Any(options.ContainsKey)) ||
            (!options.ContainsKey("--opponent") && (options.ContainsKey("--side") || options.ContainsKey("--seed"))))
            throw new ArgumentException("Choose either a replay, a fixture, or one position with an optional opponent comparison.");
        if (options.TryGetValue("--replay", out var replay))
            WriteSolverResult(options, InspectReplays(replay, Inspect, tablebase.Info.Map, default));
        else if (options.TryGetValue("--fixture", out var fixture))
            WriteSolverResult(options, InspectFixture(fixture, Inspect, tablebase.Info.Map));
        else
        {
            string starts = options.GetValueOrDefault("--starts", tablebase.Info.Starts);
            var session = OpenSolverSession(tablebase.Info.Map, starts);
            string[] path = options.TryGetValue("--path", out string? pathText) ? pathText.Split(',') : tablebase.Info.Path;
            foreach (string pair in path)
            {
                var (red, blue) = ParsePair(pair);
                Play(session, Coordinate(red), Coordinate(blue));
            }
            var position = Inspect(session.Current);
            if (options.TryGetValue("--opponent", out var opponent))
            {
                var side = options.GetValueOrDefault("--side", "red") switch
                {
                    "red" => Red,
                    "blue" => Blue,
                    _ => throw new ArgumentException("Use red or blue for --side.")
                };
                int seed = int.Parse(options.GetValueOrDefault("--seed", "42"));
                if (position.Result is not null) throw new ArgumentException("No CPU decision exists in a finished position.");
                if (opponent == "study")
                {
                    var decisions = SolverStudyOpponents().Select(spec => new { spec.Id,
                        decision = InspectSearch(session.Current, position, CreateOpponent(spec), side, null, default, seed) }).ToArray();
                    WriteSolverResult(options, new { position = InspectPosition(position), decisions });
                }
                else WriteSolverResult(options, new { position = InspectPosition(position),
                    decision = InspectSearch(session.Current, position, CreateOpponent(new("query", opponent)), side,
                        null, default, seed) });
            }
            else WriteSolverResult(options, InspectPosition(position));
        }
        return 0;

        SolvedPosition Inspect(GameSessionSnapshot snapshot) => BuildInspection(snapshot, tablebase.Get);
    }

    private static void WriteSolverResult(Dictionary<string, string> options, object result)
    {
        string output = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions(Json)
        {
            WriteIndented = true
        });
        if (options.TryGetValue("--output", out string? file)) SaveText(file, output + Environment.NewLine);
        else Console.WriteLine(output);
    }

    private static object DraftResults(GameSessionSnapshot draft, SolvedPosition root,
        BoardPosition redStart, BoardPosition blueStart)
    {
        var reverse = draft.PreviewDraft(blueStart, redStart);
        var board = root.Board;
        bool halfTurn = new BoardTransform(3, false).Apply(PositionIdentity.Key(board, draft.RuleSet), board.Width, board.Height) ==
            PositionIdentity.Key(reverse.Board, draft.RuleSet);

        return (from red in draft.EligiblePositions
                from blue in draft.EligiblePositions
                let result = draft.PreviewDraft(red, blue)
                let sign = red == redStart || halfTurn ? 1 : -1
                select new { red = Coordinate(red), blue = Coordinate(blue), phase = result.Phase.ToString(),
                    outcome = result.MainState is null ? null : (sign * root.Game(SolverUtility.Outcome).Value).ToString(),
                    margin = result.MainState is null ? null : (sign * root.Game(SolverUtility.Margin).Value).ToString() }).ToArray();
    }

    private static object InspectPosition(SolvedPosition node)
    {
        var board = node.Board;
        var state = MainPhaseState.Evaluate(board, node.Rules);
        return new
        {
            board = Enumerable.Range(0, board.Height).Select(y => Enumerable.Range(0, board.Width).Select(x =>
            {
                var position = new BoardPosition(x, y);
                var cell = board.GetCell(position);
                return new { at = Coordinate(position), terrain = cell.Terrain.ToString(),
                    owner = cell.Owner?.ToString(), piece = cell.Piece.ToString(), health = cell.GroundHealth,
                    control = state.Territory.GetControl(position).ToString() };
            })),
            redScore = state.Territory.PlayerOneScore, blueScore = state.Territory.PlayerTwoScore,
            red = node.Red.Select(Coordinate), blue = node.Blue.Select(Coordinate),
            solutions = Enum.GetValues<SolverUtility>().Select(u => InspectGame(node, u)).ToArray()
        };
    }

    private static object InspectGame(SolvedPosition node, SolverUtility utility)
    {
        var game = node.Game(utility);
        string[] Format(IEnumerable<Rational> numbers) => numbers.Select(n => n.ToString()).ToArray();
        var matrix = node.Matrix(utility);
        bool pointChildren = node.Children.All(child => child.Game(utility).IsExact);
        var lower = pointChildren ? matrix : node.Matrix(utility, -1);
        var upper = pointChildren ? matrix : node.Matrix(utility, 1);
        var redLower = MatrixGame.RowGuarantees(lower);
        var redUpper = MatrixGame.RowGuarantees(upper);
        var blueLower = MatrixGame.ColumnCeilings(upper).Select(v => -v).ToArray();
        var blueUpper = MatrixGame.ColumnCeilings(lower).Select(v => -v).ToArray();
        IEnumerable<string[]> FormatMatrix(Rational[,] m) => Enumerable.Range(0, node.Red.Length).Select(i =>
            Format(Enumerable.Range(0, node.Blue.Length).Select(j => m[i,j])));
        return new
        {
            utility = utility.ToString(), value = game.Value.ToString(),
            lower = game.Lower.ToString(), upper = game.Upper.ToString(),
            exploitabilityUpperBound = (game.Upper - game.Lower).ToString(), exact = game.IsExact,
            redPolicy = Format(game.Red), bluePolicy = Format(game.Blue),
            redGuaranteeBounds = new { lower = Format(redLower), upper = Format(redUpper) },
            blueGuaranteeBounds = new { lower = Format(blueLower), upper = Format(blueUpper) },
            redOptimalPure = node.Red.Where((_, i) => redLower[i] == game.Upper).Select(Coordinate),
            blueOptimalPure = node.Blue.Where((_, i) => blueLower[i] == -game.Lower).Select(Coordinate),
            redDominated = MatrixGame.Dominated(lower, upper, true),
            blueDominated = MatrixGame.Dominated(lower, upper, false),
            expectedRedMatchPointsBounds = utility == SolverUtility.Outcome ? new
            {
                lower = ((game.Lower + 1) / 2).ToString(), upper = ((game.Upper + 1) / 2).ToString()
            } : null,
            matrix = Enumerable.Range(0, node.Red.Length).Select(i =>
                Format(Enumerable.Range(0, node.Blue.Length).Select(j => matrix[i,j]))),
            matrixBounds = pointChildren ? null : new { lower = FormatMatrix(lower), upper = FormatMatrix(upper) }
        };
    }

    private static SolvedPosition BuildInspection(GameSessionSnapshot snapshot,
        Func<MainPhaseState, SolverUtility, Equilibrium> lookup)
    {
        var state = snapshot.MainState ?? throw new ArgumentException("Expected Main or Finished position.");
        var node = Create(state);
        for (int i = 0; i < node.Red.Length; i++)
        for (int j = 0; j < node.Blue.Length; j++)
        {
            var next = state.Advance(new(Red, node.Red[i]), new(Blue, node.Blue[j]));
            node.SetChild(i * node.Blue.Length + j, new(Create(next), false));
        }
        if (!state.IsFinished)
            foreach (var utility in Enum.GetValues<SolverUtility>())
                MatrixGame.VerifyBounds(node.Matrix(utility, -1), node.Matrix(utility, 1), node.Game(utility));
        return node;

        SolvedPosition Create(MainPhaseState position) => new(position, PositionIdentity.RemainingWork(position.Board))
        {
            Outcome = lookup(position, SolverUtility.Outcome),
            Margin = lookup(position, SolverUtility.Margin)
        };
    }
}
