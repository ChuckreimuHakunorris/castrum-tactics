using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class ResolvedTurnTests
{
    [Theory]
    [InlineData(RuleSet.Classic, false)]
    [InlineData(RuleSet.Classic, true)]
    [InlineData(RuleSet.KnightSupport, false)]
    [InlineData(RuleSet.KnightSupport, true)]
    public void DraftPublishesOneAtomicRecordWithRevisionAndForcedCollision(RuleSet rules, bool reverse)
    {
        var first = new BoardPosition(0, 0);
        var revised = new BoardPosition(1, 1);
        var second = new BoardPosition(3, 3);
        var map = PlayableMap(4, first, revised, second);
        IGameSession session = new LocalGameSession(map, rules);
        var published = new List<ResolvedTurn?>();
        session.StateChanged += _ => published.Add(session.LatestResolution);
        long round = session.Current.RoundId;
        var revisingPlayer = reverse ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
        var other = reverse ? PlayerId.PlayerOne : PlayerId.PlayerTwo;

        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(revisingPlayer, first, round)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(revisingPlayer, revised, round)));
        Assert.Equal(3, session.Current.EligiblePositions.Count);
        Assert.Null(session.LatestResolution);
        var preview = reverse ? session.Current.PreviewDraft(second, revised) : session.Current.PreviewDraft(revised, second);
        Assert.Null(session.LatestResolution);
        Assert.Equal(2, published.Count);

        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(other, second, round)));
        var turn = Assert.IsType<ResolvedTurn>(session.LatestResolution);
        Assert.Equal(new ResolvedTurn?[] { null, null, turn }, published);
        Assert.Equal(MatchPhase.Opening, turn.Phase);
        Assert.Equal(round, turn.RoundId);
        Assert.Equal(round + 2, turn.After.RoundId);
        Assert.Equal(reverse ? second : revised, turn.PlayerOne);
        Assert.Equal(reverse ? revised : second, turn.PlayerTwo);
        Assert.Equal(first, turn.AutomaticDraftCollision);
        Assert.Null(turn.MainBeforeSupport);
        Assert.Equal(PieceType.None, turn.Before.Board.GetCell(first).Piece);
        Assert.Equal(PieceType.Grave, turn.After.Board.GetCell(first).Piece);
        Assert.True(preview.GetMainPhaseLegalSelections(PlayerId.PlayerOne)
            .SetEquals(turn.After.GetMainPhaseLegalSelections(PlayerId.PlayerOne)));
    }

    [Fact]
    public void FailedDraftRecordsCollisionAndRestartClearsIt()
    {
        var target = new BoardPosition(0, 0);
        var other = new BoardPosition(2, 2);
        var map = PlayableMap(3, target, other);
        var session = new LocalGameSession(map, RuleSet.KnightSupport);
        SubmitPair(session, target, target);
        var turn = Assert.IsType<ResolvedTurn>(session.LatestResolution);
        Assert.Equal(MatchPhase.OpeningRestartRequired, turn.After.Phase);
        Assert.Equal(target, turn.PlayerTwo);
        Assert.Equal(other, turn.AutomaticDraftCollision);
        Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(session.Current.RoundId));
        Assert.Null(session.LatestResolution);
        Assert.Equal(PieceType.Grave, turn.After.Board.GetCell(other).Piece);
        Assert.Equal(PieceType.None, session.Current.Board.GetCell(other).Piece);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MainPreservesNewPlacementBeforeSupportAndRealCapture(bool reverse)
    {
        var session = new LocalGameSession(PredefinedMapCatalog.BuiltIn.Get("open-meadow").Definition, RuleSet.KnightSupport);
        SubmitPair(session, new(0, 0), new(2, 2), reverse);
        SubmitPair(session, new(1, 0), new(2, 1), reverse);
        var previous = session.LatestResolution;
        var placed = new BoardPosition(2, 0);
        var captured = new BoardPosition(1, 0);
        long round = session.Current.RoundId;
        var one = new MainPhaseCommand(PlayerId.PlayerOne, placed, round);
        var two = new MainPhaseCommand(PlayerId.PlayerTwo, captured, round);
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(reverse ? two : one));
        Assert.Same(previous, session.LatestResolution);
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(reverse ? one : two));

        var turn = Assert.IsType<ResolvedTurn>(session.LatestResolution);
        Assert.Equal(MatchPhase.Main, turn.Phase);
        Assert.Equal(placed, turn.PlayerOne);
        Assert.Equal(captured, turn.PlayerTwo);
        var ordinary = Assert.IsType<BoardState>(turn.MainBeforeSupport);
        Assert.Equal(PieceType.None, turn.Before.Board.GetCell(placed).Piece);
        Assert.Equal(PlayerId.PlayerOne, ordinary.GetCell(placed).Owner);
        Assert.Equal(PieceType.Grave, turn.After.Board.GetCell(placed).Piece);
        Assert.Equal(ordinary.GetCell(placed).GroundHealth - 1, turn.After.Board.GetCell(placed).GroundHealth);
        Assert.Equal(PlayerId.PlayerOne, turn.Before.Board.GetCell(captured).Owner);
        Assert.Equal(PlayerId.PlayerTwo, ordinary.GetCell(captured).Owner);
        Assert.Equal(turn.Before.Board.GetCell(captured).GroundHealth - 1, ordinary.GetCell(captured).GroundHealth);
        Assert.Same(session.Current, turn.After);
    }

    [Fact]
    public void LastDraftPairCanPublishAFinishedMatch()
    {
        var terrain = new[,] { { TerrainType.Playable, TerrainType.Inaccessible, TerrainType.Playable } };
        var session = new LocalGameSession(new MapDefinition(3, 1, terrain, [new(0, 0), new(2, 0)]), RuleSet.KnightSupport);
        SubmitPair(session, new(0, 0), new(2, 0));
        var turn = Assert.IsType<ResolvedTurn>(session.LatestResolution);
        Assert.Equal(MatchPhase.Finished, turn.After.Phase);
        Assert.NotNull(turn.After.FinalResult);
        Assert.Null(turn.AutomaticDraftCollision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClassicOrdinaryOutcomesShowDirectCollisionOrUpgrade(bool collide)
    {
        var session = new LocalGameSession(PredefinedMapCatalog.BuiltIn.Get("open-meadow").Definition);
        SubmitPair(session, new(0, 0), new(2, 2));
        var center = new BoardPosition(1, 1);
        SubmitPair(session, collide ? center : new(0, 0), collide ? center : new(2, 2));
        var turn = Assert.IsType<ResolvedTurn>(session.LatestResolution);
        Assert.Same(turn.After.Board, turn.MainBeforeSupport);
        var cell = turn.After.Board.GetCell(turn.PlayerOne);
        Assert.Equal(collide ? PieceType.Grave : PieceType.Castle, cell.Piece);
        Assert.Equal(collide ? 2 : 3, cell.GroundHealth);
    }

    private static MapDefinition PlayableMap(int size, params BoardPosition[] starts)
    {
        var terrain = new TerrainType[size, size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                terrain[y, x] = TerrainType.Playable;
        return new MapDefinition(size, size, terrain, starts);
    }

    private static void SubmitPair(LocalGameSession session, BoardPosition first, BoardPosition second, bool reverse = false)
    {
        var snapshot = session.Current;
        Assert.Equal(SessionCommandResult.Accepted, reverse ? Submit(PlayerId.PlayerTwo, second) : Submit(PlayerId.PlayerOne, first));
        Assert.Equal(SessionCommandResult.Accepted, reverse ? Submit(PlayerId.PlayerOne, first) : Submit(PlayerId.PlayerTwo, second));

        SessionCommandResult Submit(PlayerId player, BoardPosition position) => snapshot.Phase == MatchPhase.Opening
            ? session.SubmitOpeningSelection(new(player, position, snapshot.RoundId))
            : session.SubmitMainPhaseSelection(new(player, position, snapshot.RoundId));
    }
}
