using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class PresentationSettingsTests
{
    [Theory]
    [InlineData(null, true, false, true)]
    [InlineData("{this is malformed", true, false, true)]
    [InlineData("[]", true, false, true)]
    [InlineData("{\"ReduceMotion\":true,\"PlayTurnResolutionAnimations\":null}", true, true, true)]
    [InlineData("{\"ShowOpponentLegalTargets\":false,\"ReduceMotion\":\"true\",\"PlayTurnResolutionAnimations\":false}", false, false, false)]
    public void MissingOrInvalidFieldsIndependentlyFallBackToDefaults(string? json,
        bool showOpponent, bool reduceMotion, bool animations)
    {
        using var folder = new TemporaryFolder();
        string path = Path.Combine(folder.Location, "presentation.json");
        if (json is not null) File.WriteAllText(path, json);
        Assert.Equal(new PresentationPreferences(showOpponent, reduceMotion, animations),
            new PresentationSettings(path).Current);
    }

    [Fact]
    public void LiveUpdatePersistsAndOnlyPublishesChangesToRegisteredSubscribers()
    {
        using var folder = new TemporaryFolder();
        string path = Path.Combine(folder.Location, "presentation.json");
        var store = new PresentationSettings(path);
        var sceneOneChanges = new List<PresentationPreferences>();
        var sceneTwoChanges = new List<PresentationPreferences>();
        store.Changed += sceneOneChanges.Add;
        var first = new PresentationPreferences(false, true, true);
        store.Update(first);
        store.Update(first);
        Assert.Equal(first, store.Current);
        Assert.Equal(first, new PresentationSettings(path).Current);
        store.Changed -= sceneOneChanges.Add;
        store.Changed += sceneTwoChanges.Add;
        var second = new PresentationPreferences(true, false, false);
        store.Update(second);
        Assert.Equal([first], sceneOneChanges);
        Assert.Equal([second], sceneTwoChanges);
        Assert.Equal(second, new PresentationSettings(path).Current);
        Assert.Single(Directory.GetFiles(folder.Location));
    }

    private sealed class TemporaryFolder : IDisposable
    {
        internal string Location { get; } = Directory.CreateTempSubdirectory("castrum-presentation-").FullName;
        public void Dispose() => Directory.Delete(Location, recursive: true);
    }
}
