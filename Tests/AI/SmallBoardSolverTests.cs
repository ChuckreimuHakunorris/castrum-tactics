extern alias benchmarks;

using System.IO.Compression;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using benchmarks::CastrumTactics.Development.Solver;
using BenchmarkMaps = benchmarks::CastrumTactics.Development.HistoricalBattlefields;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.AI.Tests;

public sealed class SmallBoardSolverTests
{
    private const PlayerId Red = PlayerId.PlayerOne;
    private const PlayerId Blue = PlayerId.PlayerTwo;

    [Fact]
    public void RectangularSaddleVerifiesTiesAndPureDominance()
    {
        Rational[,] matrix = { { 2, 1, 4 }, { 2, 1, 4 }, { 0, -1, 2 } };
        var game = MatrixGame.Solve(matrix);
        Assert.Equal((Rational)1, game.Value);
        Assert.Equal([Rational.Parse("1/2"), Rational.Parse("1/2"), 0], game.Red);
        Assert.Equal([0, 1, 0], game.Blue);
        Assert.Equal([false, false, true], MatrixGame.Dominated(matrix, true));
        Assert.Equal([true, false, true], MatrixGame.Dominated(matrix, false));
        Assert.Throws<InvalidDataException>(() => MatrixGame.Verify(matrix, game with { Value = 2 }));
        Assert.Throws<InvalidDataException>(() => MatrixGame.Verify(matrix, game with { Red = [2, -1, 0] }));
    }

    [Fact]
    public void MatchingPenniesAndRockPaperScissorsRequireMixing()
    {
        Rational[,] pennies = { { 1, -1 }, { -1, 1 } };
        Rational[,] rps = { { 0, -1, 1 }, { 1, 0, -1 }, { -1, 1, 0 } };
        foreach (var matrix in new[] { pennies, rps })
        {
            var game = MatrixGame.Solve(matrix);
            int n = matrix.GetLength(0);
            Assert.Equal((Rational)0, game.Value);
            Assert.All(game.Red.Concat(game.Blue), p => Assert.Equal((Rational)1 / n, p));
            Assert.Equal((Rational)(-1), MatrixGame.RowGuarantees(matrix).Max());
            Assert.Equal((Rational)1, MatrixGame.ColumnCeilings(matrix).Min());
        }
    }

    [Fact]
    public void ExactDegenerateRectangularMatrixAgreesWithIndependentTwoRowIntersection()
    {
        var tiny = Rational.Parse("1/100000000000000000000000000000000000000");
        Rational[,] matrix = { { tiny, -tiny, -tiny }, { -2 * tiny, 3 * tiny, 3 * tiny } };
        var game = MatrixGame.Solve(matrix);

        // Each reply is a line in Red's first-action probability. The optimum is at an endpoint or intersection.
        var candidates = new List<Rational> { 0, 1 };
        for (int j = 0; j < matrix.GetLength(1); j++)
        for (int k = j + 1; k < matrix.GetLength(1); k++)
        {
            var slopeDifference = (matrix[0,j] - matrix[1,j]) - (matrix[0,k] - matrix[1,k]);
            if (slopeDifference.IsZero) continue;
            var p = (matrix[1,k] - matrix[1,j]) / slopeDifference;
            if (!(p < 0) && !(p > 1)) candidates.Add(p);
        }

        var independent = candidates.Select(p => MatrixGame.ColumnExpectations(matrix, [p, 1-p]).Min()).Max();
        Assert.Equal(independent, game.Value);
        Assert.Equal(new[] { Rational.Parse("5/7"), Rational.Parse("2/7") }, game.Red);
        Assert.Equal(MatrixGame.ColumnExpectations(matrix, game.Red).Min(), MatrixGame.RowExpectations(matrix, game.Blue).Max());
    }

    [Fact]
    public void LowerAndUpperRationalGamesCertifyPoliciesAndRoundOutward()
    {
        Rational interval = Rational.Parse("1/17");
        Assert.Equal(Rational.Parse("1/100"), Rational.Parse("1/101").RoundOutward(100, upper: true));
        Assert.Equal((Rational)0, Rational.Parse("1/101").RoundOutward(100, upper: false));
        Assert.Equal(Rational.Parse("-1/100"), Rational.Parse("-1/101").RoundOutward(100, upper: false));
        Rational[,] lower = { { 1 - interval, -1 - interval }, { -1 - interval, 1 - interval } };
        Rational[,] upper = { { 1 + interval, -1 + interval }, { -1 + interval, 1 + interval } };
        var solved = MatrixGame.SolveBounds(lower, upper);
        Assert.Equal(-interval, solved.Lower);
        Assert.Equal(interval, solved.Upper);
        Assert.False(solved.IsExact);
        Assert.Equal([Rational.Parse("1/2"), Rational.Parse("1/2")], solved.Red);
        Assert.Equal([Rational.Parse("1/2"), Rational.Parse("1/2")], solved.Blue);
        Assert.Throws<InvalidDataException>(() => MatrixGame.VerifyBounds(lower, upper, solved with { Lower = 0 }));
    }

    [Fact]
    public void Binary64CandidateCannotBypassExactBounds()
    {
        Rational value = Rational.Parse("1/17");
        Rational[,] matrix = { { value, -value, -value }, { -2 * value, 3 * value, 3 * value } };
        var truth = MatrixGame.Solve(matrix);
        var bound = Binary64Policy.TryCertify(matrix, matrix, 100_000_000);
        Assert.NotNull(bound);
        Assert.False(truth.Value < bound.Lower);
        Assert.False(truth.Value > bound.Upper);
        Assert.False(bound.Upper - bound.Lower > Rational.Parse("1/100000000"));
        MatrixGame.VerifyBounds(matrix, matrix, bound);
    }

    [Fact]
    public void OneForcedCollisionAgreesWithIndependentSessionAndDrawByEnumeration()
    {
        var game = NewLine(3);
        var start = game.Current;
        var collision = start.PreviewDraft(new(0, 0), new(0, 0));
        Assert.Equal(MatchPhase.OpeningRestartRequired, collision.Phase);
        Assert.Null(collision.FinalResult);
        Draft(game);
        var before = game.Current;
        var graph = new SmallBoardGraph(SolveLimits.Default, default);
        var root = graph.Solve(before);
        var onlyPair = Assert.Single(root.Children);
        BoardPosition middle = new(1, 0);
        Assert.Equal(middle, Assert.Single(root.Red));
        Assert.Equal(middle, Assert.Single(root.Blue));
        Turn(game, middle, middle);

        Assert.Equal(new MainPhaseResult(1, 1), game.Current.FinalResult);
        Assert.Equal(PieceType.Grave, game.Current.Board.GetCell(middle).Piece);
        Assert.Equal(2, game.Current.Board.GetCell(middle).GroundHealth);
        Assert.Equal(PositionIdentity.Key(game.Current.Board, RuleSet.KnightSupport),
            PositionIdentity.Key(onlyPair.Board, RuleSet.KnightSupport));
        Assert.Equal((Rational)0, root.Game(SolverUtility.Outcome).Value);
        Assert.Equal((Rational)0, root.Game(SolverUtility.Margin).Value);
        Assert.Equal((true, true), graph.CanForceDraw(root));
        Assert.Equal((Rational)1, graph.PolicyPair(root, SolverUtility.Outcome).Draw);
    }

    [Fact]
    public void CanonicalGraphAndUnreducedGraphAgreeOnEveryPositionAndHistoryCount()
    {
        var session = NewLine(5);
        Draft(session);
        var graph = new SmallBoardGraph(SolveLimits.Default, default);
        var plain = new SmallBoardGraph(SolveLimits.Default, default, symmetry: false);
        var root = graph.Solve(session.Current);
        var unreduced = plain.Solve(session.Current);
        Assert.True(graph.Positions.Count < plain.Positions.Count);
        Assert.True(plain.Pairs > plain.Positions.Count - 1);

        var expected = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(plain.Statistics(unreduced)))!;
        var actual = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(graph.Statistics(root)))!;
        foreach (string field in new[] { "unique", "nonterminal", "terminal", "redWins", "blueWins", "draws",
            "pairTransitions", "distinctEdges", "repeatedDestinations", "maximumTurns", "completedHistories",
            "allPrefixes", "depths", "actionCounts", "objectives" })
            Assert.True(JsonNode.DeepEquals(expected[field], actual[field]), field);

        foreach (var node in plain.Positions.Values)
        foreach (var utility in Enum.GetValues<SolverUtility>())
        {
            var state = MainPhaseState.Evaluate(node.Board, node.Rules);
            var strategy = graph.Lookup(state, utility);
            Assert.Equal(node.Game(utility).Value, strategy.Value);
            if (!state.IsFinished) MatrixGame.Verify(node.Matrix(utility), strategy);
        }
    }

    [Fact]
    public void CompleteIdentityTracksCellsRulesAndDimensionsAndIgnoresSubmissionStatus()
    {
        var session = NewLine(5);
        Draft(session);
        var before = session.Current;
        string expected = PositionIdentity.Key(before.Board, before.RuleSet);
        var action = before.GetMainPhaseLegalSelections(Red).First();
        session.SubmitMainPhaseSelection(new(Red, action, before.RoundId));
        Assert.True(session.Current.HasSubmitted(Red));
        Assert.Equal(expected, PositionIdentity.Key(session.Current.Board, before.RuleSet));
        Assert.NotEqual(expected, PositionIdentity.Key(before.Board, RuleSet.Classic));
        Assert.NotEqual(expected, PositionIdentity.Key(NewLine(4).Current.Board, before.RuleSet));
        var terrain = new TerrainType[1, 5];
        for (int x = 0; x < 5; x++) terrain[0,x] = x == action.X ? TerrainType.Inaccessible : TerrainType.Playable;
        var changed = new BoardState(new MapDefinition(5, 1, terrain, []));
        var empty = NewLine(5).Current.Board;
        Assert.NotEqual(PositionIdentity.Key(empty, before.RuleSet), PositionIdentity.Key(changed, before.RuleSet));
        var cell = before.Board.GetCell(action);
        foreach (var change in new CellState[] {
            new(cell.Terrain, PieceType.None, null, int.MaxValue),
            new(cell.Terrain, PieceType.Knight, Red, cell.GroundHealth),
            new(cell.Terrain, PieceType.Knight, Blue, cell.GroundHealth),
            new(cell.Terrain, PieceType.Grave, null, cell.GroundHealth) })
        {
            var board = before.Board.WithResolvedCells(action, change);
            string key = PositionIdentity.Key(board, before.RuleSet);
            Assert.NotEqual(expected, key);
            Assert.Equal(change, PositionIdentity.Materialize(key, before.Board, default).GetCell(action));
        }
    }

    [Fact]
    public void PackedPhysicalCounterMatchesUnreducedSolverOnTractableBoard()
    {
        var session = NewLine(7);
        Draft(session);
        var exact = new SmallBoardGraph(SolveLimits.Default, default, symmetry: false);
        var root = exact.Solve(session.Current);
        Assert.True(PackedEnumeration.Supports(root.Board));
        var packed = new PackedEnumeration(session.Current.MainState!, SolveLimits.Default, true, default);
        packed.Run();
        Assert.True(packed.Expanded < exact.Positions.Count);
        var expected = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(exact.Statistics(root), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)))!;
        var actual = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(packed.Statistics()))!;
        foreach (string field in new[] { "unique", "nonterminal", "terminal", "redWins", "blueWins", "draws",
            "pairTransitions", "distinctEdges", "maximumTurns", "completedHistories", "allPrefixes", "depths", "actionCounts" })
            Assert.True(JsonNode.DeepEquals(expected[field], actual[field]), field);
    }

    [Fact]
    public void LockedHealthProjectionPreservesAuthoritativeActionHistoriesAndValues()
    {
        var session = NewLine(7);
        Draft(session);
        var full = new SmallBoardGraph(SolveLimits.Default, default);
        var quotient = new SmallBoardGraph(SolveLimits.Default, default, ignoreLockedHealth: true);
        var exact = full.Solve(session.Current);
        var compact = quotient.Solve(session.Current);
        Assert.True(quotient.Positions.Count < full.Positions.Count);
        foreach (var utility in Enum.GetValues<SolverUtility>())
            Assert.Equal(exact.Game(utility).Value, compact.Game(utility).Value);
        var expected = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(full.Statistics(exact)))!;
        var actual = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(quotient.Statistics(compact)))!;
        foreach (string field in new[] { "maximumTurns", "completedHistories", "allPrefixes" })
            Assert.True(JsonNode.DeepEquals(expected[field], actual[field]), field);
        foreach (var node in full.Positions.Values)
        foreach (var utility in Enum.GetValues<SolverUtility>())
        {
            var mapped = quotient.Lookup(MainPhaseState.Evaluate(node.Board, node.Rules), utility);
            Assert.Equal(node.Game(utility).Value, mapped.Value);
            if (node.Result is null) MatrixGame.Verify(node.Matrix(utility), mapped);
        }
    }

    [Fact]
    public void ExchangeNegatesValueAndRemapsPoliciesAndCoordinates()
    {
        var game = new Equilibrium(Rational.Parse("1/3"), [1, 0], [Rational.Parse("2/3"), Rational.Parse("1/3")]);
        BoardPosition[] red = [new(0,0), new(1,0)], blue = [new(1,2), new(2,2)];
        var transform = new BoardTransform(3, true);
        var converted = transform.PolicyToCanonical(game, red, blue, 3, 3);
        Assert.Equal(new BoardPosition(2,2), transform.Map(red[0], 3, 3));
        Assert.Equal(-game.Value, converted.Value);
        Assert.Equal(game.Blue.Reverse(), converted.Red);
        Assert.Equal(game.Red.Reverse(), converted.Blue);
        var back = transform.PolicyFromCanonical(converted, red, blue, 3, 3);
        Assert.Equal(game.Value, back.Value);
        Assert.Equal(game.Red, back.Red);
        Assert.Equal(game.Blue, back.Blue);
    }

    [Fact]
    public void ArchivedFixtureHasForcedBlueOutcomeEvenAfterAvoidingTheHistoricalImmediateLoss()
    {
        var archived = BenchmarkMaps.Get("pocket-meadow").Definition;
        var session = new LocalGameSession(archived, RuleSet.KnightSupport);
        Draft(session);
        Turn(session, new(0,1), new(1,1));
        Turn(session, new(1,1), new(0,1));
        Turn(session, new(1,0), new(1,1));
        var state = session.Current.MainState!;
        Assert.Equal(new MainPhaseResult(1, 5), state.Advance(new(Red, new(1,1)), new(Blue, new(1,0))).Result);
        Assert.Null(state.Advance(new(Red, new(1,0)), new(Blue, new(1,0))).Result);
        // Blue's B1 is a forcing line checked out to the end with that opponent choice fixed at the root.
        var continued = state.Advance(new(Red, new(1,0)), new(Blue, new(1,0)));
        Assert.True(PureForce(continued, Blue, new()));

        static bool PureForce(MainPhaseState state, PlayerId side, Dictionary<string,bool> memo)
        {
            if (state.Result is { } result) return result.Winner == side;
            string key = PositionIdentity.Key(state.Board, state.RuleSet);
            if (memo.TryGetValue(key, out bool known)) return known;
            var opponent = side == Red ? Blue : Red;
            return memo[key] = state.GetLegalSelections(side).Any(action => state.GetLegalSelections(opponent).All(reply =>
                PureForce(state.Advance(new(side, action), new(opponent, reply)), side, memo)));
        }
    }

    [Fact]
    public void LimitsAndCancellationKeepUnsolvedRootUncertified()
    {
        var session = NewLine(5);
        Draft(session);
        var limited = new SmallBoardGraph(SolveLimits.Default with { MaxStates = 2 }, default);
        Assert.Throws<SolveLimitException>(() => limited.Solve(session.Current));
        Assert.Throws<InvalidOperationException>(() => limited.Lookup(session.Current.MainState!, SolverUtility.Outcome));
        var memory = new SmallBoardGraph(SolveLimits.Default with { MaxManagedBytes = 1 }, default);
        Assert.Throws<SolveLimitException>(() => memory.Solve(session.Current));
        var time = new SmallBoardGraph(SolveLimits.Default with { MaxTime = TimeSpan.Zero }, default);
        Assert.Throws<SolveLimitException>(() => time.Solve(session.Current));
        var cancelled = new SmallBoardGraph(SolveLimits.Default, new CancellationToken(true));
        Assert.Throws<OperationCanceledException>(() => cancelled.Solve(session.Current));
    }

    [Fact]
    public void TablebaseRoundTripsValidatesFingerprintAndRejectsCorruptionAndUnsupportedBoards()
    {
        var session = NewLine(5);
        Draft(session);
        var graph = new SmallBoardGraph(SolveLimits.Default, default);
        var root = graph.Solve(session.Current);
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ctb");
        try
        {
            string expected = SolvedTablebase.Save(file, "test-line", "test-layout", "A1/E1", [], graph, true, default);
            var tablebase = SolvedTablebase.Load(file);
            Assert.Equal(expected, tablebase.Digest);
            var stored = tablebase.Get(session.Current.MainState!, SolverUtility.Outcome);
            Assert.Equal(root.Outcome!.Value, stored.Value);
            MatrixGame.Verify(root.Matrix(SolverUtility.Outcome), stored);
            Assert.Equal(tablebase.Decide(session.Current, Red, new Random(73)),
                tablebase.Decide(session.Current, Red, new Random(73)));
            var unsupported = NewLine(4);
            Draft(unsupported);
            Assert.Throws<InvalidOperationException>(() => tablebase.Decide(unsupported.Current, Red, new(1)));
            Assert.Throws<InvalidOperationException>(() => tablebase.Decide(NewLine(5).Current, Red, new(1)));

            string[] lines;
            using (var stream = new GZipStream(File.OpenRead(file), CompressionMode.Decompress))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                lines = reader.ReadToEnd().TrimEnd('\n').Split('\n');
            var metadata = JsonNode.Parse(lines[0])!;
            metadata["domainFingerprint"] = "different";
            lines[0] = metadata.ToJsonString();
            string body = string.Join('\n', lines[..^1]) + "\n";
            Rewrite(body + "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))) + "\n");
            Assert.Throws<InvalidDataException>(() => SolvedTablebase.Load(file));
            Rewrite("broken");
            Assert.Throws<InvalidDataException>(() => SolvedTablebase.Load(file));
        }
        finally { File.Delete(file); }

        void Rewrite(string contents)
        {
            using var stream = new GZipStream(File.Create(file), CompressionMode.Compress);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(contents);
        }
    }

    [Fact]
    public void ExactPolicySamplingIsSeededAndSkipsZeroMass()
    {
        Rational[] policy = [0, Rational.Parse("2/7"), Rational.Parse("5/7")];
        var first = new Random(37);
        var second = new Random(37);
        int[] one = Enumerable.Range(0, 64).Select(_ => SolvedTablebase.Sample(policy, first)).ToArray();
        int[] two = Enumerable.Range(0, 64).Select(_ => SolvedTablebase.Sample(policy, second)).ToArray();
        Assert.Equal(one, two);
        Assert.Contains(1, one);
        Assert.Contains(2, one);
        Assert.DoesNotContain(0, one);
    }

    private static LocalGameSession NewLine(int width)
    {
        var terrain = new TerrainType[1, width];
        for (int x = 0; x < width; x++) terrain[0,x] = TerrainType.Playable;
        return new(new(width, 1, terrain, [new(0,0), new(width-1,0)]), RuleSet.KnightSupport);
    }

    private static void Draft(LocalGameSession session)
    {
        var state = session.Current;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Red, state.EligiblePositions[0], state.RoundId)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Blue, state.EligiblePositions[1], state.RoundId)));
    }

    private static void Turn(LocalGameSession session, BoardPosition red, BoardPosition blue)
    {
        long round = session.Current.RoundId;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(Red, red, round)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(Blue, blue, round)));
    }
}
