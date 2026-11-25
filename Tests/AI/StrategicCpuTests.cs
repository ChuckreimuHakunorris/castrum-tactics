using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using CastrumTactics.Domain.Tests;
using Xunit;

namespace CastrumTactics.AI.Tests;

public sealed class StrategicCpuTests
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;
    private static readonly CpuProfile Monster = CpuDifficultyProfiles.Get(CpuDifficulty.Monster);
    private static readonly CpuDecisionEngine Engine = new();

    [Fact]
    public void PocketMeadowRecognizesRecordedDefeatAndSearchesAnotherTurn()
    {
        var session = PocketMeadow();
        var before = session.Current;
        var cells = before.Board.Cells();
        var state = before.MainState!;
        var reply = new BoardPosition(1, 0);
        var historical = state.Advance(new(One, new(1, 1)), new(Two, reply));
        var alternative = state.Advance(new(One, reply), new(Two, reply));
        Assert.Equal(new MainPhaseResult(1, 5), historical.Result);
        Assert.Null(alternative.Result);
        Assert.Equal((1, 1), (alternative.Territory.PlayerOneScore, alternative.Territory.PlayerTwoScore));

        var report = Engine.DecideWithDiagnostics(before, One, Monster, new Random(42));
        var search = report.Search!;
        Assert.Equal(2, search.Depth);
        Assert.Equal(6, search.Replies.Count);
        Assert.Equal(1, search.Replies.Sum(r => r.Probability), 12);
        var rejected = search.Actions.Single(action => action.Position == new BoardPosition(1, 1));
        Assert.Equal(-10004, rejected.Adverse);
        var selected = report.Decision!.Value.Position;
        Assert.All(state.GetLegalSelections(Two), opponent =>
            Assert.NotEqual(Two, state.Advance(new(One, selected), new(Two, opponent)).Result?.Winner));
        Assert.Same(before, session.Current);
        Assert.Equal(cells, before.Board.Cells());

        Pair(session, selected, reply);
        Assert.Equal(state.Advance(new(One, selected), new(Two, reply)).Board.Cells(), session.Current.Board.Cells());
    }

    [Fact]
    public void ChecksOmittedCollisionWithoutChangingTheCommonExpectation()
    {
        var report = Engine.DecideWithDiagnostics(PocketMeadow().Current, One, Profile(1, 20, 1, 300), new Random(42));
        var search = report.Search!;
        var reply = Assert.Single(search.Replies);
        Assert.Equal(1, reply.Probability);
        Assert.Contains(search.Actions, a => a.Position != reply.Position && a.CollisionChecked && a.Adverse < a.Expected);
        Assert.InRange(search.ResolvedPairs, search.Actions.Count + 1, 2 * search.Actions.Count);
    }

    [Fact]
    public void PairLimitsAndWallTimeFallbackKeepASeededLegalDecision()
    {
        var snapshot = PocketMeadow().Current;
        var profile = Profile(2, 18, 10, 1);
        var a = Engine.DecideWithDiagnostics(snapshot, One, profile, new Random(42));
        var b = Engine.DecideWithDiagnostics(snapshot, One, profile, new Random(42));
        Assert.True(a.Search!.BudgetExhausted);
        Assert.Equal(0, a.Search.Depth);
        Assert.Equal(1, a.Search.ResolvedPairs);
        Assert.Equal(a.Decision, b.Decision);
        Assert.Equal(a.Candidates, b.Candidates);
        Assert.Contains(a.Decision!.Value.Position, snapshot.GetMainPhaseLegalSelections(One));

        var timed = new CpuDecisionEngine(TimeSpan.FromTicks(1))
            .DecideWithDiagnostics(snapshot, One, Monster, new Random(42));
        Assert.True(timed.Search!.TimeLimitReached);
        Assert.Contains(timed.Decision!.Value.Position, snapshot.GetMainPhaseLegalSelections(One));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuDecisionEngine(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuSearchOptions(3, 5, 5, 100, 0.4, 1));
    }

    [Fact]
    public void FallBackToACompleteFirstPassWhenTheSecondDoesNotFit()
    {
        var snapshot = PocketMeadow().Current;
        var one = Engine.DecideWithDiagnostics(snapshot, One, Profile(1, 18, 10, 24), new Random(42));
        var two = Engine.DecideWithDiagnostics(snapshot, One, Profile(2, 18, 10, 26), new Random(42));
        Assert.Equal(1, two.Search!.Depth);
        Assert.True(two.Search.BudgetExhausted);
        Assert.Equal(26, two.Search.ResolvedPairs);
        Assert.Equal(one.Decision, two.Decision);
        Assert.Equal(one.Candidates, two.Candidates);
    }

    [Fact]
    public void GuaranteedTerminalWinOutranksTheNonterminalCastleUpgrade()
    {
        var snapshot = CpuDecisionTests.Snapshot(ResolvedBoard.Parse(".A1.B."), RuleSet.KnightSupport);
        BoardPosition collision = new(3, 0);
        BoardPosition upgrade = new(2, 0);
        Assert.Null(snapshot.MainState!.Advance(new(One, upgrade), new(Two, collision)).Result);
        var report = Engine.DecideWithDiagnostics(snapshot, One, Monster, new Random(42));
        Assert.Equal(collision, report.Decision!.Value.Position);
        Assert.Equal(10001, report.Search!.Actions[0].Adverse);
        Assert.Equal(1, report.Candidates[0].Probability);
    }

    [Fact]
    public void StrategicFeaturesAreAntisymmetricAndProjectThroughContestedTerrain()
    {
        var board = ResolvedBoard.Parse("A1..#..", ".1.....", ".......", "......B");
        var swapped = ResolvedBoard.Parse("B2..#..", ".2.....", ".......", "......A");
        var value = Features(board);
        var mirror = Features(swapped);
        Assert.Equal(0, value.Secured);
        Assert.True(value.Influence > 0);
        Assert.Equal(value.Score(One), -value.Score(Two), 12);
        Assert.Equal(value.Score(One), -mirror.Score(One), 12);
    }

    [Fact]
    public void MeaningfulExpansionAddsMoreInfluenceThanSeveralLegalInteriorPlacements()
    {
        var board = ResolvedBoard.Parse("A1.....", ".1.....", ".1.....", ".......", "......B");
        var snapshot = CpuDecisionTests.Snapshot(board, RuleSet.KnightSupport);
        var state = snapshot.MainState!;
        BoardPosition[] interior = [new(0, 1), new(0, 2)];
        var frontier = new BoardPosition(2, 3);
        var reply = new BoardPosition(5, 3);
        var expansion = Features(state.Advance(new(One, frontier), new(Two, reply)).Board);
        foreach (BoardPosition square in interior)
        {
            Assert.Contains(square, state.GetLegalSelections(One));
            Assert.Equal(TerritoryControl.Contested, state.Territory.GetControl(square));
            var fill = Features(state.Advance(new(One, square), new(Two, reply)).Board);
            Assert.True(expansion.Influence > fill.Influence);
            Assert.True(expansion.Score(One) > fill.Score(One));
        }
        Assert.DoesNotContain(Engine.Decide(snapshot, One, Monster, new Random(42))!.Value.Position, interior);
    }

    [Fact]
    public void CastleBoundaryAndReinforcingAnInteriorSupporterHaveStrategicValue()
    {
        var closure = MainPhaseState.Evaluate(ResolvedBoard.Parse("B21A.."), RuleSet.KnightSupport);
        var fortify = closure.Advance(new(One, new(2, 0)), new(Two, new(1, 0)));
        Assert.Equal(PieceType.Castle, fortify.Board.GetCell(new(2, 0)).Piece);
        Assert.True(fortify.Territory.PlayerOneScore > closure.Territory.PlayerOneScore);
        Assert.Equal(One, fortify.Result?.Winner);

        var sole = ResolvedBoard.Parse(".......", "A11....", "..B....");
        var backup = ResolvedBoard.Parse("...1...", "A11....", "..B....");
        var loneState = MainPhaseState.Evaluate(sole, RuleSet.KnightSupport);
        var backedState = MainPhaseState.Evaluate(backup, RuleSet.KnightSupport);
        var attack = new MainPhaseSelection(One, new(3, 1));
        var capture = new MainPhaseSelection(Two, new(2, 1));
        var exposed = loneState.Advance(attack, capture);
        var reinforced = backedState.Advance(attack, capture);
        Assert.Equal(PieceType.Grave, exposed.Board.GetCell(attack.Position).Piece);
        Assert.Equal(One, reinforced.Board.GetCell(attack.Position).Owner);
        Assert.True(Features(reinforced.Board).Score(One) > Features(exposed.Board).Score(One));
        var vulnerable = Features(ResolvedBoard.Parse("A122.B."));
        var anchored = Features(ResolvedBoard.Parse("A122B.."));
        Assert.True(anchored.Contributions(Two).Formation > vulnerable.Contributions(Two).Formation);
    }

    [Fact]
    public void DraftPreviewMatchesActualAutomaticCollisionAndIgnoresPendingSelections()
    {
        var starts = new BoardPosition[] { new(0, 0), new(1, 1), new(2, 2) };
        var session = new LocalGameSession(CpuDecisionTests.OpenMap(starts), RuleSet.KnightSupport);
        var original = session.Current;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(One, starts[2], original.RoundId)));
        var preview = session.Current.PreviewDraft(starts[0], starts[2]);
        Assert.True(session.Current.HasSubmitted(One));
        Assert.False(preview.HasSubmitted(One));
        Assert.Equal(2, preview.ResolvedRounds);
        Assert.Equal(PieceType.Grave, preview.Board.GetCell(starts[1]).Piece);
        Pair(session, starts[0], starts[2]);
        Assert.Equal(preview.RoundId, session.Current.RoundId);
        Assert.Equal(preview.Phase, session.Current.Phase);
        Assert.Equal(preview.Board.Cells(), session.Current.Board.Cells());
        Assert.Throws<InvalidOperationException>(() => preview.PreviewDraft(starts[0], starts[1]));
    }

    [Fact]
    public void OmittedDraftCollisionLowersAdverseValueWithoutChangingTerminalCommonOutcome()
    {
        BoardPosition[] starts = [new(0, 0), new(1, 0), new(3, 0)];
        var terrain = new[,] { { TerrainType.Playable, TerrainType.Playable, TerrainType.Inaccessible,
            TerrainType.Playable, TerrainType.Playable } };
        var snapshot = new LocalGameSession(new MapDefinition(5, 1, terrain, starts), RuleSet.KnightSupport).Current;
        var common = starts[0];
        var omitted = starts[2];
        var distinct = snapshot.PreviewDraft(omitted, common);
        Assert.Equal(MatchPhase.Finished, distinct.Phase);
        Assert.Equal(2, distinct.ResolvedRounds);
        Assert.Equal(PieceType.Grave, distinct.Board.GetCell(starts[1]).Piece);
        Assert.Equal(new MainPhaseResult(2, 1), distinct.FinalResult);
        Assert.Equal(MatchPhase.Opening, snapshot.PreviewDraft(omitted, omitted).Phase);

        var search = AnalyzeDraft(snapshot, Profile(1, 5, 1, 5));
        Assert.Equal(new CpuReplyAnalysis(common, 1), Assert.Single(search.Replies));
        Assert.Equal(5, search.ResolvedPairs);
        Assert.False(search.BudgetExhausted);
        Assert.Equal(3, search.Actions.Count);
        Assert.All(search.Actions, action => Assert.True(action.CollisionChecked));
        var rating = search.Actions.Single(action => action.Position == omitted);
        Assert.Equal(1.575, rating.Expected, 12);
        Assert.Equal(0, rating.Adverse, 12);
    }

    [Theory]
    [InlineData(CpuDifficulty.Easy, 5, 61)]
    [InlineData(CpuDifficulty.Normal, 8, 91)]
    [InlineData(CpuDifficulty.Monster, 11, 121)]
    public void LargeDraftBudgetsIncludeEveryCollisionAndIgnorePendingOpponentChoices(
        CpuDifficulty difficulty, int replyCount, int pairCount)
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("broadshield-field").Definition;
        var first = new LocalGameSession(map, RuleSet.KnightSupport);
        var second = new LocalGameSession(map, RuleSet.KnightSupport);
        var snapshot = first.Current;
        Assert.Equal(11, snapshot.EligiblePositions.Count);
        var profile = CpuDifficultyProfiles.Get(difficulty);
        var baseline = Engine.DecideWithDiagnostics(snapshot, One, profile, new Random(42));
        var search = baseline.Search!;
        Assert.Equal(1, search.Depth);
        Assert.Equal(replyCount, search.Replies.Count);
        Assert.Equal(1, search.Replies.Sum(reply => reply.Probability), 12);
        Assert.Equal(pairCount, search.ResolvedPairs);
        Assert.InRange(search.ResolvedPairs, 1, profile.Search!.PairBudget);
        Assert.False(search.BudgetExhausted);
        Assert.Equal(snapshot.EligiblePositions.Count, search.Actions.Count);
        Assert.All(search.Actions, action => Assert.True(action.CollisionChecked));
        Assert.Contains(baseline.Decision!.Value.Position, snapshot.EligiblePositions);
        Assert.Equal(1, baseline.Candidates.Sum(candidate => candidate.Probability), 12);
        Assert.True(baseline.Candidates.Count(candidate => candidate.Probability > 0) >= 2);

        Assert.Equal(SessionCommandResult.Accepted, first.SubmitOpeningSelection(new(Two, snapshot.EligiblePositions[0], snapshot.RoundId)));
        Assert.Equal(SessionCommandResult.Accepted, second.SubmitOpeningSelection(new(Two, snapshot.EligiblePositions[^1], snapshot.RoundId)));
        foreach (var pending in new[] { first.Current, second.Current })
        {
            Assert.True(pending.HasSubmitted(Two));
            var replay = Engine.DecideWithDiagnostics(pending, One, profile, new Random(42));
            Assert.Equal(baseline.Decision, replay.Decision);
            Assert.Equal(baseline.Candidates, replay.Candidates);
            Assert.Equal(search.Actions, replay.Search!.Actions);
            Assert.Equal(search.Replies, replay.Search.Replies);
        }
    }

    [Fact]
    public void DraftCollisionCompletesOrFallsBackWhenTheLastPreviewDoesNotFit()
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("pocket-meadow").Definition;
        var snapshot = new LocalGameSession(map, RuleSet.KnightSupport).Current;
        var common = snapshot.EligiblePositions[0];
        var omitted = snapshot.EligiblePositions[1];
        Assert.Equal(MatchPhase.Main, snapshot.PreviewDraft(omitted, common).Phase);
        Assert.Equal(MatchPhase.OpeningRestartRequired, snapshot.PreviewDraft(omitted, omitted).Phase);

        var complete = AnalyzeDraft(snapshot, Profile(1, 5, 1, 3));
        Assert.Equal(new CpuReplyAnalysis(common, 1), Assert.Single(complete.Replies));
        Assert.Equal(3, complete.ResolvedPairs);
        Assert.False(complete.BudgetExhausted);
        var rating = complete.Actions.Single(action => action.Position == omitted);
        Assert.Equal(0, rating.Expected, 12);
        Assert.Equal(-1, rating.Adverse, 12);
        Assert.True(rating.CollisionChecked);

        var profile = Profile(1, 5, 1, 2);
        var fallback = Engine.DecideWithDiagnostics(snapshot, One, profile, new Random(42));
        Assert.Equal(0, fallback.Search!.Depth);
        Assert.Equal(2, fallback.Search.ResolvedPairs);
        Assert.True(fallback.Search.BudgetExhausted);
        Assert.Empty(fallback.Search.Actions);
        Assert.Contains(fallback.Decision!.Value.Position, snapshot.EligiblePositions);
        Assert.All(fallback.Candidates, candidate => Assert.True(candidate.Probability > 0));

        var timed = new CpuDecisionEngine(TimeSpan.FromTicks(1))
            .DecideWithDiagnostics(snapshot, One, Monster, new Random(42));
        Assert.True(timed.Search!.TimeLimitReached);
        Assert.Equal(0, timed.Search.Depth);
        Assert.Empty(timed.Search.Actions);
        Assert.Contains(timed.Decision!.Value.Position, snapshot.EligiblePositions);
    }

    [Fact]
    public void DraftPreviewLoopObservesCancellation()
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("broadshield-field").Definition;
        var snapshot = new LocalGameSession(map, RuleSet.KnightSupport).Current;
        using var source = new CancellationTokenSource();
        var search = new SimultaneousSearch(snapshot, One, Two, Monster, source.Token, 0, null);
        var ranked = snapshot.EligiblePositions.Select(position => new CpuCandidate(position, default, 0, 0)).ToArray();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => search.Analyze(snapshot, ranked, ranked, diagnostics: true));
    }

    private static CpuSearchReport AnalyzeDraft(GameSessionSnapshot snapshot, CpuProfile profile)
    {
        var ranked = snapshot.EligiblePositions.Select(position => new CpuCandidate(position, default, 0, 0)).ToArray();
        return new SimultaneousSearch(snapshot, One, Two, profile, default, 0, null)
            .Analyze(snapshot, ranked, ranked, diagnostics: true).Report!;
    }

    private static StrategicPosition.PositionFeatures Features(BoardState board)
    {
        var state = MainPhaseState.Evaluate(board, RuleSet.KnightSupport);
        return new StrategicPosition(board, RuleSetDefinition.Get(state.RuleSet), default).Evaluate(board,
            state.Territory, state.GetLegalSelections(One), state.GetLegalSelections(Two), default);
    }

    private static CpuProfile Profile(int depth, int own, int replies, int budget) =>
        new(Monster.Weights, 16, 1, 0, 0.5) { Search = new(depth, own, replies, budget, 0.4, 1) };

    private static LocalGameSession PocketMeadow()
    {
        var session = new LocalGameSession(PredefinedMapCatalog.BuiltIn.Get("pocket-meadow").Definition, RuleSet.KnightSupport);
        Pair(session, new(0, 0), new(2, 2));
        Pair(session, new(0, 1), new(1, 1));
        Pair(session, new(1, 1), new(0, 1));
        Pair(session, new(1, 0), new(1, 1));
        return session;
    }

    private static void Pair(LocalGameSession session, BoardPosition one, BoardPosition two)
    {
        long round = session.Current.RoundId;
        if (session.Current.Phase == MatchPhase.Opening)
        {
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(One, one, round)));
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Two, two, round)));
        }
        else
        {
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(One, one, round)));
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(Two, two, round)));
        }
    }
}
