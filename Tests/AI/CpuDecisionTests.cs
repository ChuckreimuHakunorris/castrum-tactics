using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using CastrumTactics.Domain.Tests;
using Xunit;
using Xunit.Abstractions;

namespace CastrumTactics.AI.Tests;

public sealed class CpuDecisionTests(ITestOutputHelper output)
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;
    private static readonly CpuProfile Monster = CpuDifficultyProfiles.Get(CpuDifficulty.Monster);
    private static readonly CpuProfile Normal = CpuDifficultyProfiles.Get(CpuDifficulty.Normal);
    private readonly CpuDecisionEngine _engine = new();

    [Theory]
    [InlineData(RuleSet.Classic)]
    [InlineData(RuleSet.KnightSupport)]
    public void AllCatalogMapsOfferLegalDecisionsForEveryProfileAndBothPlayers(RuleSet ruleSet)
    {
        foreach (PredefinedMap map in PredefinedMapCatalog.BuiltIn.Maps)
        {
            var opening = new LocalGameSession(map.Definition, ruleSet).Current;
            var main = StartPlayableMatch(map.Definition, ruleSet).Current;
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
    public void CatalogOpeningSurveyReportsImmediateTerminalsWithOneSuccessfulPair()
    {
        var terminalMaps = new List<string>();
        // Surplus castles can be replaced by draft graves without joining any components.
        // This only removes territory sources, so one castle per player suffices to witness any terminal opening.
        foreach (PredefinedMap map in PredefinedMapCatalog.BuiltIn.Maps)
        {
            var starts = map.Definition.StartingPositions;
            int terminalPairs = 0;
            for (int i = 0; i < starts.Count; i++)
            {
                for (int j = i + 1; j < starts.Count; j++)
                {
                    GameSessionSnapshot result = DraftOnePair(map.Definition, RuleSet.KnightSupport, i, j).Current;
                    if (result.Phase != MatchPhase.Finished) continue;
                    terminalPairs++;
                    Assert.Equal(0, result.ResolvedMainTurns);
                    Assert.Null(_engine.Decide(result, One, Monster, new Random(13)));
                }
            }
            if (terminalPairs > 0) terminalMaps.Add($"{map.Id} ({terminalPairs}/{starts.Count * (starts.Count - 1) / 2})");
        }
        Assert.Equal(new[] { "island-keep (8/15)" }, terminalMaps);
        output.WriteLine($"Immediate terminal maps (one castle each, all other starts collided): {string.Join(", ", terminalMaps)}");
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

    [Theory]
    [InlineData(RuleSet.Classic)]
    [InlineData(RuleSet.KnightSupport)]
    public void ValidatesPlayerProfileAndCancellation(RuleSet ruleSet)
    {
        var snapshot = new LocalGameSession(OpenMap([new(0, 0), new(2, 0)]), ruleSet).Current;
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
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 0, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, -1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, 1, double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, 1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, 1, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, 1, 1, 11));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CpuProfile(default(CpuEvaluationWeights) with { Capture = -1 }, 1, 1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CpuProfile(default(CpuEvaluationWeights) with { Risk = double.PositiveInfinity }, 1, 1, 1, 1));

        CpuCandidate[] ranked = [new(new(0, 0), default, 0, 0), new(new(1, 0), default, -1, 0), new(new(2, 0), default, -2, 0)];
        Assert.Equal(0, CandidateSampling.Sample(ranked, 2, 1, 0, new FixedRandom(0)));
        double boundary = ranked[0].Probability;
        Assert.Equal(1, CandidateSampling.Sample(ranked, 2, 1, 0, new FixedRandom(boundary)));
        Assert.Equal(0, ranked[2].Probability);
        Assert.Equal(1, ranked.Sum(candidate => candidate.Probability), 12);
        CandidateSampling.Sample(ranked, 2, 0.1, 0, new FixedRandom(0));
        Assert.True(ranked[0].Probability > boundary);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RejectsInvalidDiversityInEitherOpeningSituation(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, 1, 1, 1, openingDiversity: value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuProfile(default, 1, 1, 1, 1, urgentOpeningDiversity: value));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RejectsInvalidMaximumScoreDifferenceInEitherPhase(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CpuProfile(default, 1, 1, 1, 1, openingMaximumScoreDifference: value));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CpuProfile(default, 1, 1, 1, 1, mainMaximumScoreDifference: value));
    }

    [Fact]
    public void BuiltInProfilesUseIndependentLimitsAndCustomWeightsAreCopied()
    {
        (int Opening, int Main)[] limits = [(16, 12), (12, 8), (10, 5), (8, 3), (16, 1)];
        foreach (CpuDifficulty difficulty in Enum.GetValues<CpuDifficulty>())
        {
            var profile = CpuDifficultyProfiles.Get(difficulty);
            Assert.Equal(limits[(int)difficulty], (profile.OpeningCandidateLimit, profile.MainCandidateLimit));
            Assert.Equal((0.2, 0.7), (profile.OpeningDiversity, profile.UrgentOpeningDiversity));
            Assert.Null(profile.OpeningMaximumScoreDifference);
            Assert.Null(profile.MainMaximumScoreDifference);
        }

        var weights = default(CpuEvaluationWeights) with { Expansion = 2 };
        var custom = new CpuProfile(weights, 2, 8, 0.4, 1, 0, 1, 0.3, 2);
        weights = weights with { Expansion = 10 };
        Assert.Equal(2, custom.Weights.Expansion);
        Assert.Equal((2, 8), (custom.OpeningCandidateLimit, custom.MainCandidateLimit));
        Assert.Equal((0.4, 1.0), (custom.MainTemperature, custom.OpeningTemperature));
        Assert.Equal((0.0, 1.0), (custom.OpeningDiversity, custom.UrgentOpeningDiversity));
        Assert.Equal((0.3, 2.0), (custom.OpeningMaximumScoreDifference!.Value, custom.MainMaximumScoreDifference!.Value));
    }

    [Fact]
    public void NominalPoolIncludesCutoffTiesAndScoreCutoffUsesTheWeightedScale()
    {
        CpuCandidate[] ranked = Ranked(8, 6, 6, 6 - 5e-13, 6 - 2e-12, 2);
        int pool = CandidateSampling.GetPoolSize(ranked, 2, 1, null);
        Assert.Equal(4, pool);
        Assert.Equal(3, CandidateSampling.Sample(ranked, pool, 1, 0, new FixedRandom(1)));
        Assert.All(ranked.Take(pool), candidate => Assert.True(candidate.Probability > 0));
        Assert.All(ranked.Skip(pool), candidate => Assert.Equal(0, candidate.Probability));

        int unrestricted = CandidateSampling.GetPoolSize(ranked, ranked.Length, 1, null);
        Assert.Equal(ranked.Length, unrestricted);
        CandidateSampling.Sample(ranked, unrestricted, 1, 0, new FixedRandom(0));
        Assert.All(ranked, candidate => Assert.True(candidate.Probability > 0));
        pool = CandidateSampling.GetPoolSize(ranked, ranked.Length, 1, 2);
        Assert.Equal(4, pool);
        CandidateSampling.Sample(ranked, pool, 1, 0, new FixedRandom(0));
        Assert.All(ranked.Take(pool), candidate => Assert.True(candidate.Probability > 0));
        Assert.All(ranked.Skip(pool), candidate => Assert.Equal(0, candidate.Probability));
        Assert.Equal(1, ranked.Sum(candidate => candidate.Probability), 12);
    }

    [Fact]
    public void ZeroTemperatureSamplesEquivalentBestAndClearsReusedDiagnostics()
    {
        CpuCandidate[] ranked = Ranked(3.5, 3.5, 3.5 - 5e-13, 3.2);
        CandidateSampling.Sample(ranked, ranked.Length, 1, 0, new FixedRandom(0));
        foreach ((double draw, int index) in new[] { (0.0, 0), (1.0 / 3, 1), (2.0 / 3, 2), (1.0, 2) })
        {
            Assert.Equal(index, CandidateSampling.Sample(ranked, ranked.Length, 0, 0, new FixedRandom(draw)));
            Assert.Equal(new[] { 1.0 / 3, 1.0 / 3, 1.0 / 3, 0 }, ranked.Select(c => c.Probability));
        }

        int selected = CandidateSampling.Sample(ranked, 1, 0, 0, new Random(42));
        Assert.Equal(selected, CandidateSampling.Sample(ranked, 1, 0, 0, new Random(42)));
        Assert.Equal(3, CandidateSampling.GetPoolSize(ranked, 3, 1, 0));

        ranked = Ranked(0, -2, -3);
        Assert.Equal(0, CandidateSampling.Sample(ranked, 3, double.Epsilon, 0, new FixedRandom(1)));
        Assert.Equal(new[] { 1.0, 0, 0 }, ranked.Select(c => c.Probability));
        CandidateSampling.Sample(ranked, 3, 1, 0, new FixedRandom(0));
        Assert.Equal(0, CandidateSampling.Sample(ranked, 3, 0, 0, new FixedRandom(0.99)));
        Assert.Equal(new[] { 1.0, 0, 0 }, ranked.Select(c => c.Probability));
    }

    [Fact]
    public void MonsterRandomizesBetweenEquivalentBestCapturesOnTheMainBoard()
    {
        var snapshot = Snapshot(ResolvedBoard.Parse("12#12"));
        var left = _engine.DecideWithDiagnostics(snapshot, One, Monster, new FixedRandom(0));
        var right = _engine.DecideWithDiagnostics(snapshot, One, Monster, new FixedRandom(0.5));
        Assert.Equal(new BoardPosition(1, 0), left.Decision!.Value.Position);
        Assert.Equal(new BoardPosition(4, 0), right.Decision!.Value.Position);
        Assert.Equal(left.Candidates, right.Candidates);
        Assert.All(left.Candidates.Take(2), candidate => Assert.Equal(1.5328125, candidate.Score, 12));
        Assert.All(left.Candidates.Skip(2), candidate => Assert.Equal(0.5475, candidate.Score, 12));
        Assert.Equal(new[] { 0.5, 0.5, 0, 0 }, left.Candidates.Select(c => c.Probability));
    }

    [Fact]
    public void BannerPlainGivesMonsterFourEquallyLikelyOpeningChoices()
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("banner-plain").Definition;
        var snapshot = new LocalGameSession(map).Current;
        var result = _engine.DecideWithDiagnostics(snapshot, One, Monster, new Random(42));
        Assert.Equal(4, result.Candidates.Count);
        Assert.All(result.Candidates, candidate =>
        {
            Assert.Equal(result.Candidates[0].Score, candidate.Score, 12);
            Assert.Equal(0.25, candidate.Probability, 12);
        });
        var narrow = new CpuProfile(Monster.Weights, 1, 1, 0, Monster.OpeningTemperature);
        for (int i = 0; i < result.Candidates.Count; i++)
        {
            var sample = _engine.DecideWithDiagnostics(snapshot, One, narrow, new FixedRandom(i * 0.25));
            Assert.Equal(result.Candidates, sample.Candidates);
            Assert.Equal(result.Candidates[i].Position, sample.Decision!.Value.Position);
        }
        Assert.Equal(result.Decision, _engine.Decide(snapshot, One, Monster, new Random(42)));
    }

    [Fact]
    public void PhaseLimitsAndScoreTolerancesAreIndependentAndProtectTwoOpeningChoices()
    {
        var opening = new LocalGameSession(OpenMap([new(0, 0), new(0, 1), new(1, 1)])).Current;
        var main = Snapshot(ResolvedBoard.Parse(".....", ".12..", "....."));
        var openingNarrow = new CpuProfile(Monster.Weights, 1, 20, 1, 1);
        var mainNarrow = new CpuProfile(Monster.Weights, 20, 1, 1, 1);
        var openingGap = new CpuProfile(Monster.Weights, 20, 20, 1, 1, openingMaximumScoreDifference: 0);
        var mainGap = new CpuProfile(Monster.Weights, 20, 20, 1, 1, mainMaximumScoreDifference: 0);

        Assert.Equal(2, Eligible(opening, openingNarrow));
        Assert.Equal(3, Eligible(opening, mainNarrow));
        Assert.Equal(2, Eligible(opening, openingGap));
        Assert.Equal(3, Eligible(opening, mainGap));
        Assert.True(Eligible(main, openingNarrow) > 1);
        Assert.Equal(1, Eligible(main, mainNarrow));
        Assert.Equal(Eligible(main, openingNarrow), Eligible(main, openingGap));
        Assert.Equal(1, Eligible(main, mainGap));

        int Eligible(GameSessionSnapshot snapshot, CpuProfile profile) =>
            _engine.DecideWithDiagnostics(snapshot, One, profile, new FixedRandom(0))
                .Candidates.Count(c => c.Probability > 0);
    }

    [Theory]
    [InlineData(2, false, 0.75)]
    [InlineData(3, false, 0.25)]
    [InlineData(4, true, 0.25)]
    public void OpeningDiversityUsesTheConfiguredShareForTheCurrentSituation(int startCount, bool establishKnights, double share)
    {
        BoardPosition[] starts = establishKnights
            ? [new(0, 0), new(1, 1), new(2, 0), new(2, 2)]
            : [new(0, 0), new(1, 1), new(0, 1)];
        var session = new LocalGameSession(OpenMap(starts.Take(startCount)));
        if (establishKnights) OpenRound(session, starts[2], starts[3]);
        var snapshot = session.Current;
        var weights = default(CpuEvaluationWeights) with { Expansion = 2 };
        var softmax = new CpuProfile(weights, 2, 1, 0, 1, 0, 0);
        var mixed = new CpuProfile(weights, 2, 1, 0, 1, 0.25, 0.75);
        var baseline = _engine.DecideWithDiagnostics(snapshot, One, softmax, new FixedRandom(0));
        var actual = _engine.DecideWithDiagnostics(snapshot, One, mixed, new FixedRandom(0));
        Assert.True(baseline.Candidates[0].Probability > baseline.Candidates[1].Probability);
        for (int i = 0; i < actual.Candidates.Count; i++)
        {
            double expected = i < 2 ? (1 - share) * baseline.Candidates[i].Probability + share / 2 : 0;
            Assert.Equal(expected, actual.Candidates[i].Probability, 12);
        }
        Assert.Equal(1, actual.Candidates.Sum(c => c.Probability), 12);
    }

    [Fact]
    public void MonsterPreservesOpeningVariationAndAlwaysSelectsAUniqueMainPhaseBest()
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

    [Theory]
    [InlineData(RuleSet.Classic, PieceType.Knight)]
    [InlineData(RuleSet.KnightSupport, PieceType.Castle)]
    public void DraftCollisionRiskRisesWithoutFootholdsAndFallsAfterADistinctPair(RuleSet ruleSet, PieceType piece)
    {
        BoardPosition[] corners = [new(0, 0), new(2, 0), new(0, 2), new(2, 2)];
        var map = OpenMap(corners);
        var colliding = new LocalGameSession(map, ruleSet);
        var established = new LocalGameSession(map, ruleSet);
        double fresh = MaximumRisk(colliding.Current);
        OpenRound(colliding, corners[0], corners[0]);
        OpenRound(established, corners[0], corners[1]);
        Assert.Equal(piece, established.Current.Board.GetCell(corners[0]).Piece);
        Assert.True(MaximumRisk(colliding.Current) > fresh);
        Assert.True(MaximumRisk(established.Current) < fresh);

        double MaximumRisk(GameSessionSnapshot snapshot) =>
            _engine.DecideWithDiagnostics(snapshot, One, Monster, new Random(1))
                .Candidates.Max(candidate => candidate.Features.Risk);
    }

    [Theory]
    [InlineData(RuleSet.Classic)]
    [InlineData(RuleSet.KnightSupport)]
    public void SeededChoicesAreIndependentOfStartOrderAndPreviousCalls(RuleSet ruleSet)
    {
        BoardPosition[] starts = [new(2, 2), new(1, 1), new(0, 0)];
        var first = new LocalGameSession(OpenMap(starts), ruleSet).Current;
        var reordered = new LocalGameSession(OpenMap(starts.Reverse()), ruleSet).Current;
        var a = _engine.DecideWithDiagnostics(first, One, Normal, new Random(5));
        _engine.Decide(first, Two, Monster, new Random(4));
        var b = _engine.DecideWithDiagnostics(reordered, One, Normal, new Random(5));
        Assert.Equal(a.Decision, b.Decision);
        Assert.Equal(a.Candidates, b.Candidates);
    }

    [Theory]
    [InlineData(false, false, RuleSet.Classic)]
    [InlineData(true, false, RuleSet.Classic)]
    [InlineData(false, true, RuleSet.Classic)]
    [InlineData(true, true, RuleSet.Classic)]
    [InlineData(false, false, RuleSet.KnightSupport)]
    [InlineData(true, false, RuleSet.KnightSupport)]
    [InlineData(false, true, RuleSet.KnightSupport)]
    [InlineData(true, true, RuleSet.KnightSupport)]
    public void PendingSelectionsAndFlagsDoNotAffectDecisions(bool main, bool tie, RuleSet ruleSet)
    {
        var map = !main && tie
            ? OpenMap([new(0, 0), new(1, 1)])
            : PredefinedMapCatalog.BuiltIn.Get("demonstration").Definition;
        var profile = (main, tie) switch
        {
            (true, true) => new CpuProfile(default, 1, 1, 0, 1),
            (false, true) => new CpuProfile(Monster.Weights, 1, 1, 0, 1, urgentOpeningDiversity: 0.4),
            _ => Normal
        };
        LocalGameSession first = main ? StartPlayableMatch(map, ruleSet) : new(map, ruleSet);
        LocalGameSession second = main ? StartPlayableMatch(map, ruleSet) : new(map, ruleSet);
        var before = first.Current;
        BoardPosition[] targets = main
            ? before.GetMainPhaseLegalSelections(Two).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray()
            : before.EligiblePositions.ToArray();
        Assert.True(targets.Length >= 2);
        var a = _engine.DecideWithDiagnostics(before, One, profile, new Random(1));
        Submit(first, new CpuDecision(Two, targets[0], before.Phase, before.RoundId));
        Submit(second, new CpuDecision(Two, targets[1], before.Phase, before.RoundId));
        Assert.True(first.Current.HasSubmitted(Two));
        var cells = before.Board.Cells();
        foreach (GameSessionSnapshot after in new[] { first.Current, second.Current })
        {
            var b = _engine.DecideWithDiagnostics(after, One, profile, new Random(1));
            Assert.Equal(a.Decision, b.Decision);
            Assert.Equal(a.Candidates, b.Candidates);
            Assert.Equal(cells, after.Board.Cells());
        }
        Submit(first, new CpuDecision(Two, targets[1], before.Phase, before.RoundId));
        var revised = _engine.DecideWithDiagnostics(first.Current, One, profile, new Random(1));
        Assert.Equal(a.Decision, revised.Decision);
        Assert.Equal(a.Candidates, revised.Candidates);
        Assert.Same(before.Board, first.Current.Board);
        Assert.Equal(cells, before.Board.Cells());
    }

    [Fact]
    public async Task SharedEngineConcurrentCallsMatchSequentialEvaluations()
    {
        (GameSessionSnapshot Snapshot, PlayerId Player, CpuProfile Profile, int Seed)[] inputs =
        [
            (new LocalGameSession(PredefinedMapCatalog.BuiltIn.Get("banner-plain").Definition).Current, One, Monster, 1),
            (Snapshot(ResolvedBoard.Parse(".....", ".12..", ".....")), Two, Normal, 2)
        ];
        var expected = inputs.Select(input =>
            _engine.DecideWithDiagnostics(input.Snapshot, input.Player, input.Profile, new Random(input.Seed))).ToArray();
        using var bothInFlight = new CountdownEvent(inputs.Length);
        Task<CpuDecisionReport>[] workers = inputs.Select(input => Task.Factory.StartNew(() =>
        {
            bool sampled = false;
            var random = new FixedRandom(new Random(input.Seed).NextDouble(), () =>
            {
                if (sampled) throw new InvalidOperationException("A decision should use only one random draw.");
                sampled = true;
                bothInFlight.Signal();
                bothInFlight.Wait();
            });
            try
            {
                var result = _engine.DecideWithDiagnostics(input.Snapshot, input.Player, input.Profile, random);
                Assert.True(sampled);
                return result;
            }
            finally
            {
                if (!sampled) bothInFlight.Signal();
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        CpuDecisionReport[] actual = await Task.WhenAll(workers);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Decision, actual[i].Decision);
            Assert.Equal(expected[i].Candidates, actual[i].Candidates);
        }
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

    internal static GameSessionSnapshot Snapshot(BoardState board, RuleSet ruleSet = RuleSet.Classic)
    {
        var draft = new OpeningDraft(OpenMap([new(0, 0), new(2, 0)]), ruleSet).Resolve(new(0, 0), new(2, 0));
        return new GameSessionSnapshot(draft, MainPhaseState.Evaluate(board, ruleSet), 0, 15, false, false);
    }

    private static CpuCandidate[] Ranked(params double[] scores) => scores
        .Select((score, x) => new CpuCandidate(new BoardPosition(x, 0), default, score, 0)).ToArray();

    internal static MapDefinition OpenMap(IEnumerable<BoardPosition> starts)
    {
        var grid = new TerrainType[3, 3];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                grid[y, x] = TerrainType.Playable;
        return new MapDefinition(3, 3, grid, starts);
    }

    internal static LocalGameSession StartPlayableMatch(MapDefinition map, RuleSet ruleSet = RuleSet.Classic)
    {
        for (int i = 0; i < map.StartingPositions.Count; i++)
        {
            for (int j = i + 1; j < map.StartingPositions.Count; j++)
            {
                var session = DraftOnePair(map, ruleSet, i, j);
                if (session.Current.Phase == MatchPhase.Main) return session;
            }
        }

        throw new InvalidOperationException("No tested pair produced a nonterminal opening.");
    }

    private static LocalGameSession DraftOnePair(MapDefinition map, RuleSet ruleSet, int first, int second)
    {
        var session = new LocalGameSession(map, ruleSet);
        OpenRound(session, map.StartingPositions[first], map.StartingPositions[second]);
        while (session.Current.Phase == MatchPhase.Opening)
        {
            var next = session.Current.EligiblePositions[0];
            OpenRound(session, next, next);
        }
        return session;
    }

    private void CheckDecision(GameSessionSnapshot snapshot, PlayerId player, CpuProfile profile)
    {
        var report = _engine.DecideWithDiagnostics(snapshot, player, profile, new Random(13));
        var decision = report.Decision!.Value;
        Assert.Equal((player, snapshot.Phase, snapshot.RoundId), (decision.Player, decision.Phase, decision.RoundId));
        var legal = snapshot.Phase == MatchPhase.Opening ? snapshot.EligiblePositions.AsEnumerable()
            : snapshot.GetMainPhaseLegalSelections(player);
        Assert.Contains(decision.Position, legal);
        Assert.Equal(legal.OrderBy(p => p.Y).ThenBy(p => p.X),
            report.Candidates.Select(c => c.Position).OrderBy(p => p.Y).ThenBy(p => p.X));
        Assert.True(report.Candidates.Single(c => c.Position == decision.Position).Probability > 0);
        Assert.Equal(1, report.Candidates.Sum(c => c.Probability), 12);
        Assert.All(report.Candidates, candidate =>
        {
            Assert.InRange(candidate.Probability, 0, 1);
            Assert.True(double.IsFinite(candidate.Score));
            var f = candidate.Features;
            Assert.All(new[] { f.Expansion, f.Territory, f.Capture, f.Fortification, f.Mobility, f.Bottleneck,
                f.Spread, f.Risk, f.Formation, f.SupportRisk },
                value => Assert.InRange(value, 0, 1));
        });
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
