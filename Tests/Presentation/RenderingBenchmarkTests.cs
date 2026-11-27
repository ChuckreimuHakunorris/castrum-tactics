using CastrumTactics.Domain;
using CastrumTactics.RenderingBenchmarks;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class RenderingBenchmarkTests
{
    [Theory]
    [InlineData("quick", 2, 4, 1, 3)]
    [InlineData("standard", 8, 20, 3, 8)]
    [InlineData("sustained", 30, 1200, 1, 1)]
    public void ProfilesSupplyVersionedDefaults(string profile, double warmup, double measure, int repeats, int cases)
    {
        var options = RenderBenchmarkOptions.Parse($"{{\"profile\":\"{profile}\"}}");
        Assert.Equal(warmup, options.WarmupSeconds);
        Assert.Equal(measure, options.MeasureSeconds);
        Assert.Equal(repeats, options.Repetitions);
        Assert.Equal(cases, options.Cases!.Length);
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
    [InlineData("damage")]
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
        if (id == "damage") Assert.True(first.Board.GetCell(new BoardPosition(0, 0)).GroundHealth < 3);
    }
}
