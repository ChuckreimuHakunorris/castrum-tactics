using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class KnightSupportTests
{
    [Fact]
    public void ExtendingCastleSurvivalSupportDoesNotExtendPlacementOrChangeTerritory()
    {
        var rules = new RuleSetDefinition(PieceType.Castle, true, 1, 1, 1, 2);
        var board = ResolvedBoard.Parse(".......", ".A1....", "..B....");
        var official = MainPhaseState.Evaluate(board, RuleSet.KnightSupport);
        var experimental = MainPhaseState.Evaluate(board, RuleSet.KnightSupport, rules);
        Assert.True(official.GetLegalSelections(One).SetEquals(experimental.GetLegalSelections(One)));
        var invasion = new MainPhaseSelection(One, new(3, 1));
        var capture = new MainPhaseSelection(Two, new(2, 1));
        Assert.Equal(PieceType.Grave, official.Advance(invasion, capture).Board.GetCell(invasion.Position).Piece);
        Assert.Equal(One, experimental.Advance(invasion, capture).Board.GetCell(invasion.Position).Owner);

        var onlyCastle = ResolvedBoard.Parse(".......", ".A.....", "..B....");
        var before = MainPhaseState.Evaluate(onlyCastle, RuleSet.KnightSupport, rules);
        Assert.Equal(TerritoryControl.Contested, before.Territory.GetControl(invasion.Position));
        Assert.DoesNotContain(invasion.Position, before.GetLegalSelections(One));
        Assert.Equal(2, Enum.GetValues<RuleSet>().Length);
    }

    private const PlayerId One = PlayerId.PlayerOne;
    private const PlayerId Two = PlayerId.PlayerTwo;

    [Theory]
    [InlineData(RuleSet.Classic, PieceType.Knight)]
    [InlineData(RuleSet.KnightSupport, PieceType.Castle)]
    public void DraftPlacesStartingPiecesAndAutomaticallyCollidesOnFinalStart(RuleSet rules, PieceType piece)
    {
        BoardPosition[] starts = [new(0, 0), new(2, 0), new(4, 0)];
        const TerrainType playable = TerrainType.Playable;
        var map = new MapDefinition(5, 2, new[,] { { playable, playable, playable, playable, playable },
            { playable, playable, playable, playable, playable } }, starts);
        var session = new LocalGameSession(map, rules);
        long round = session.Current.RoundId;
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(One, starts[0], round)));
        Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Two, starts[2], round)));

        var result = session.Current;
        Assert.Equal(rules, result.RuleSet);
        Assert.Equal(MatchPhase.Main, result.Phase);
        Assert.Equal(2, result.ResolvedRounds);
        Assert.Equal(round + 2, result.RoundId);
        Assert.Equal(new CellState(TerrainType.Playable, piece, One, 3), result.Board.GetCell(starts[0]));
        Assert.Equal(new CellState(TerrainType.Playable, piece, Two, 3), result.Board.GetCell(starts[2]));
        AssertGrave(result.Board, starts[1], 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FirstTurnKnightsSurviveWithOrthogonalOrDiagonalCastleSupport(int row)
    {
        var state = MainPhaseState.Evaluate(ResolvedBoard.Parse("A...B", "....."), RuleSet.KnightSupport);
        var one = new MainPhaseSelection(One, new(1, row));
        var two = new MainPhaseSelection(Two, new(3, row));
        BoardState next = state.ResolveTurn(one, two);
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, One, 3), next.GetCell(one.Position));
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Knight, Two, 3), next.GetCell(two.Position));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedAndImmediatelyFinishedDraftsRetainKnightSupportOnRestart(bool distinct)
    {
        var left = new BoardPosition(0, 0);
        var right = new BoardPosition(1, 0);
        var map = new MapDefinition(2, 1, new[,] { { TerrainType.Playable, TerrainType.Playable } }, [left, right]);
        var session = new LocalGameSession(map, RuleSet.KnightSupport);
        long round = session.Current.RoundId;
        session.SubmitOpeningSelection(new(One, left, round));
        session.SubmitOpeningSelection(new(Two, distinct ? right : left, round));

        Assert.Equal(distinct ? MatchPhase.Finished : MatchPhase.OpeningRestartRequired, session.Current.Phase);
        Assert.Equal(0, session.Current.ResolvedMainTurns);
        if (distinct)
        {
            Assert.Equal(new MainPhaseResult(1, 1), session.Current.FinalResult);
        }
        else
        {
            AssertGrave(session.Current.Board, left, 2);
            AssertGrave(session.Current.Board, right, 2);
        }
        Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(session.Current.RoundId));
        Assert.Equal(RuleSet.KnightSupport, session.Current.RuleSet);
        Assert.Equal(MatchPhase.Opening, session.Current.Phase);
        Assert.Equal(2, session.Current.EligiblePositions.Count);
        Assert.Equal(PieceType.None, session.Current.Board.GetCell(left).Piece);
        Assert.Throws<ArgumentOutOfRangeException>("ruleSet", () => new LocalGameSession(map, (RuleSet)99));
        Assert.Throws<ArgumentOutOfRangeException>("ruleSet", () => MainPhaseState.Evaluate(session.Current.Board, (RuleSet)99));
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(2, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    public void SimultaneousCapturesCanMakeBothNewlyCapturedKnightsUnsupported(int initialHealth, int finalHealth)
    {
        var left = new BoardPosition(2, 0);
        var right = new BoardPosition(3, 0);
        BoardState board = ResolvedBoard.Parse("A1122B").AtHealth(left, initialHealth).AtHealth(right, initialHealth);
        var state = MainPhaseState.Evaluate(board, RuleSet.KnightSupport);
        var one = new MainPhaseSelection(One, right);
        var two = new MainPhaseSelection(Two, left);

        BoardState next = state.ResolveTurn(one, two);
        Assert.Equal(next.Cells(), state.ResolveTurn(two, one).Cells());
        AssertGrave(next, left, finalHealth);
        AssertGrave(next, right, finalHealth);
        Assert.Equal(PieceType.Knight, next.GetCell(new(1, 0)).Piece);
        Assert.Equal(PieceType.Knight, next.GetCell(new(4, 0)).Piece);
        var scored = MainPhaseState.Evaluate(next, state.RuleSet);
        Assert.Equal(TerritoryControl.Neutral, scored.Territory.GetControl(left));
        Assert.Equal(TerritoryControl.Neutral, scored.Territory.GetControl(right));
        Assert.Equal(new MainPhaseResult(2, 2), scored.Result);
        if (initialHealth > 1)
        {
            var ordinary = MainPhaseState.Evaluate(MainPhaseState.Evaluate(board).ResolveTurn(one, two));
            Assert.False(ordinary.IsFinished);
            Assert.Equal((1, 1), (ordinary.Territory.PlayerOneScore, ordinary.Territory.PlayerTwoScore));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunawayKnightPursuitRequiresSurvivingSupport(bool supported)
    {
        BoardState board = ResolvedBoard.Parse(supported ? "...1..." : ".......", "A11....", "..B....");
        var captured = new BoardPosition(2, 1);
        var invasion = new BoardPosition(3, 1);
        var pursuit = new BoardPosition(4, 1);
        var attack = new MainPhaseSelection(One, invasion);
        var defend = new MainPhaseSelection(Two, captured);
        var classic = MainPhaseState.Evaluate(board).ResolveTurn(attack, defend);
        Assert.Equal(One, classic.GetCell(invasion).Owner);
        Assert.Contains(pursuit, MainPhaseState.Evaluate(classic).GetLegalSelections(One));

        var support = MainPhaseState.Evaluate(board, RuleSet.KnightSupport);
        BoardState next = support.ResolveTurn(attack, defend);
        Assert.Equal(next.Cells(), support.ResolveTurn(defend, attack).Cells());
        Assert.Equal(Two, next.GetCell(captured).Owner); // The defender has castle support.
        var legal = MainPhaseState.Evaluate(next, support.RuleSet).GetLegalSelections(One);
        if (supported)
        {
            // The new knight and its neighbor support one another after their original supporter is captured.
            Assert.Equal(One, next.GetCell(invasion).Owner);
            Assert.Equal(One, next.GetCell(new(3, 0)).Owner);
            Assert.Contains(pursuit, legal);
        }
        else
        {
            AssertGrave(next, invasion, 2);
            Assert.DoesNotContain(invasion, legal);
            Assert.DoesNotContain(pursuit, legal);
        }
    }

    [Fact]
    public void CollisionTriggersWholeBoardEliminationAndClampsGroundHealth()
    {
        var collision = new BoardPosition(3, 0);
        var unsupportedOne = new BoardPosition(6, 0);
        var unsupportedTwo = new BoardPosition(6, 1);
        BoardState board = ResolvedBoard.Parse("A.1.2#1", "..1.2#2").AtHealth(unsupportedOne, 0);
        var state = MainPhaseState.Evaluate(board, RuleSet.KnightSupport);
        BoardState next = state.ResolveTurn(new(One, collision), new(Two, collision));
        AssertGrave(next, collision, 2);
        AssertGrave(next, unsupportedOne, 0);
        AssertGrave(next, unsupportedTwo, 2);
        Assert.Equal(PieceType.Castle, next.GetCell(new(0, 0)).Piece);
        Assert.Equal(PieceType.Knight, next.GetCell(new(2, 0)).Piece);
        Assert.Equal(PieceType.Knight, next.GetCell(new(4, 0)).Piece);
    }

    [Fact]
    public void SessionPublishesTheSameEnforcedBoardInBothSubmissionOrders()
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("pocket-meadow").Definition;
        var forward = new LocalGameSession(map, RuleSet.KnightSupport);
        var reverse = new LocalGameSession(map, RuleSet.KnightSupport);
        var invasion = new BoardPosition(2, 0);
        var captured = new BoardPosition(1, 0);
        foreach (var session in new[] { forward, reverse })
        {
            long opening = session.Current.RoundId;
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(One, new(0, 0), opening)));
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(new(Two, new(2, 2), opening)));
            Submit(session, One, captured);
            Submit(session, Two, new(2, 1));
        }

        Submit(forward, One, invasion);
        Submit(forward, Two, captured);
        BoardState before = reverse.Current.Board;
        var updates = new List<GameSessionSnapshot>();
        reverse.StateChanged += updates.Add;
        Submit(reverse, Two, captured);
        Submit(reverse, One, invasion);

        Assert.Collection(updates, pending => Assert.Same(before, pending.Board),
            resolved => AssertGrave(resolved.Board, invasion, 2));
        Assert.Equal(forward.Current.Board.Cells(), reverse.Current.Board.Cells());
        Assert.True(forward.Current.GetMainPhaseLegalSelections(One).SetEquals(reverse.Current.GetMainPhaseLegalSelections(One)));
        Assert.True(forward.Current.GetMainPhaseLegalSelections(Two).SetEquals(reverse.Current.GetMainPhaseLegalSelections(Two)));
        Assert.Equal((forward.Current.PlayerOneScore, forward.Current.PlayerTwoScore),
            (reverse.Current.PlayerOneScore, reverse.Current.PlayerTwoScore));

        static void Submit(LocalGameSession session, PlayerId player, BoardPosition position) =>
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitMainPhaseSelection(new(player, position, session.Current.RoundId)));
    }

    private static void AssertGrave(BoardState board, BoardPosition position, int health) =>
        Assert.Equal(new CellState(TerrainType.Playable, PieceType.Grave, null, health), board.GetCell(position));
}
