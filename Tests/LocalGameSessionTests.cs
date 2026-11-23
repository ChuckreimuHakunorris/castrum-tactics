using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class LocalGameSessionTests
{
    private static readonly BoardPosition First = new(0, 0);
    private static readonly BoardPosition Second = new(3, 1);
    private static readonly BoardPosition Third = new(1, 0);
    private static readonly BoardPosition Fourth = new(2, 1);
    private static readonly BoardPosition Fifth = new(0, 2);

    [Fact]
    public void PendingChoicePublishesOnlyStatusAndLeavesBothPlayersEligible()
    {
        var session = new LocalGameSession(CreateMap(First, Second));
        GameSessionSnapshot initial = session.Current;
        var updates = new List<GameSessionSnapshot>();
        session.StateChanged += updates.Add;

        Submit(session, PlayerId.PlayerOne, First);

        GameSessionSnapshot pending = Assert.Single(updates);
        Assert.Same(session.Current, pending);
        Assert.Same(initial.Board, pending.Board);
        Assert.Same(initial.EligiblePositions, pending.EligiblePositions);
        Assert.Equal(new[] { First, Second }, pending.EligiblePositions);
        Assert.Equal(initial.RoundId, pending.RoundId);
        Assert.Equal(0, pending.ResolvedRounds);
        Assert.Equal(MatchPhase.Opening, pending.Phase);
        Assert.True(pending.HasSubmitted(PlayerId.PlayerOne));
        Assert.False(pending.HasSubmitted(PlayerId.PlayerTwo));

        // A different pending choice has the same publicly observable state.
        Submit(session, PlayerId.PlayerOne, Second);
        GameSessionSnapshot revised = updates[1];
        Assert.Same(pending.Board, revised.Board);
        Assert.Same(pending.EligiblePositions, revised.EligiblePositions);
        Assert.Equal(pending.RoundId, revised.RoundId);
        Assert.Equal(pending.ResolvedRounds, revised.ResolvedRounds);
        Assert.Equal(pending.Phase, revised.Phase);
        Assert.Equal(pending.HasSubmitted(PlayerId.PlayerOne), revised.HasSubmitted(PlayerId.PlayerOne));
        Assert.Equal(pending.HasSubmitted(PlayerId.PlayerTwo), revised.HasSubmitted(PlayerId.PlayerTwo));

        Submit(session, PlayerId.PlayerTwo, Second);
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 2),
            session.Current.Board.GetCell(Second));
        Assert.False(session.Current.Board.IsPlayable(Second));
        Assert.Equal(PieceType.None, session.Current.Board.GetCell(First).Piece);
        Assert.Equal(PieceType.None, pending.Board.GetCell(Second).Piece);
        Assert.False(initial.HasSubmitted(PlayerId.PlayerOne));
        Assert.False(session.Current.HasSubmitted(PlayerId.PlayerOne));
        Assert.False(session.Current.HasSubmitted(PlayerId.PlayerTwo));
        AssertKnightCounts(session.Current.Board, 0);
        Assert.Equal(3, updates.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolvedPairIsIndependentOfSubmissionOrder(bool collision)
    {
        var forward = new LocalGameSession(CreateMap(First, Second, Third));
        var reverse = new LocalGameSession(CreateMap(First, Second, Third));
        BoardPosition playerTwoChoice = collision ? First : Second;

        Submit(forward, PlayerId.PlayerOne, First);
        Submit(forward, PlayerId.PlayerTwo, playerTwoChoice);
        Submit(reverse, PlayerId.PlayerTwo, playerTwoChoice);
        Submit(reverse, PlayerId.PlayerOne, First);

        Assert.Equal(Cells(forward.Current.Board), Cells(reverse.Current.Board));
        Assert.Equal(forward.Current.EligiblePositions, reverse.Current.EligiblePositions);
        Assert.Equal(1, forward.Current.ResolvedRounds);
        Assert.Equal(1, reverse.Current.ResolvedRounds);
        Assert.Equal(MatchPhase.Opening, forward.Current.Phase);
        Assert.Equal(MatchPhase.Opening, reverse.Current.Phase);
        if (collision)
        {
            Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 2), forward.Current.Board.GetCell(First));
            Assert.Equal(new[] { Second, Third }, forward.Current.EligiblePositions);
            AssertKnightCounts(forward.Current.Board, 0);
        }
        else
        {
            Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerOne, 3),
                forward.Current.Board.GetCell(First));
            Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, PlayerId.PlayerTwo, 3),
                forward.Current.Board.GetCell(Second));
            Assert.Equal(Third, Assert.Single(forward.Current.EligiblePositions));
            AssertKnightCounts(forward.Current.Board, 1);
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    [InlineData(2, 0)] // Inaccessible.
    [InlineData(3, 0)] // Playable but absent from the map's starts.
    public void InvalidPositionsDoNotReplaceAPendingChoiceOrPublishState(int x, int y)
    {
        var session = new LocalGameSession(CreateMap(First, Second, Third));
        Submit(session, PlayerId.PlayerOne, First);
        GameSessionSnapshot pending = session.Current;
        int updates = 0;
        session.StateChanged += _ => updates++;

        var invalid = new BoardPosition(x, y);
        Assert.Equal(SessionCommandResult.IneligiblePosition,
            session.SubmitOpeningSelection(new OpeningSelection(PlayerId.PlayerOne, invalid, pending.RoundId)));
        Assert.Equal(SessionCommandResult.IneligiblePosition,
            session.SubmitOpeningSelection(new OpeningSelection(PlayerId.PlayerTwo, invalid, pending.RoundId)));
        Assert.Same(pending, session.Current);
        Assert.Equal(0, updates);

        Submit(session, PlayerId.PlayerTwo, Second);
        Assert.Equal(PlayerId.PlayerOne, session.Current.Board.GetCell(First).Owner);
        Assert.Equal(PlayerId.PlayerTwo, session.Current.Board.GetCell(Second).Owner);
        Assert.Equal(1, updates);
    }

    [Theory]
    [InlineData((PlayerId)0)]
    [InlineData((PlayerId)3)]
    public void UndefinedPlayersCannotSubmit(PlayerId player)
    {
        var session = new LocalGameSession(CreateMap(First, Second));
        GameSessionSnapshot initial = session.Current;

        Assert.Equal(SessionCommandResult.InvalidPlayer,
            session.SubmitOpeningSelection(new OpeningSelection(player, First, initial.RoundId)));

        Assert.Same(initial, session.Current);
        Assert.Throws<ArgumentOutOfRangeException>("player", () => initial.HasSubmitted(player));
    }

    [Fact]
    public void ResolvedRoundsAndOccupiedStartingPositionsRejectNewCommands()
    {
        var session = new LocalGameSession(CreateMap(First, Second, Third, Fourth, Fifth));
        long oldRound = session.Current.RoundId;
        Resolve(session, First, Second);
        Resolve(session, Third, Third);
        GameSessionSnapshot current = session.Current;

        Assert.Equal(SessionCommandResult.StaleRound,
            session.SubmitOpeningSelection(new OpeningSelection(PlayerId.PlayerOne, Fourth, oldRound)));
        Assert.Equal(SessionCommandResult.WrongPhase, session.RestartDraft(current.RoundId));
        // Neither a friendly upgrade nor an enemy capture is legal in the opening.
        foreach (BoardPosition occupied in new[] { First, Second, Third })
        {
            Assert.Equal(SessionCommandResult.IneligiblePosition,
                session.SubmitOpeningSelection(new OpeningSelection(PlayerId.PlayerOne, occupied, current.RoundId)));
            Assert.Equal(SessionCommandResult.IneligiblePosition,
                session.SubmitOpeningSelection(new OpeningSelection(PlayerId.PlayerTwo, occupied, current.RoundId)));
        }

        Assert.Same(current, session.Current);
        Assert.Equal(new[] { Fourth, Fifth }, current.EligiblePositions);
        Assert.Equal(2, current.ResolvedRounds);
        AssertKnightCounts(current.Board, 1);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(1, 5)]
    [InlineData(4, 3)]
    public void MixedDraftMaintainsEqualCountsThroughoutAndEntersMain(int width, int height)
    {
        var terrain = new TerrainType[height, width];
        var positions = new List<BoardPosition>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                terrain[y, x] = TerrainType.Playable;
                positions.Add(new BoardPosition(x, y));
            }
        }

        BoardPosition[] starts = positions.Take(4).Append(positions[^1]).ToArray();
        var session = new LocalGameSession(new MapDefinition(width, height, terrain, starts));
        Assert.Equal(width, session.Current.Board.Width);
        Assert.Equal(height, session.Current.Board.Height);
        AssertKnightCounts(session.Current.Board, 0);

        Resolve(session, starts[0], starts[0]);
        AssertKnightCounts(session.Current.Board, 0);
        Assert.Equal(MatchPhase.Opening, session.Current.Phase);
        Resolve(session, starts[1], starts[2]);
        AssertKnightCounts(session.Current.Board, 1);
        Assert.Equal(MatchPhase.Opening, session.Current.Phase);
        Resolve(session, starts[3], starts[4]);
        AssertKnightCounts(session.Current.Board, 2);
        Assert.Equal(3, session.Current.ResolvedRounds);
        Assert.Equal(MatchPhase.Main, session.Current.Phase);
        Assert.Empty(session.Current.EligiblePositions);
        Assert.Equal(PlayerId.PlayerTwo, session.Current.Board.GetCell(starts[4]).Owner);
        Assert.Equal(SessionCommandResult.WrongPhase, session.RestartDraft(session.Current.RoundId));
        Assert.Equal(SessionCommandResult.WrongPhase,
            session.SubmitOpeningSelection(new OpeningSelection(PlayerId.PlayerOne, starts[0], session.Current.RoundId)));
    }

    [Fact]
    public void AllCollisionDraftCanRestartFromItsOriginalMapAndThenReachMain()
    {
        var session = new LocalGameSession(CreateMap(First, Second, Third));
        GameSessionSnapshot initial = session.Current;
        var updates = new List<GameSessionSnapshot>();
        session.StateChanged += updates.Add;
        foreach (BoardPosition start in initial.EligiblePositions)
        {
            Assert.Equal(MatchPhase.Opening, session.Current.Phase);
            Resolve(session, start, start);
            AssertKnightCounts(session.Current.Board, 0);
            Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, 2), session.Current.Board.GetCell(start));
        }

        GameSessionSnapshot exhausted = session.Current;
        Assert.Equal(MatchPhase.OpeningRestartRequired, exhausted.Phase);
        Assert.Empty(exhausted.EligiblePositions);
        Assert.Equal(3, exhausted.ResolvedRounds);
        Assert.Equal(SessionCommandResult.WrongPhase,
            session.SubmitOpeningSelection(new OpeningSelection(PlayerId.PlayerOne, First, exhausted.RoundId)));
        Assert.Equal(SessionCommandResult.StaleRound, session.RestartDraft(initial.RoundId));
        Assert.Equal(6, updates.Count);

        Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(exhausted.RoundId));

        GameSessionSnapshot restarted = session.Current;
        Assert.Equal(7, updates.Count);
        Assert.Same(restarted, updates[^1]);
        Assert.Equal(MatchPhase.Opening, restarted.Phase);
        Assert.Equal(0, restarted.ResolvedRounds);
        Assert.Equal(initial.EligiblePositions, restarted.EligiblePositions);
        Assert.Equal(Cells(initial.Board), Cells(restarted.Board));
        Assert.NotSame(initial.Board, restarted.Board);
        Assert.False(restarted.HasSubmitted(PlayerId.PlayerOne));
        Assert.False(restarted.HasSubmitted(PlayerId.PlayerTwo));
        Assert.Equal(PieceType.Grave, exhausted.Board.GetCell(First).Piece);
        Assert.True(restarted.RoundId > exhausted.RoundId);
        Assert.Equal(SessionCommandResult.StaleRound,
            session.SubmitOpeningSelection(new OpeningSelection(PlayerId.PlayerTwo, First, initial.RoundId)));
        Assert.Equal(SessionCommandResult.StaleRound, session.RestartDraft(exhausted.RoundId));
        Assert.Same(restarted, session.Current);

        Submit(session, PlayerId.PlayerTwo, First);
        Assert.Same(restarted.Board, session.Current.Board);
        Assert.Equal(0, session.Current.ResolvedRounds);
        Submit(session, PlayerId.PlayerOne, Second);
        Assert.Equal(MatchPhase.Opening, session.Current.Phase);
        Resolve(session, Third, Third);
        Assert.Equal(MatchPhase.Main, session.Current.Phase);
        AssertKnightCounts(session.Current.Board, 1);
        Assert.Equal(PlayerId.PlayerTwo, session.Current.Board.GetCell(First).Owner);
        Assert.Equal(PlayerId.PlayerOne, session.Current.Board.GetCell(Second).Owner);
    }

    [Fact]
    public void TwoStartingPositionsCanReachMainInOneRound()
    {
        var session = new LocalGameSession(CreateMap(First, Second));
        Assert.Equal(MatchPhase.Opening, session.Current.Phase);

        Resolve(session, First, Second);

        Assert.Equal(MatchPhase.Main, session.Current.Phase);
        Assert.Empty(session.Current.EligiblePositions);
        Assert.Equal(1, session.Current.ResolvedRounds);
        AssertKnightCounts(session.Current.Board, 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MapsWithFewerThanTwoStartingPositionsCanBeDefinedButCannotStartASession(int startingPositionCount)
    {
        MapDefinition map = CreateMap(startingPositionCount == 0 ? [] : [First]);

        Assert.Equal(startingPositionCount, map.StartingPositions.Count);
        var exception = Assert.Throws<ArgumentException>("map", () => new LocalGameSession(map));
        Assert.Contains("at least two starting positions", exception.Message);
    }

    [Fact]
    public void PublishedEligibilityCannotBeModified()
    {
        var session = new LocalGameSession(CreateMap(First, Second));
        var eligible = Assert.IsAssignableFrom<IList<BoardPosition>>(session.Current.EligiblePositions);

        Assert.Throws<NotSupportedException>(() => eligible[0] = Second);
        Assert.Equal(new[] { First, Second }, session.Current.EligiblePositions);
    }

    private static MapDefinition CreateMap(params BoardPosition[] starts)
    {
        const TerrainType ground = TerrainType.Playable;
        TerrainType[,] terrain = { { ground, ground, TerrainType.Inaccessible, ground },
            { ground, ground, ground, ground }, { ground, ground, ground, ground } };
        return new MapDefinition(4, 3, terrain, starts);
    }

    private static void Submit(IGameSession session, PlayerId player, BoardPosition position)
    {
        Assert.Equal(SessionCommandResult.Accepted,
            session.SubmitOpeningSelection(new OpeningSelection(player, position, session.Current.RoundId)));
    }

    private static void Resolve(IGameSession session, BoardPosition playerOne, BoardPosition playerTwo)
    {
        long round = session.Current.RoundId;
        Submit(session, PlayerId.PlayerOne, playerOne);
        Submit(session, PlayerId.PlayerTwo, playerTwo);
        Assert.True(session.Current.RoundId > round);
    }

    private static void AssertKnightCounts(BoardState board, int expectedPerPlayer)
    {
        CellState[] knights = Cells(board).Where(cell => cell.Piece == PieceType.Knight).ToArray();
        Assert.Equal(expectedPerPlayer, knights.Count(cell => cell.Owner == PlayerId.PlayerOne));
        Assert.Equal(expectedPerPlayer, knights.Count(cell => cell.Owner == PlayerId.PlayerTwo));
    }

    private static CellState[] Cells(BoardState board) =>
        (from y in Enumerable.Range(0, board.Height)
         from x in Enumerable.Range(0, board.Width)
         select board.GetCell(new BoardPosition(x, y))).ToArray();
}
