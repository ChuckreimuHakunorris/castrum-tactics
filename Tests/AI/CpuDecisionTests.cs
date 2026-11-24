using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using CastrumTactics.Domain.Tests;
using Xunit;

namespace CastrumTactics.AI.Tests;

public sealed class CpuDecisionTests
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;
    private static readonly CpuProfile Monster = CpuDifficultyProfiles.Get(CpuDifficulty.Monster);
    private static readonly CpuProfile Normal = CpuDifficultyProfiles.Get(CpuDifficulty.Normal);
    private readonly CpuDecisionEngine _engine = new();

    [Fact]
    public void AllCatalogMapsOfferLegalDecisionsForEveryProfileAndBothPlayers()
    {
        foreach (PredefinedMap map in PredefinedMapCatalog.BuiltIn.Maps)
        {
            var opening = new LocalGameSession(map.Definition).Current;
            var main = StartPlayableMatch(map.Definition).Current;
            foreach (CpuDifficulty difficulty in Enum.GetValues<CpuDifficulty>())
            {
                var profile = CpuDifficultyProfiles.Get(difficulty);
                foreach (PlayerId player in new[] { One, Two })
                {
                    CheckDecision(opening, player, profile);
                    CheckDecision(main, player, profile);
                }
            }
        }
    }

    [Fact]
    public void TerminalSnapshotsReturnNoDecisionAndSingleCandidateIsAlwaysSelected()
    {
        var failed = new LocalGameSession(OpenMap([new(0, 0), new(2, 0)]));
        OpenRound(failed, new(0, 0), new(0, 0));
        Assert.Equal(MatchPhase.OpeningRestartRequired, failed.Current.Phase);
        Assert.Null(_engine.Decide(failed.Current, One, Monster, new Random(1)));
        var finished = Snapshot(ResolvedBoard.Parse("1#2"));
        Assert.Equal(MatchPhase.Finished, finished.Phase);
        Assert.Empty(_engine.DecideWithDiagnostics(finished, Two, Monster, new Random(1)).Candidates);

        var only = Snapshot(ResolvedBoard.Parse("1B"));
        foreach (PlayerId player in new[] { One, Two })
        {
            var result = _engine.DecideWithDiagnostics(only, player, Normal, new Random(1));
            Assert.Equal(new BoardPosition(0, 0), result.Decision!.Value.Position);
            Assert.Equal(1.0, Assert.Single(result.Candidates).Probability);
        }
    }

    [Fact]
    public void ValidatesPlayerProfileAndCancellation()
    {
        var snapshot = new LocalGameSession(OpenMap([new(0, 0), new(2, 0)])).Current;
        Assert.Throws<ArgumentNullException>(() => _engine.Decide(null!, One, Normal, new Random()));
        Assert.Throws<ArgumentNullException>(() => _engine.Decide(snapshot, One, null!, new Random()));
        Assert.Throws<ArgumentNullException>(() => _engine.Decide(snapshot, One, Normal, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.Decide(snapshot, (PlayerId)99, Normal, new Random()));
        Assert.Throws<OperationCanceledException>(() =>
            _engine.Decide(snapshot, One, Normal, new Random(), new CancellationToken(true)));

        using var source = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() =>
            _engine.Decide(snapshot, One, Normal, new FixedRandom(0.5, source.Cancel), source.Token));
    }

    [Fact]
    public void ProfilesAreValidatedAndSamplingHonorsRankPoolTemperatureAndBoundaries()
    {
        Assert.Equal(5, Enum.GetValues<CpuDifficulty>().Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => CpuDifficultyProfiles.Get((CpuDifficulty)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 0, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, 1, 11));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default(CpuFeatures) with { Capture = -1 }, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default(CpuFeatures) with { Risk = double.PositiveInfinity }, 1, 1, 1));

        CpuCandidate[] ranked = [new(new(0, 0), default, 0, 0), new(new(1, 0), default, -1, 0), new(new(2, 0), default, -2, 0)];
        Assert.Equal(0, CpuDecisionEngine.Sample(ranked, 2, 1, 0, new FixedRandom(0)));
        double boundary = ranked[0].Probability;
        Assert.Equal(1, CpuDecisionEngine.Sample(ranked, 2, 1, 0, new FixedRandom(boundary)));
        Assert.Equal(0, ranked[2].Probability);
        Assert.Equal(1, ranked.Sum(candidate => candidate.Probability), 12);
        CpuDecisionEngine.Sample(ranked, 2, 0.1, 0, new FixedRandom(0));
        Assert.True(ranked[0].Probability > boundary);
    }

    [Fact]
    public void MonsterPreservesOpeningVariationWithOnlyTwoStartsAndWinsMainRankingDeterministically()
    {
        var opening = new LocalGameSession(OpenMap([new(0, 0), new(1, 1)])).Current;
        var low = _engine.DecideWithDiagnostics(opening, One, Monster, new FixedRandom(0));
        var high = _engine.DecideWithDiagnostics(opening, One, Monster, new FixedRandom(0.99));
        Assert.NotEqual(low.Decision, high.Decision);
        Assert.All(low.Candidates, candidate => Assert.InRange(candidate.Probability, 0.35, 0.65));
        Assert.All(low.Candidates, candidate => Assert.True(candidate.Features.Risk > 0));

        var main = Snapshot(ResolvedBoard.Parse(".....", ".12..", "....."));
        var result = _engine.DecideWithDiagnostics(main, One, Monster, new FixedRandom(0.99));
        Assert.Equal(result.Candidates[0].Position, result.Decision!.Value.Position);
        Assert.Equal(1, result.Candidates[0].Probability);
        Assert.All(result.Candidates.Skip(1), candidate => Assert.Equal(0, candidate.Probability));
        var easy = _engine.DecideWithDiagnostics(main, One, CpuDifficultyProfiles.Get(CpuDifficulty.Easy), new Random(2));
        Assert.Contains(easy.Candidates.Skip(1), candidate => candidate.Probability > 0);
    }

    [Fact]
    public void DraftCollisionRiskRisesWithoutKnightsAndFallsAfterADistinctPair()
    {
        BoardPosition[] corners = [new(0, 0), new(2, 0), new(0, 2), new(2, 2)];
        var map = OpenMap(corners);
        var colliding = new LocalGameSession(map);
        var established = new LocalGameSession(map);
        double fresh = MaximumRisk(colliding.Current);
        OpenRound(colliding, corners[0], corners[0]);
        OpenRound(established, corners[0], corners[1]);
        Assert.True(MaximumRisk(colliding.Current) > fresh);
        Assert.True(MaximumRisk(established.Current) < fresh);

        double MaximumRisk(GameSessionSnapshot snapshot) =>
            _engine.DecideWithDiagnostics(snapshot, One, Monster, new Random(1))
                .Candidates.Max(candidate => candidate.Features.Risk);
    }

    [Fact]
    public void SeededChoicesAreIndependentOfStartOrderAndPreviousCalls()
    {
        BoardPosition[] starts = [new(2, 2), new(1, 1), new(0, 0)];
        var first = new LocalGameSession(OpenMap(starts)).Current;
        var reordered = new LocalGameSession(OpenMap(starts.Reverse())).Current;
        var a = _engine.DecideWithDiagnostics(first, One, Normal, new Random(5));
        _engine.Decide(first, Two, Monster, new Random(4));
        var b = _engine.DecideWithDiagnostics(reordered, One, Normal, new Random(5));
        Assert.Equal(a.Decision, b.Decision);
        Assert.Equal(a.Candidates, b.Candidates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingSelectionsAndFlagsDoNotAffectDecisions(bool main)
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("demonstration").Definition;
        LocalGameSession first = main ? StartPlayableMatch(map) : new(map);
        LocalGameSession second = main ? StartPlayableMatch(map) : new(map);
        var before = first.Current;
        BoardPosition[] targets = main
            ? before.GetMainPhaseLegalSelections(Two).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray()
            : before.EligiblePositions.ToArray();
        Assert.True(targets.Length >= 2);
        var a = _engine.DecideWithDiagnostics(before, One, Normal, new Random(1));
        Submit(first, new CpuDecision(Two, targets[0], before.Phase, before.RoundId));
        Submit(second, new CpuDecision(Two, targets[1], before.Phase, before.RoundId));
        Assert.True(first.Current.HasSubmitted(Two));
        var cells = before.Board.Cells();
        foreach (GameSessionSnapshot after in new[] { first.Current, second.Current })
        {
            var b = _engine.DecideWithDiagnostics(after, One, Normal, new Random(1));
            Assert.Equal(a.Decision, b.Decision);
            Assert.Equal(a.Candidates, b.Candidates);
            Assert.Equal(cells, after.Board.Cells());
        }
        Assert.Same(before.Board, first.Current.Board);
        Assert.Equal(cells, before.Board.Cells());
    }

    [Fact]
    public void CommandsFromOneSnapshotResolveAuthoritativelyInEitherOrder()
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("demonstration").Definition;
        var first = new LocalGameSession(map);
        var second = new LocalGameSession(map);
        ResolveCpuPair(first, second);
        first = StartPlayableMatch(map);
        second = StartPlayableMatch(map);
        ResolveCpuPair(first, second);

        void ResolveCpuPair(LocalGameSession left, LocalGameSession right)
        {
            var snapshot = left.Current;
            var cells = snapshot.Board.Cells();
            var one = _engine.Decide(snapshot, One, Normal, new Random(4))!.Value;
            var two = _engine.Decide(snapshot, Two, Monster, new Random(8))!.Value;
            Assert.Same(snapshot, left.Current);
            Assert.Equal(cells, snapshot.Board.Cells());
            BoardState? expected = snapshot.Phase == MatchPhase.Main
                ? MainPhaseState.Evaluate(snapshot.Board).ResolveTurn(new(One, one.Position), new(Two, two.Position))
                : null;

            Submit(left, one);
            Submit(left, two);
            Submit(right, two);
            Submit(right, one);
            Assert.Equal(left.Current.RoundId, right.Current.RoundId);
            Assert.Equal(left.Current.Phase, right.Current.Phase);
            Assert.Equal(left.Current.Board.Cells(), right.Current.Board.Cells());

            if (expected is not null) Assert.Equal(expected.Cells(), left.Current.Board.Cells());
            else if (one.Position == two.Position) Assert.Equal(PieceType.Grave, left.Current.Board.GetCell(one.Position).Piece);
            else
            {
                Assert.Equal(One, left.Current.Board.GetCell(one.Position).Owner);
                Assert.Equal(Two, left.Current.Board.GetCell(two.Position).Owner);
            }
        }
    }

    internal static GameSessionSnapshot Snapshot(BoardState board)
    {
        var draft = new OpeningDraft(OpenMap([new(0, 0), new(2, 0)])).Resolve(new(0, 0), new(2, 0));
        return new GameSessionSnapshot(draft, MainPhaseState.Evaluate(board), 0, 15, false, false);
    }

    internal static MapDefinition OpenMap(IEnumerable<BoardPosition> starts)
    {
        var grid = new TerrainType[3, 3];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                grid[y, x] = TerrainType.Playable;
        return new MapDefinition(3, 3, grid, starts);
    }

    internal static LocalGameSession StartPlayableMatch(MapDefinition map)
    {
        for (int i = 0; i < map.StartingPositions.Count; i++)
        {
            for (int j = i + 1; j < map.StartingPositions.Count; j++)
            {
                var session = new LocalGameSession(map);
                OpenRound(session, map.StartingPositions[i], map.StartingPositions[j]);
                while (session.Current.Phase == MatchPhase.Opening)
                {
                    var next = session.Current.EligiblePositions[0];
                    OpenRound(session, next, next);
                }

                if (session.Current.Phase == MatchPhase.Main) return session;
            }
        }

        throw new InvalidOperationException("No tested pair produced a nonterminal opening.");
    }

    private void CheckDecision(GameSessionSnapshot snapshot, PlayerId player, CpuProfile profile)
    {
        var decision = _engine.Decide(snapshot, player, profile, new Random(13))!.Value;
        Assert.Equal((player, snapshot.Phase, snapshot.RoundId), (decision.Player, decision.Phase, decision.RoundId));
        var legal = snapshot.Phase == MatchPhase.Opening ? snapshot.EligiblePositions.AsEnumerable()
            : snapshot.GetMainPhaseLegalSelections(player);
        Assert.Contains(decision.Position, legal);
    }

    private static void OpenRound(LocalGameSession session, BoardPosition first, BoardPosition second)
    {
        long round = session.Current.RoundId;
        Submit(session, new(One, first, MatchPhase.Opening, round));
        Submit(session, new(Two, second, MatchPhase.Opening, round));
    }

    private static void Submit(LocalGameSession session, CpuDecision decision) =>
        Assert.Equal(SessionCommandResult.Accepted, decision.Phase == MatchPhase.Opening
            ? session.SubmitOpeningSelection(new(decision.Player, decision.Position, decision.RoundId))
            : session.SubmitMainPhaseSelection(new(decision.Player, decision.Position, decision.RoundId)));

    private sealed class FixedRandom(double value, Action? onDraw = null) : Random
    {
        public override double NextDouble()
        {
            onDraw?.Invoke();
            return value;
        }
    }
}
