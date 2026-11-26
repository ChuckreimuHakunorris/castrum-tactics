using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Domain.Tests;

public sealed class DraftTransitionParityTests
{
    [Theory]
    [InlineData(RuleSet.Classic, "B6A1427921701F00069059AEC9E09BBAA95CE50B29139A02E35E41BEA98ADF68")]
    [InlineData(RuleSet.KnightSupport, "735EC0E4C90357865881C2445F3B6F6932792581035BD01EF1A8C679787E4E73")]
    public void SeededPublicSessionHistoryMatchesIntegrated8E(RuleSet rules, string reference)
    {
        var map = PredefinedMapCatalog.BuiltIn.Get("honeystone-green").Definition;
        var session = new LocalGameSession(map, rules);
        var random = new Random(3951);
        // Pin published snapshots from integrated 8E independently of the frozen CPU, which shares the live Domain assembly.
        var history = new System.Text.StringBuilder();
        var sides = new[] { PlayerId.PlayerOne, PlayerId.PlayerTwo };
        var coordinates = from y in Enumerable.Range(0, map.Height)
                          from x in Enumerable.Range(0, map.Width)
                          select new BoardPosition(x, y);
        session.StateChanged += Record;
        Record(session.Current);

        while (session.Current.Phase is MatchPhase.Opening or MatchPhase.Main)
        {
            var state = session.Current;
            bool opening = state.Phase == MatchPhase.Opening;
            foreach (var side in sides)
            {
                var legal = opening ? state.EligiblePositions : state.GetMainPhaseLegalSelections(side).AsEnumerable();
                var candidates = legal.OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
                var move = candidates[random.Next(candidates.Length)];
                var result = opening
                    ? session.SubmitOpeningSelection(new(side, move, state.RoundId))
                    : session.SubmitMainPhaseSelection(new(side, move, state.RoundId));
                Assert.Equal(SessionCommandResult.Accepted, result);
            }
        }

        Assert.Equal(MatchPhase.Finished, session.Current.Phase);
        var digest = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(history.ToString()));
        Assert.Equal(reference, Convert.ToHexString(digest));

        void Record(GameSessionSnapshot state)
        {
            history.Append(FormattableString.Invariant(
                $"{state.RoundId}:{state.Phase}:{state.ResolvedRounds}:{state.ResolvedMainTurns}:{state.PlayerOneScore}:{state.PlayerTwoScore}:{state.FinalResult?.Winner}|"));
            foreach (var p in coordinates)
            {
                var cell = state.Board.GetCell(p);
                history.Append(FormattableString.Invariant(
                    $"{cell.Terrain},{cell.GroundHealth},{cell.Piece},{cell.Owner},{state.Territory?.GetControl(p)};"));
            }
            history.Append('|');
            foreach (var p in state.EligiblePositions)
                history.Append(FormattableString.Invariant($"{p.X},{p.Y};"));
            foreach (var side in sides)
            {
                history.Append(state.HasSubmitted(side)).Append('|');
                foreach (var p in state.GetMainPhaseLegalSelections(side).OrderBy(p => p.Y).ThenBy(p => p.X))
                    history.Append(FormattableString.Invariant($"{p.X},{p.Y};"));
                history.Append('|');
            }
        }
    }

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
