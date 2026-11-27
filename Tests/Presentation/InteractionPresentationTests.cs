using System.Reflection;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class InteractionPresentationTests
{
    [Theory]
    [InlineData(false, false, true, 0)]
    [InlineData(true, false, true, 1)]
    [InlineData(false, true, true, 2)]
    [InlineData(true, true, true, 3)]
    [InlineData(false, true, false, 0)]
    [InlineData(true, true, false, 1)]
    public void ClassifiesPublicTargetsAndHidesOnlyOpposingInformation(
        bool active, bool opposing, bool visible, int expected)
    {
        Assert.Equal(expected, (int)MatchPresentation.Classify(active, opposing, visible));
    }

    [Theory]
    [InlineData(RuleSet.Classic)]
    [InlineData(RuleSet.KnightSupport)]
    public void TargetSetsFollowPerspectiveAndStopOutsidePlayablePhases(RuleSet rules)
    {
        var session = new LocalGameSession(PredefinedMapCatalog.BuiltIn.Get("open-meadow").Definition, rules);
        var draft = session.Current;
        var openingPair = MatchPresentation.Targets(draft, PlayerId.PlayerTwo);
        Assert.Same(draft.EligiblePositions, openingPair.Active);
        Assert.Same(openingPair.Active, openingPair.Opposing);
        Assert.Equal(default, TerritoryFill.From(draft.Board, draft.Territory));

        var after = draft.PreviewDraft(draft.EligiblePositions[0], draft.EligiblePositions[1]);
        Assert.Equal(MatchPhase.Main, after.Phase);
        var red = MatchPresentation.Targets(after, PlayerId.PlayerOne);
        var blue = MatchPresentation.Targets(after, PlayerId.PlayerTwo);
        Assert.NotEmpty(red.Active);
        Assert.Same(red.Active, blue.Opposing);
        Assert.Same(red.Opposing, blue.Active);

        var finished = SplitDraft(rules);
        Assert.Equal(MatchPhase.Finished, finished.Phase);
        Assert.Equal(new TerritoryFill(1, 1, 2), TerritoryFill.From(finished.Board, finished.Territory));
        Assert.Empty(MatchPresentation.Targets(finished, PlayerId.PlayerOne).Active);
        Assert.Empty(MatchPresentation.Targets(finished, PlayerId.PlayerOne).Opposing);

        var exhausted = draft.PreviewDraft(draft.EligiblePositions[0], draft.EligiblePositions[0]);
        while (exhausted.Phase == MatchPhase.Opening)
        {
            var shared = exhausted.EligiblePositions[0];
            exhausted = exhausted.PreviewDraft(shared, shared);
        }
        Assert.Equal(MatchPhase.OpeningRestartRequired, exhausted.Phase);
        Assert.Equal(default, TerritoryFill.From(exhausted.Board, exhausted.Territory));
        Assert.Empty(MatchPresentation.Targets(exhausted, PlayerId.PlayerTwo).Active);
    }

    [Theory]
    [InlineData("R...b#..", 1, 0, 5)]
    [InlineData("R...B#b.", 1, 3, 7)]
    [InlineData("r...b#..", 0, 0, 5)]
    [InlineData("Rr...#..", 5, 0, 5)]
    [InlineData("RR#BBB#..", 2, 3, 5)]
    [InlineData("..#123#.", 0, 0, 0)]
    [InlineData("R...b#123#.", 1, 0, 5)]
    public void CountsSecuredAndContestedWithoutGravesRockOrNeutralRegions(
        string row, int one, int two, int claimable)
    {
        var board = CreateBoard(row);
        var analysis = TerritoryAnalyzer.Analyze(board);
        var fill = TerritoryFill.From(board, analysis);

        Assert.Equal(new TerritoryFill(one, two, claimable), fill);
        Assert.Equal(claimable == 0 ? 0 : (float)one / claimable, fill.OneFraction);
        Assert.Equal(claimable == 0 ? 0 : (float)two / claimable, fill.TwoFraction);
    }

    [Fact]
    public void ContestedInteriorCountsBeyondLegalReachAndGravesCanIncreaseFillWithoutScoreChange()
    {
        var before = CreateBoard("R...b");
        var after = CreateBoard("R..2b");
        var beforeTerritory = TerritoryAnalyzer.Analyze(before);
        var beforeFill = TerritoryFill.From(before, beforeTerritory);
        var afterFill = TerritoryFill.From(after, TerritoryAnalyzer.Analyze(after));
        var middle = new BoardPosition(2, 0);
        var main = MainPhaseState.Evaluate(before, RuleSet.Classic);
        Assert.Equal(TerritoryControl.Contested, beforeTerritory.GetControl(middle));
        Assert.DoesNotContain(middle, main.GetLegalSelections(PlayerId.PlayerOne));
        Assert.DoesNotContain(middle, main.GetLegalSelections(PlayerId.PlayerTwo));
        Assert.Equal(new TerritoryFill(1, 0, 5), beforeFill);

        // A grave on row 0 removes one contested square. Row 1 keeps the component connected.
        before = CreateBoard("R...b", ".....");
        after = CreateBoard("R..2b", ".....");
        beforeFill = TerritoryFill.From(before, TerritoryAnalyzer.Analyze(before));
        afterFill = TerritoryFill.From(after, TerritoryAnalyzer.Analyze(after));
        Assert.Equal(new TerritoryFill(1, 0, 10), beforeFill);
        Assert.Equal(new TerritoryFill(1, 0, 9), afterFill);
        Assert.True(afterFill.OneFraction > beforeFill.OneFraction);
    }

    private static GameSessionSnapshot SplitDraft(RuleSet rules)
    {
        var terrain = new[,] { { TerrainType.Playable, TerrainType.Inaccessible, TerrainType.Playable } };
        var left = new BoardPosition(0, 0);
        var right = new BoardPosition(2, 0);
        var session = new LocalGameSession(new MapDefinition(3, 1, terrain, [left, right]), rules);
        return session.Current.PreviewDraft(left, right);
    }

    private static BoardState CreateBoard(params string[] rows)
    {
        var ground = new TerrainType[rows.Length, rows[0].Length];
        var pieces = new List<(BoardPosition, CellState)>();
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[0].Length; x++)
            {
                char mark = rows[y][x];
                if (mark == '#') continue;
                ground[y, x] = TerrainType.Playable;
                if (mark == '.') continue;
                PieceType piece = mark switch
                {
                    'R' or 'B' => PieceType.Castle,
                    'r' or 'b' => PieceType.Knight,
                    _ => PieceType.Grave,
                };
                PlayerId? owner = mark switch
                {
                    'R' or 'r' => PlayerId.PlayerOne,
                    'B' or 'b' => PlayerId.PlayerTwo,
                    _ => null,
                };
                int health = piece == PieceType.Grave ? mark - '1' : 3;
                pieces.Add((new BoardPosition(x, y), new CellState(TerrainType.Playable, piece, owner, health)));
            }

        var map = new MapDefinition(rows[0].Length, rows.Length, ground, []);
        // Keep synthetic resolved fixtures out of the production session API.
        var replace = typeof(BoardState).GetMethod("WithResolvedCells", BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(IReadOnlyList<(BoardPosition, CellState)>)], null)!;
        return (BoardState)replace.Invoke(new BoardState(map), [pieces])!;
    }
}
