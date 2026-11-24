using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class LocalMainPhaseTests
{
    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;

    [Fact]
    public void EnteringMainPublishesSecuredSquaresFromTheResolvedDraft()
    {
        var session = CreateSession("....#..#..", 0, 2, 5, 8);
        ResolveOpening(session, 0, 2);
        Assert.Equal((0, 0), (session.Current.PlayerOneScore, session.Current.PlayerTwoScore));
        ResolveOpening(session, 5, 8);
        Assert.Equal(MatchPhase.Main, session.Current.Phase);
        Assert.Equal((2, 2), (session.Current.PlayerOneScore, session.Current.PlayerTwoScore));
    }

    [Fact]
    public void OpeningTransitionsWithExistingPiecesHealthAndPlayerSpecificTargets()
    {
        var session = CreateSession("......", 0, 2, 4);
        Assert.Null(session.Current.Territory);
        Assert.Equal((0, 0), (session.Current.PlayerOneScore, session.Current.PlayerTwoScore));
        Assert.Empty(session.Current.GetMainPhaseLegalSelections(One));
        ResolveOpening(session, 4, 4);
        GameSessionSnapshot beforeLast = session.Current;
        Assert.Null(beforeLast.FinalResult);
        ResolveOpening(session, 0, 2);

        GameSessionSnapshot main = session.Current;
        Assert.Equal(MatchPhase.Main, main.Phase);
        Assert.Equal(beforeLast.RoundId + 1, main.RoundId);
        Assert.Equal(2, main.ResolvedRounds);
        Assert.Equal(0, main.ResolvedMainTurns);
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, One, 3), main.Board.GetCell(At(0)));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, Two, 3), main.Board.GetCell(At(2)));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 2), main.Board.GetCell(At(4)));
        Assert.Equal(beforeLast.Board.GetCell(At(4)), main.Board.GetCell(At(4)));
        Assert.Empty(main.EligiblePositions);
        Assert.True(main.GetMainPhaseLegalSelections(One).SetEquals([At(0), At(1)]));
        Assert.True(main.GetMainPhaseLegalSelections(Two).SetEquals([At(1), At(2), At(3)]));
        Assert.Equal(TerritoryControl.Contested, main.Territory!.GetControl(At(1)));
        Assert.Equal(TerritoryControl.Neutral, main.Territory.GetControl(At(5)));
        Assert.Null(main.FinalResult);
        Assert.Equal((0, 0), (main.PlayerOneScore, main.PlayerTwoScore));
    }

    [Fact]
    public void ImmediateEndgamePublishesFinishedWithoutAnInterveningMainSnapshot()
    {
        var session = CreateSession(".#..#..", 0, 2, 6);
        GameSessionSnapshot initial = session.Current;
        var updates = new List<GameSessionSnapshot>();
        session.StateChanged += updates.Add;

        ResolveOpening(session, 0, 2);

        Assert.Equal(new[] { MatchPhase.Opening, MatchPhase.Finished }, updates.Select(update => update.Phase));
        GameSessionSnapshot finished = session.Current;
        Assert.Same(finished, updates[^1]);
        Assert.Equal(3, finished.RoundId);
        Assert.Equal(2, finished.ResolvedRounds);
        Assert.Equal(0, finished.ResolvedMainTurns);
        AssertFinished(finished, 1, 2, Two);
        Assert.Equal(TerritoryControl.SecuredByPlayerTwo, finished.Territory!.GetControl(At(3)));
        Assert.Equal(TerritoryControl.Neutral, finished.Territory!.GetControl(At(5)));
        Assert.Equal(PlayerId.PlayerTwo, finished.Board.GetCell(At(2)).Owner);
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 2), finished.Board.GetCell(At(6)));

        Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(finished.RoundId));
        Assert.Same(session.Current, updates[2]);
        AssertFreshDraft(initial, finished, session.Current);
        AssertFinished(finished, 1, 2, Two);
    }

    [Fact]
    public void PendingAndRevisedCoordinatesStayPrivateUntilBothPlayersSubmit()
    {
        var session = CreateSession("....", 0, 2);
        ResolveOpening(session, 0, 2);
        GameSessionSnapshot initial = session.Current;
        var updates = new List<GameSessionSnapshot>();
        session.StateChanged += updates.Add;

        SubmitMain(session, One, 0);
        SubmitMain(session, One, 1);

        Assert.Equal(2, updates.Count);
        Assert.All(updates, pending =>
        {
            Assert.Equal(initial.Phase, pending.Phase);
            Assert.Equal(initial.RoundId, pending.RoundId);
            Assert.Equal(initial.ResolvedRounds, pending.ResolvedRounds);
            Assert.Equal(initial.ResolvedMainTurns, pending.ResolvedMainTurns);
            Assert.Same(initial.Board, pending.Board);
            Assert.Same(initial.Territory, pending.Territory);
            Assert.Equal((initial.PlayerOneScore, initial.PlayerTwoScore), (pending.PlayerOneScore, pending.PlayerTwoScore));
            Assert.Same(initial.GetMainPhaseLegalSelections(One), pending.GetMainPhaseLegalSelections(One));
            Assert.Same(initial.GetMainPhaseLegalSelections(Two), pending.GetMainPhaseLegalSelections(Two));
            Assert.Empty(pending.EligiblePositions);
            Assert.Null(pending.FinalResult);
            Assert.True(pending.HasSubmitted(One));
            Assert.False(pending.HasSubmitted(Two));
        });

        SubmitMain(session, Two, 1);

        Assert.Equal(3, updates.Count);
        Assert.Same(session.Current, updates[^1]);
        Assert.Equal(initial.RoundId + 1, session.Current.RoundId);
        Assert.Equal(1, session.Current.ResolvedMainTurns);
        Assert.False(session.Current.HasSubmitted(One));
        Assert.False(session.Current.HasSubmitted(Two));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 2), session.Current.Board.GetCell(At(1)));
        Assert.Equal(PieceType.Knight, session.Current.Board.GetCell(At(0)).Piece);
        Assert.Equal(PieceType.None, updates[1].Board.GetCell(At(1)).Piece);
        Assert.True(updates[1].HasSubmitted(One));
    }

    [Fact]
    public void MainPhaseSubmissionOrderDoesNotChangeTheResolvedMatch()
    {
        var forward = CreateSession("....", 0, 2);
        var reverse = CreateSession("....", 0, 2);
        ResolveOpening(forward, 0, 2);
        ResolveOpening(reverse, 0, 2);

        ResolveMain(forward, 1, 3);
        SubmitMain(reverse, Two, 3);
        Assert.True(reverse.Current.HasSubmitted(Two));
        Assert.False(reverse.Current.HasSubmitted(One));
        SubmitMain(reverse, One, 1);

        Assert.Equal(forward.Current.Board.Cells(), reverse.Current.Board.Cells());
        Assert.Equal(forward.Current.Phase, reverse.Current.Phase);
        Assert.Equal(forward.Current.RoundId, reverse.Current.RoundId);
        Assert.Equal(forward.Current.FinalResult, reverse.Current.FinalResult);
        foreach (PlayerId player in new[] { One, Two })
        {
            Assert.True(forward.Current.GetMainPhaseLegalSelections(player).SetEquals(
                reverse.Current.GetMainPhaseLegalSelections(player)));
        }
    }

    [Fact]
    public void RejectionsLeaveMainPhasePendingChoiceAndPublishedStateUnchanged()
    {
        var session = CreateSession("....", 0, 2);
        long openingRound = session.Current.RoundId;
        Assert.Equal(SessionCommandResult.WrongPhase, session.SubmitMainPhaseSelection(new(One, At(0), openingRound)));
        ResolveOpening(session, 0, 2);
        SubmitMain(session, Two, 3);
        GameSessionSnapshot pending = session.Current;
        var updates = new List<GameSessionSnapshot>();
        session.StateChanged += updates.Add;

        Assert.Equal(SessionCommandResult.InvalidPlayer, session.SubmitMainPhaseSelection(new((PlayerId)0, At(1), pending.RoundId)));
        Assert.Equal(SessionCommandResult.StaleRound, session.SubmitMainPhaseSelection(new(One, At(1), openingRound)));
        Assert.Equal(SessionCommandResult.IneligiblePosition, session.SubmitMainPhaseSelection(new(Two, At(0), pending.RoundId)));
        Assert.Equal(SessionCommandResult.IneligiblePosition, session.SubmitMainPhaseSelection(new(One, At(-1), pending.RoundId)));
        Assert.Equal(SessionCommandResult.WrongPhase, session.SubmitOpeningSelection(new(One, At(1), pending.RoundId)));
        Assert.Equal(SessionCommandResult.WrongPhase, session.RestartDraft(pending.RoundId));
        Assert.Throws<ArgumentOutOfRangeException>("player", () => pending.GetMainPhaseLegalSelections((PlayerId)0));
        Assert.Empty(updates);
        Assert.Same(pending, session.Current);

        SubmitMain(session, One, 1);

        Assert.Same(session.Current, Assert.Single(updates));
        Assert.Equal(One, session.Current.Board.GetCell(At(1)).Owner);
        Assert.Equal(Two, session.Current.Board.GetCell(At(3)).Owner);
        Assert.Equal(SessionCommandResult.StaleRound, session.SubmitMainPhaseSelection(new(One, At(1), pending.RoundId)));
    }

    [Fact]
    public void CompleteMatchRefreshesTerritoryAndTargetsAfterPlacementCaptureUpgradeAndCollision()
    {
        var session = CreateSession("....", 0, 2);
        GameSessionSnapshot initial = session.Current;
        ResolveOpening(session, 0, 2);
        ResolveMain(session, 1, 3);
        GameSessionSnapshot placed = session.Current;
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, One, 3), placed.Board.GetCell(At(1)));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, Two, 3), placed.Board.GetCell(At(3)));
        Assert.Equal(TerritoryControl.Contested, placed.Territory!.GetControl(At(3)));
        Assert.Equal((0, 0), (placed.PlayerOneScore, placed.PlayerTwoScore));

        ResolveMain(session, 2, 3);
        GameSessionSnapshot captured = session.Current;
        Assert.Equal(MatchPhase.Main, captured.Phase);
        Assert.Equal(2, captured.ResolvedMainTurns);
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, One, 2), captured.Board.GetCell(At(2)));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Castle, Two, 3), captured.Board.GetCell(At(3)));
        Assert.Equal(TerritoryControl.SecuredByPlayerTwo, captured.Territory!.GetControl(At(3)));
        Assert.Equal((0, 1), (captured.PlayerOneScore, captured.PlayerTwoScore));
        Assert.Equal(TerritoryControl.Contested, placed.Territory!.GetControl(At(3)));
        Assert.True(captured.GetMainPhaseLegalSelections(Two).SetEquals([At(2)]));
        Assert.DoesNotContain(At(3), captured.GetMainPhaseLegalSelections(One));
        Assert.Equal(Two, placed.Board.GetCell(At(2)).Owner);

        ResolveMain(session, 2, 2);
        GameSessionSnapshot finished = session.Current;
        Assert.Equal(3, finished.ResolvedMainTurns);
        Assert.Equal(captured.RoundId + 1, finished.RoundId);
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 1), finished.Board.GetCell(At(2)));
        Assert.Equal(TerritoryControl.SecuredByPlayerOne, finished.Territory!.GetControl(At(1)));
        AssertFinished(finished, 2, 1, One);
        Assert.Equal((2, 1), (finished.PlayerOneScore, finished.PlayerTwoScore));

        var updates = new List<GameSessionSnapshot>();
        session.StateChanged += updates.Add;
        Assert.Equal(SessionCommandResult.WrongPhase, session.SubmitMainPhaseSelection(new(Two, At(1), finished.RoundId)));
        Assert.Equal(SessionCommandResult.WrongPhase, session.SubmitOpeningSelection(new(One, At(0), finished.RoundId)));
        Assert.Equal(SessionCommandResult.StaleRound, session.RestartDraft(captured.RoundId));
        Assert.Same(finished, session.Current);
        Assert.Empty(updates);

        Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(finished.RoundId));
        GameSessionSnapshot restarted = session.Current;
        Assert.Same(restarted, Assert.Single(updates));
        AssertFreshDraft(initial, finished, restarted);
        Assert.Equal(SessionCommandResult.StaleRound, session.RestartDraft(finished.RoundId));
        Assert.Equal(SessionCommandResult.StaleRound, session.SubmitOpeningSelection(new(One, At(0), initial.RoundId)));
        Assert.Equal(SessionCommandResult.StaleRound, session.SubmitMainPhaseSelection(new(One, At(2), captured.RoundId)));
        Assert.Same(restarted, session.Current);
        Assert.Single(updates);

        ResolveOpening(session, 0, 2);
        ResolveMain(session, 1, 3);
        ResolveMain(session, 2, 3);
        ResolveMain(session, 2, 2);
        AssertFinished(session.Current, 2, 1, One);
        AssertFinished(finished, 2, 1, One);
        Assert.Equal(PieceType.Castle, finished.Board.GetCell(At(3)).Piece);
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 1), finished.Board.GetCell(At(2)));
        AssertFreshDraft(initial, finished, restarted);
    }

    private static void AssertFreshDraft(GameSessionSnapshot initial, GameSessionSnapshot finished, GameSessionSnapshot restarted)
    {
        Assert.Equal(MatchPhase.Opening, restarted.Phase);
        Assert.Equal((0, 0), (restarted.PlayerOneScore, restarted.PlayerTwoScore));
        Assert.Equal(finished.RoundId + 1, restarted.RoundId);
        Assert.Equal(0, restarted.ResolvedRounds);
        Assert.Equal(0, restarted.ResolvedMainTurns);
        Assert.Equal(initial.EligiblePositions, restarted.EligiblePositions);
        Assert.NotSame(finished.Board, restarted.Board);
        Assert.Equal(initial.Board.Cells(), restarted.Board.Cells());
        Assert.Null(restarted.Territory);
        Assert.Null(restarted.FinalResult);
        Assert.Empty(restarted.GetMainPhaseLegalSelections(One));
        Assert.Empty(restarted.GetMainPhaseLegalSelections(Two));
        Assert.False(restarted.HasSubmitted(One));
        Assert.False(restarted.HasSubmitted(Two));
    }

    [Fact]
    public void FinishedMatchCanPublishADraw()
    {
        var session = CreateSession("...", 0, 2);
        ResolveOpening(session, 0, 2);
        ResolveMain(session, 1, 1);

        AssertFinished(session.Current, 1, 1, null);
    }

    private static void AssertFinished(GameSessionSnapshot snapshot, int one, int two, PlayerId? winner)
    {
        Assert.Equal(MatchPhase.Finished, snapshot.Phase);
        Assert.NotNull(snapshot.Territory);
        MainPhaseResult result = Assert.IsType<MainPhaseResult>(snapshot.FinalResult);
        Assert.Equal((one, two, winner), (result.PlayerOneScore, result.PlayerTwoScore, result.Winner));
        Assert.Empty(snapshot.EligiblePositions);
        Assert.Empty(snapshot.GetMainPhaseLegalSelections(One));
        Assert.Empty(snapshot.GetMainPhaseLegalSelections(Two));
        Assert.False(snapshot.HasSubmitted(One));
        Assert.False(snapshot.HasSubmitted(Two));
    }

    private static IGameSession CreateSession(string row, params int[] starts)
    {
        var terrain = new TerrainType[1, row.Length];
        for (int x = 0; x < row.Length; x++)
        {
            terrain[0, x] = row[x] == '#' ? TerrainType.Inaccessible : TerrainType.Playable;
        }
        return new LocalGameSession(new MapDefinition(row.Length, 1, terrain, starts.Select(At)));
    }

    private static BoardPosition At(int x) => new(x, 0);

    private static void ResolveOpening(IGameSession session, int first, int second)
    {
        long round = session.Current.RoundId;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(One, At(first), round)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Two, At(second), round)));
    }

    private static void ResolveMain(IGameSession session, int first, int second)
    {
        SubmitMain(session, One, first);
        SubmitMain(session, Two, second);
    }

    private static void SubmitMain(IGameSession session, PlayerId player, int position) =>
        Assert.Equal(SessionCommandResult.Accepted,
            session.SubmitMainPhaseSelection(new(player, At(position), session.Current.RoundId)));
}
