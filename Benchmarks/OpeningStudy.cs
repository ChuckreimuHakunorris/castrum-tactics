using System.Text.Json;
using CastrumTactics.AI;
using CastrumTactics.Development;
using CastrumTactics.Development.Solver;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

internal static partial class Program
{
    private sealed record OpeningStudySpec(string[] Tuning, string[] HeldOut, string[] Policies, int Samples = 16,
        int Seeds = 4, int Workers = 2, int Seed = 713);

    private sealed record OpeningCase(string Key, string[] History, GameSessionSnapshot State, int Castles);

    private static int RunOpeningStudy(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Use opening-study <config.json> <output-directory>.");
        var spec = ReadJson<OpeningStudySpec>(args[0]);
        if (spec.Samples <= 0 || spec.Seeds <= 0 || spec.Workers <= 0 || spec.Policies.Length == 0 ||
            spec.Tuning.Concat(spec.HeldOut).Distinct().Count() != spec.Tuning.Length + spec.HeldOut.Length)
            throw new ArgumentException("Use positive study limits and distinct maps.");
        string output = args[1];
        Directory.CreateDirectory(output);
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            var catalogs = spec.Tuning.Concat(spec.HeldOut).Select(map => OpeningCases(map, spec, cancellation.Token)).ToArray();
            SaveJson(Path.Combine(output, "cases.json"), catalogs.Select(c => new
            {
                c.Map, split = spec.HeldOut.Contains(c.Map) ? "heldOut" : "tuning",
                c.Exhaustive, c.Active, c.Terminal, c.Restarts,
                cases = c.Cases.Select(p => new { p.Key, p.History, p.Castles, p.State.Phase })
            }));
            var cases = (from catalog in catalogs from opening in catalog.Cases
                         where opening.State.Phase == MatchPhase.Main select (catalog.Map, Opening: opening)).ToArray();
            var rows = new string[cases.Length][];
            Parallel.For(0, cases.Length, new ParallelOptions
            {
                CancellationToken = cancellation.Token, MaxDegreeOfParallelism = spec.Workers
            }, index =>
            {
                var (map, opening) = cases[index];
                rows[index] = StudyOpening(spec, map, opening, cancellation.Token).ToArray();
            });
            SaveText(Path.Combine(output, "outcomes.jsonl"), string.Join('\n', rows.SelectMany(row => row)) + "\n");
            Write(new { configurations = cases.Length, matches = rows.Sum(r => r.Length), maps = catalogs.Length, output });
            return 0;
        }
        finally { Console.CancelKeyPress -= handler; }
    }

    private static IEnumerable<string> StudyOpening(OpeningStudySpec spec, string map, OpeningCase opening, CancellationToken token)
    {
        var state = opening.State;
        var features = new OpeningPosition(state.Board).Evaluate(state, token);
        var legacy = new StrategicPosition(state.Board, RuleSetDefinition.Get(state.RuleSet), token)
            .Evaluate(state.Board, state.Territory!, state.GetMainPhaseLegalSelections(Red),
                state.GetMainPhaseLegalSelections(Blue), token, opening: true).Score(Red);
        var redStarts = state.BoardPositions(Red);
        var blueStarts = state.BoardPositions(Blue);
        double redSpread = Spread(redStarts), blueSpread = Spread(blueStarts);
        int enemyDistance = redStarts.SelectMany(a => blueStarts.Select(b => Distance(a,b))).DefaultIfEmpty().Min();
        for (int index = 0; index < spec.Policies.Length; index++)
        {
            string opponent = spec.Policies[index];
            var config = new TournamentConfig
            {
                Id = "opening-study-" + opening.Key[..12] + "-" + index, Format = "self-play",
                Participants = [new("control", opponent)], Maps = new() { Ids = [map] },
                Opening = new() { Mode = "fixed", Fixed = new(StringComparer.Ordinal) { [map] = opening.History } },
                Seeds = Enumerable.Range(0, spec.Seeds).Select(i => StreamSeed(spec.Seed + i, map, "research")).ToArray(),
                SwapStarts = true, SwapSeats = false, Workers = 1, StrategicMetrics = false
            };
            var plan = PlanTournament(config);
            foreach (var match in plan.Matches)
            {
                var result = PlayTournamentMatch(plan, match, token);
                var early = EarlyBoard(state, match.StartsSwapped, result.Rounds);
                int sign = match.StartsSwapped ? -1 : 1;
                yield return JsonSerializer.Serialize(new
                {
                    map, split = spec.HeldOut.Contains(map) ? "heldOut" : "tuning", opening.Key,
                    opponent, match.Seed, match.StartsSwapped, result.Status, result.Opening, result.MainTurns,
                    result.Collisions, result.ForcedCollisions, result.RedCastles, result.BlueCastles,
                    result.HistoryDigest, result.Error, winner = result.Winner?.ToString(),
                    points = result.Winner == Red ? 1.0 : result.Winner == Blue ? 0 : 0.5,
                    margin = result.RedScore - result.BlueScore, legacy = sign * legacy,
                    evaluation = sign * features.Value(1), ablation = sign * features.Value(0),
                    secured = sign * features.Secured, coverage = sign * features.Coverage,
                    accessible = sign * features.Accessible, footholds = sign * features.Footholds, routes = sign * features.OpenRoutes,
                    friendlySpread = sign * (redSpread - blueSpread), enemyDistance,
                    legal = sign * (state.GetMainPhaseLegalSelections(Red).Count - state.GetMainPhaseLegalSelections(Blue).Count),
                    earlyMargin = sign * (early.Territory.PlayerOneScore - early.Territory.PlayerTwoScore),
                    earlyFormation = sign * (early.BoardPositions(Red).Length - early.BoardPositions(Blue).Length)
                }, Json);
            }
        }

        static int Distance(BoardPosition a, BoardPosition b) => Math.Max(Math.Abs(a.X-b.X), Math.Abs(a.Y-b.Y));
        static double Spread(BoardPosition[] positions) => positions.SelectMany((a, i) =>
            positions.Skip(i + 1).Select(b => (double)Distance(a,b))).DefaultIfEmpty().Average();
    }

    private static MainPhaseState EarlyBoard(GameSessionSnapshot opening, bool swap, List<TournamentRound> rounds)
    {
        // A swapped replay is resolved on the original opening with colors reversed.
        var state = opening.MainState!;
        var turns = rounds.Where(r => r.Phase == "main").Take(3);
        foreach (var turn in turns)
        {
            string red = swap ? turn.Blue.Move : turn.Red.Move;
            string blue = swap ? turn.Red.Move : turn.Blue.Move;
            state = state.Advance(new(Red, Position(red)), new(Blue, Position(blue)));
        }
        return state;
    }

    private static (string Map, bool Exhaustive, int Active, int Terminal, int Restarts, OpeningCase[] Cases)
        OpeningCases(string id, OpeningStudySpec spec, CancellationToken token)
    {
        var map = HistoricalBattlefields.Resolve(id);
        var root = new LocalGameSession(map.Definition, RuleSet.KnightSupport).Current;
        var board = root.Board;
        var emptyKey = PositionIdentity.Key(board, root.RuleSet);
        var eligibility = root.EligiblePositions.ToHashSet();
        var transforms = (from geometry in Enumerable.Range(0, board.Width == board.Height ? 8 : 4)
                          from exchange in new[] { false, true }
                          let transform = new BoardTransform(geometry, exchange)
                          where transform.Apply(emptyKey, board.Width, board.Height) == emptyKey &&
                                eligibility.SetEquals(eligibility.Select(p => transform.Map(p, board.Width, board.Height)))
                          select transform).ToArray();
        var symmetry = new BoardSymmetry(transforms);
        bool exhaustive = eligibility.Count <= 5;
        var resolved = new Dictionary<string, OpeningCase>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var random = new Random(StreamSeed(spec.Seed, id, "opening-cases"));
        if (exhaustive) Visit(root, []);
        else
        {
            for (int attempt = 0; attempt < Math.Max(128, spec.Samples * 24); attempt++)
            {
                token.ThrowIfCancellationRequested();
                var state = root;
                var path = new List<string>();
                double collisionRate = (attempt % 5) * 0.17;
                while (state.Phase == MatchPhase.Opening)
                {
                    var choices = state.EligiblePositions;
                    int first = random.Next(choices.Count);
                    int second = first;
                    if (choices.Count > 1 && random.NextDouble() >= collisionRate)
                        second = (first + 1 + random.Next(choices.Count - 1)) % choices.Count;
                    var a = choices[first];
                    var b = choices[second];
                    path.Add($"{Coordinate(a)}/{Coordinate(b)}");
                    state = state.PreviewDraft(a,b);
                }
                Add(state, path.ToArray());
            }
        }

        var active = resolved.Values.Where(p => p.State.Phase == MatchPhase.Main).ToArray();
        var strata = active.GroupBy(p => p.Castles).OrderBy(g => g.Key)
            .Select(g => new Queue<OpeningCase>(g.OrderBy(_ => random.Next()))).ToArray();
        var selected = new List<OpeningCase>();
        while (selected.Count < spec.Samples && strata.Any(q => q.Count > 0))
            foreach (var queue in strata)
                if (queue.Count > 0 && selected.Count < spec.Samples) selected.Add(queue.Dequeue());

        return (id, exhaustive, active.Length, resolved.Values.Count(p => p.State.Phase == MatchPhase.Finished),
            resolved.Values.Count(p => p.State.Phase == MatchPhase.OpeningRestartRequired), selected.ToArray());

        string Canonical(GameSessionSnapshot state) => symmetry.Canonicalize(PositionIdentity.Key(state.Board, state.RuleSet,
            ignoreLockedHealth: true), board.Width, board.Height).Key;

        void Visit(GameSessionSnapshot state, string[] path)
        {
            token.ThrowIfCancellationRequested();
            if (state.Phase != MatchPhase.Opening) { Add(state, path); return; }
            if (!visited.Add(Canonical(state))) return;
            foreach (var a in state.EligiblePositions)
            foreach (var b in state.EligiblePositions)
                Visit(state.PreviewDraft(a,b), [..path, $"{Coordinate(a)}/{Coordinate(b)}"]);
        }

        void Add(GameSessionSnapshot state, string[] path)
        {
            string key = Digest(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(Canonical(state))));
            resolved.TryAdd(key, new(key, path, state, state.BoardPositions(Red).Length));
        }
    }
}

internal static class OpeningStudyPositions
{
    internal static BoardPosition[] BoardPositions(this GameSessionSnapshot state, PlayerId side) =>
        state.Board.BoardPositions(side);

    internal static BoardPosition[] BoardPositions(this MainPhaseState state, PlayerId side) =>
        state.Board.BoardPositions(side);

    private static BoardPosition[] BoardPositions(this BoardState board, PlayerId side) =>
        (from y in Enumerable.Range(0, board.Height)
         from x in Enumerable.Range(0, board.Width)
         let p = new BoardPosition(x,y)
         where board.GetCell(p).Owner == side
         select p).ToArray();
}
