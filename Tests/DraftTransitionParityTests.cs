using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class DraftTransitionParityTests
{
    [Theory]
    [InlineData(RuleSet.Classic, false)]
    [InlineData(RuleSet.Classic, true)]
    [InlineData(RuleSet.KnightSupport, false)]
    [InlineData(RuleSet.KnightSupport, true)]
    public void HypotheticalAndPublishedTransitionsAgreeThroughDraftAndRestart(RuleSet rules, bool reverse)
    {
        var a = new BoardPosition(0, 0);
        var b = new BoardPosition(1, 0);
        var c = new BoardPosition(2, 0);
        var d = new BoardPosition(3, 0);
        var e = new BoardPosition(4, 0);
        var terrain = new TerrainType[3, 5];
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 5; x++) terrain[y, x] = TerrainType.Playable;
        var map = new MapDefinition(5, 3, terrain, [a, b, c, d, e]);
        var session = new LocalGameSession(map, rules);
        var initial = session.Current;
        var events = new List<GameSessionSnapshot>();
        session.StateChanged += events.Add;

        Resolve(a, a, 2, MatchPhase.Opening);
        Resolve(b, b, 3, MatchPhase.Opening);
        Resolve(c, c, 4, MatchPhase.Opening);
        Resolve(d, d, 6, MatchPhase.OpeningRestartRequired);
        Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(6));
        Assert.Equal(7, session.Current.RoundId);
        Assert.Equal(0, session.Current.ResolvedRounds);
        Assert.Equal(initial.Board.Cells(), session.Current.Board.Cells());
        Resolve(a, e, 8, MatchPhase.Opening);
        Resolve(b, d, 10, MatchPhase.Main);
        Assert.Equal(PieceType.Grave, session.Current.Board.GetCell(c).Piece);
        Assert.Equal(2, session.Current.Board.GetCell(c).GroundHealth);
        Assert.Equal(5, initial.EligiblePositions.Count);
        Assert.All(initial.Board.Cells(), cell => Assert.Equal(PieceType.None, cell.Piece));

        var divided = new[,] { { TerrainType.Playable, TerrainType.Inaccessible, TerrainType.Playable } };
        session = new LocalGameSession(new MapDefinition(3, 1, divided, [a, c]), rules);
        session.StateChanged += events.Add;
        Resolve(a, c, 2, MatchPhase.Finished);
        Assert.Null(session.Current.FinalResult!.Value.Winner);
        Assert.Equal((1, 1), (session.Current.PlayerOneScore, session.Current.PlayerTwoScore));

        void Resolve(BoardPosition one, BoardPosition two, long nextRound, MatchPhase phase)
        {
            var before = session.Current;
            var preview = before.PreviewDraft(one, two);
            int count = events.Count;
            var oneCommand = new OpeningSelection(PlayerId.PlayerOne, one, before.RoundId);
            var twoCommand = new OpeningSelection(PlayerId.PlayerTwo, two, before.RoundId);
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(reverse ? twoCommand : oneCommand));
            var pending = session.Current;
            Assert.Same(before.Board, pending.Board);
            Assert.Equal(before.RoundId, pending.RoundId);
            Assert.False(before.HasSubmitted(PlayerId.PlayerOne));
            Assert.False(before.HasSubmitted(PlayerId.PlayerTwo));
            Assert.True(pending.HasSubmitted(reverse ? PlayerId.PlayerTwo : PlayerId.PlayerOne));
            Assert.Equal(SessionCommandResult.Accepted, session.SubmitOpeningSelection(reverse ? oneCommand : twoCommand));

            var actual = session.Current;
            Assert.Equal(count + 2, events.Count);
            Assert.Same(actual, events[^1]);
            Assert.Equal((nextRound, phase), (actual.RoundId, actual.Phase));
            Assert.Equal((preview.RoundId, preview.Phase, preview.ResolvedRounds, preview.ResolvedMainTurns, preview.RuleSet),
                (actual.RoundId, actual.Phase, actual.ResolvedRounds, actual.ResolvedMainTurns, actual.RuleSet));
            Assert.Equal(preview.Board.Cells(), actual.Board.Cells());
            Assert.Equal(preview.EligiblePositions, actual.EligiblePositions);
            Assert.Equal(preview.FinalResult, actual.FinalResult);
            Assert.Equal((preview.PlayerOneScore, preview.PlayerTwoScore), (actual.PlayerOneScore, actual.PlayerTwoScore));
            for (int y = 0; y < actual.Board.Height; y++)
            for (int x = 0; x < actual.Board.Width; x++)
                Assert.Equal(preview.Territory?.GetControl(new(x, y)), actual.Territory?.GetControl(new(x, y)));
            foreach (var side in new[] { PlayerId.PlayerOne, PlayerId.PlayerTwo })
            {
                Assert.True(preview.GetMainPhaseLegalSelections(side).SetEquals(actual.GetMainPhaseLegalSelections(side)));
                Assert.False(actual.HasSubmitted(side));
            }
        }
    }
}
