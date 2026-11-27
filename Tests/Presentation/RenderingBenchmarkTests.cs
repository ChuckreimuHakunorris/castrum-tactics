using CastrumTactics.Domain;
using CastrumTactics.RenderingBenchmarks;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class RenderingBenchmarkTests
{
    [Theory]
    [InlineData("quick", 2, 4, 1, 3)]
    [InlineData("standard", 8, 20, 3, 9)]
    [InlineData("sustained", 30, 1200, 1, 1)]
    public void ProfilesSupplyVersionedDefaults(string profile, double warmup, double measure, int repeats, int cases)
    {
        var options = RenderBenchmarkOptions.Parse($"{{\"profile\":\"{profile}\"}}");
        Assert.Equal(warmup, options.WarmupSeconds);
        Assert.Equal(measure, options.MeasureSeconds);
        Assert.Equal(repeats, options.Repetitions);
        Assert.Equal(cases, options.Cases!.Length);
        Assert.False(options.ViewportTiming);
        Assert.False(options.CpuDiagnostics);
        Assert.Equal(2, RenderBenchmarkOptions.ScenarioVersion);
        if (profile == "standard") Assert.Contains("damage", options.Cases);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"cases\":[\"small\",\"small\"]}")]
    [InlineData("{\"cases\":[null]}")]
    [InlineData("{\"cases\":[\"unknown\"]}")]
    [InlineData("{\"measureSeconds\":0}")]
    [InlineData("{\"repetitions\":11}")]
    [InlineData("{\"pacing\":\"unknown\"}")]
    public void InvalidWorkloadsAreRejected(string request) =>
        Assert.Throws<InvalidDataException>(() => RenderBenchmarkOptions.Parse(request));

    [Fact]
    public void IndividualMotionOverridesDoNotChangeUnrelatedEffects()
    {
        var options = RenderBenchmarkOptions.Parse("""{"reduceMotion":true,"legalMotion":true,"cases":["targets"],"repetitions":2}""");
        Assert.True(options.AnimateLegal);
        Assert.False(options.AnimateAmbient);
        Assert.False(options.AnimateTube);
        Assert.True(options.ShowOpponent);
        Assert.Equal(2, options.Repetitions);
    }

    [Fact]
    public void CalculatesFpsPercentilesJankAndVarianceFromOnlySuppliedIntervals()
    {
        Assert.Null(FrameSummary.Calculate([]));
        var summary = FrameSummary.Calculate([new(10, 10, null, null, null), new(30, 20, null, null, null),
            new(60, 30, null, null, null), new(100, 40, null, null, null)])!;
        Assert.Equal(4, summary.Samples);
        Assert.Equal(40, summary.AverageFps);
        Assert.Equal(25, summary.MedianMs);
        Assert.Equal(38.5, summary.P95Ms, 6);
        Assert.Equal(39.7, summary.P99Ms, 6);
        Assert.Equal(Math.Sqrt(125), summary.StandardDeviationMs, 6);
        Assert.Equal(75, summary.PercentOver16_67Ms);
        Assert.Equal(25, summary.PercentOver33_33Ms);
        Assert.Equal(3, summary.Over16_67Ms);
        Assert.Equal(1, summary.Over33_33Ms);
    }

    [Theory]
    [InlineData("castles")]
    [InlineData("damage-extreme")]
    [InlineData("targets")]
    public void SyntheticFixturesAreRepeatableAndProvideBothLegalSets(string id)
    {
        var first = RenderBenchmarkFixtures.Create(id);
        var second = RenderBenchmarkFixtures.Create(id);
        Assert.False(first.IsFinished);
        Assert.Equal(12, first.Board.Width);
        Assert.Equal(12, first.Board.Height);
        var own = first.GetLegalSelections(PlayerId.PlayerOne);
        var other = first.GetLegalSelections(PlayerId.PlayerTwo);
        Assert.NotEmpty(own);
        Assert.NotEmpty(other);
        Assert.True(own.SetEquals(second.GetLegalSelections(PlayerId.PlayerOne)));
        if (id == "targets") Assert.True(own.Count(other.Contains) > 40);
        if (id == "castles") Assert.Equal(80, own.Union(other).Count());
        if (id == "damage-extreme") Assert.True(first.Board.GetCell(new BoardPosition(0, 0)).GroundHealth < 3);
    }

    [Fact]
    public void DetailedCpuDiagnosticsCanBeEnabledExplicitly() =>
        Assert.True(RenderBenchmarkOptions.Parse("""{"cpuDiagnostics":true}""").CpuDiagnostics);

    [Fact]
    public void HeavyDamageReplayPreservesLiveGroundAndShowsDamageAcrossFactions()
    {
        var state = RenderBenchmarkFixtures.ReplayHeavyDamage();
        Assert.False(state.IsFinished);
        var cells = from y in Enumerable.Range(0, state.Board.Height)
                    from x in Enumerable.Range(0, state.Board.Width)
                    select state.Board.GetCell(new BoardPosition(x, y));
        Assert.All(cells.Where(cell => cell.IsPlayable), cell => Assert.True(cell.GroundHealth > 0));
        Assert.Contains(cells, cell => cell.Piece == PieceType.Grave);
        Assert.Contains(cells, cell => cell.Piece == PieceType.Castle && TerrainCatalog.Damage(cell) > 0);
        foreach (var owner in new[] { PlayerId.PlayerOne, PlayerId.PlayerTwo })
            Assert.Contains(cells, cell => cell.Piece == PieceType.Knight && cell.Owner == owner &&
                TerrainCatalog.DamageMarkerCount(cell) > 0);
    }

    [Theory]
    [InlineData(PieceType.None, null, 1, 2)]
    [InlineData(PieceType.Knight, PlayerId.PlayerOne, 1, 2)]
    [InlineData(PieceType.Knight, PlayerId.PlayerTwo, 2, 1)]
    [InlineData(PieceType.Knight, PlayerId.PlayerOne, 3, 0)]
    [InlineData(PieceType.Castle, PlayerId.PlayerOne, 1, 0)]
    [InlineData(PieceType.Castle, PlayerId.PlayerTwo, 2, 0)]
    [InlineData(PieceType.Grave, null, 2, 0)]
    [InlineData(PieceType.Grave, null, 0, 0)]
    public void MarkerVisibilityIsIndependentOfTerrainWear(PieceType piece, PlayerId? owner, int health, int markers)
    {
        var cell = new CellState(TerrainType.Playable, piece, owner, health);
        Assert.Equal(3 - health, TerrainCatalog.Damage(cell));
        Assert.Equal(markers, TerrainCatalog.DamageMarkerCount(cell));
        Assert.Equal(0, TerrainCatalog.DamageMarkerCount(default));
    }
}
