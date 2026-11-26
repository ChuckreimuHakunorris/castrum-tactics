using CastrumTactics.AI;
using CastrumTactics.Development.Solver;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

internal static partial class Program
{
    private static object InspectFixture(string file, Func<GameSessionSnapshot, SolvedPosition> lookup, string mapId)
    {
        var fixture = System.Text.Json.JsonSerializer.Deserialize<PocketFixture>(File.ReadAllText(file), Json)!;
        if (fixture.Map != mapId) throw new ArgumentException("Fixture map differs from solve map.");
        var session = OpenSolverSession(mapId, string.Join('/', fixture.Draft));
        var turns = new List<object>();
        foreach (var pair in fixture.Main)
        {
            turns.Add(InspectTurn(lookup(session.Current), pair[0], pair[1]));
            Play(session, pair[0], pair[1]);
        }

        var node = lookup(session.Current);
        return new { turns, position = InspectPosition(node),
            historical = InspectTurn(node, fixture.Historical[0], fixture.Historical[1]),
            alternative = InspectTurn(node, fixture.RecordedAlternative[0], fixture.RecordedAlternative[1]),
            monster = InspectSearch(session.Current, node, CreateOpponent(new("monster", "strategic:Monster")),
                Red, fixture.Historical[0], default),
            probe = InspectSearch(session.Current, node, CreateOpponent(new("probe", "probe")),
                Blue, fixture.Historical[1], default) };
    }

    private static object InspectReplays(string path, Func<GameSessionSnapshot, SolvedPosition> lookup, string mapId, CancellationToken token)
    {
        if (Directory.Exists(path)) return InspectTournament(path, lookup, mapId, token);
        return File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line =>
        {
            token.ThrowIfCancellationRequested();
            using var document = System.Text.Json.JsonDocument.Parse(line);
            var match = document.RootElement;
            if (match.GetProperty("map").GetString() != mapId) return null;
            if (match.GetProperty("ruleset").GetString() is not ("KnightSupport" or "knightSupport" or "knight-support"))
                throw new ArgumentException("Replay uses a ruleset other than Knight Support.");
            var session = Start(mapId);
            var turns = new List<object>();
            foreach (var turn in match.GetProperty("rounds").EnumerateArray())
            {
                string phase = turn.GetProperty("phase").GetString()!;
                if (phase == "auto-draft-collision") continue;
                string red = turn.GetProperty("red").GetProperty("move").GetString()!;
                string blue = turn.GetProperty("blue").GetProperty("move").GetString()!;
                if (phase == "main")
                    turns.Add(new { number = session.Current.ResolvedMainTurns + 1,
                        solved = InspectTurn(lookup(session.Current), red, blue) });
                Play(session, red, blue);
                if (phase == "main" && (turn.GetProperty("redScore").GetInt32() != session.Current.PlayerOneScore ||
                    turn.GetProperty("blueScore").GetInt32() != session.Current.PlayerTwoScore))
                    throw new InvalidDataException("Replay scores differ from authoritative resolution.");
            }
            return new { match = match.Clone(), turns };
        }).Where(match => match is not null).ToArray();
    }

    private static object InspectTournament(string path, Func<GameSessionSnapshot, SolvedPosition> lookup, string mapId, CancellationToken token)
    {
        using var directoryLock = LockTournament(path);
        var manifest = ReadJson<Manifest>(ManifestPath(path));
        var results = CheckResults(path, manifest);
        if (manifest.Config.Ruleset != RuleSet.KnightSupport)
            throw new ArgumentException("Replay uses a ruleset other than Knight Support.");
        var people = manifest.Config.Participants.ToDictionary(p => p.Id, StringComparer.Ordinal);
        return manifest.Matches.Where(m => m.Map == mapId && results.GetValueOrDefault(m.Id) == "completed").Select(match =>
        {
            token.ThrowIfCancellationRequested();
            var record = LoadResult(path, match.Id);
            _ = Reconstruct(manifest, match, record);
            string redName = match.SeatsSwapped ? match.Second : match.First;
            string blueName = match.SeatsSwapped ? match.First : match.Second;
            var redOpponent = CreateOpponent(people[redName]);
            var blueOpponent = CreateOpponent(people[blueName]);
            var turns = record.Rounds.Where(r => r.Phase == "main").Select(turn =>
            {
                token.ThrowIfCancellationRequested();
                var snapshot = Reconstruct(manifest, match, record, "main", turn.Number);
                var node = lookup(snapshot);
                return new { turn.Number, solved = InspectTurn(node, turn.Red.Move, turn.Blue.Move),
                    red = InspectSearch(snapshot, node, redOpponent, Red, turn.Red.Move, token),
                    blue = InspectSearch(snapshot, node, blueOpponent, Blue, turn.Blue.Move, token) };
            }).ToArray();
            return new { match.Id, match.Seed, red = redName, blue = blueName, record.RedScore, record.BlueScore, turns };
        }).ToArray();
    }

    private static object? InspectSearch(GameSessionSnapshot snapshot, SolvedPosition node,
        TournamentOpponent opponent, PlayerId side, string? actual, CancellationToken token, int seed = 42)
    {
        if (opponent.Spec.Opponent.StartsWith("solver:", StringComparison.Ordinal)) return null;
        var diag = opponent.Decide(snapshot, side, new Random(seed), true, token);
        actual ??= diag.Move;
        bool red = side == Red;
        var own = red ? node.Red : node.Blue;
        var other = red ? node.Blue : node.Red;
        var m = node.Matrix(SolverUtility.Outcome);
        int selected = Array.IndexOf(own, Position(actual));
        Rational At(int i, int j) => red ? m[i,j] : -m[j,i];
        var guarantees = red ? MatrixGame.RowGuarantees(m) :
            MatrixGame.ColumnCeilings(m).Select(value => -value).ToArray();
        var adverse = other.Where((_, j) => At(selected,j) == guarantees[selected]).ToArray();
        var modeled = CpuPolicy(snapshot, side, own, diag);
        var search = diag.Diagnostics is CpuDecisionReport report ? report.Search : null;
        return new
        {
            actual,
            diagnosticSample = diag.Move,
            utilities = Enum.GetValues<SolverUtility>().Select(utility =>
            {
                var matrix = node.Matrix(utility);
                var value = red ? node.Game(utility).Value : -node.Game(utility).Value;
                var pure = red ? MatrixGame.RowGuarantees(matrix) :
                    MatrixGame.ColumnCeilings(matrix).Select(v => -v).ToArray();
                double guarantee = Enumerable.Range(0, other.Length).Min(j =>
                    modeled.Select((p, i) => p * (red ? matrix[i,j] : -matrix[j,i]).Approximate()).Sum());
                return new { utility = utility.ToString(), value = value.ToString(),
                    chosenPureGuarantee = pure[selected].ToString(), policyGuarantee = guarantee,
                    policyGap = value.Approximate() - guarantee };
            }),
            probabilities = own.Select((p, i) => new { at = Coordinate(p), probability = modeled[i] }),
            bestPure = own.Where((_, i) => guarantees[i] == guarantees.Max()).Select(Coordinate),
            mostAdverse = adverse.Select(Coordinate),
            omittedAdverse = search is null ? null : adverse.Where(p =>
                !search.Replies.Any(r => r.Position == p) &&
                !(p == own[selected] && search.Actions.Any(a => a.Position == p && a.CollisionChecked))).Select(Coordinate),
            search = search is null ? null : new
            {
                search.Depth, search.CompletedDepth, search.Matrix,
                actions = search.Actions.Select(a => new { at = Coordinate(a.Position), a.Expected, a.Adverse,
                    a.ProvenWin, guarantee = guarantees[Array.IndexOf(own, a.Position)].ToString() }),
                replies = search.Replies.Select(r => new { at = Coordinate(r.Position), r.Probability })
            }
        };
    }

    private static LocalGameSession OpenSolverSession(string mapId, string starts)
    {
        var session = new LocalGameSession(CastrumTactics.Development.HistoricalBattlefields.Resolve(mapId).Definition,
            RuleSet.KnightSupport);
        var (red, blue) = ParsePair(starts);
        Play(session, Coordinate(red), Coordinate(blue));
        return session;
    }

    private static object InspectTurn(SolvedPosition node, string red, string blue)
    {
        int i = Array.IndexOf(node.Red, Position(red)), j = Array.IndexOf(node.Blue, Position(blue));
        if (i < 0 || j < 0) throw new ArgumentException("Replay contains an illegal simultaneous pair.");
        var edge = node.Child(i,j);
        var child = edge.Target;
        var state = MainPhaseState.Evaluate(child.Board, child.Rules);
        int redScore = state.Territory.PlayerOneScore, blueScore = state.Territory.PlayerTwoScore;
        if (edge.Exchange) (redScore, blueScore) = (blueScore, redScore);
        return new { red, blue, redScore, blueScore, finished = child.Result is not null,
            objectives = Enum.GetValues<SolverUtility>().Select(u =>
            {
                var matrix = node.Matrix(u);
                var value = node.Game(u).Value;
                return new { utility = u.ToString(), before = value.ToString(), after = edge.Value(u).ToString(),
                    redGap = (value - MatrixGame.RowGuarantees(matrix)[i]).ToString(),
                    blueGap = (MatrixGame.ColumnCeilings(matrix)[j] - value).ToString() };
            }).ToArray() };
    }
}
