using Godot;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class MatchLayoutTests
{
    [Theory]
    [InlineData(448, 800, false)]
    [InlineData(448, 368, false)]
    [InlineData(469, 368, false)]
    [InlineData(470, 368, true)]
    [InlineData(800, 368, true)]
    [InlineData(700, 630, false)]
    public void SidebarRequiresBoardWidthAndLandscapeProportions(int width, int height, bool expected)
    {
        Assert.Equal(expected, MatchLayout.UseSidebar(new Vector2(width, height)));
    }

    [Fact]
    public void ContentFitsInsideAsymmetricSafeArea()
    {
        var viewport = new Rect2(0, 0, 800, 400);
        var safeArea = new Rect2(30, 0, 750, 420);

        Assert.Equal(new Rect2(40, 10, 730, 380), MatchLayout.GetContentRect(viewport, safeArea));
    }

    [Fact]
    public void UnavailableSafeAreaFallsBackToViewport()
    {
        var viewport = new Rect2(0, 0, 480, 400);

        Assert.Equal(new Rect2(10, 10, 460, 380), MatchLayout.GetContentRect(viewport, default));
    }

    [Theory]
    [InlineData(0.75f, 0.75f, 160, 64, 64)]
    [InlineData(2.25f, 2.25f, 440, 59, 59)]
    [InlineData(2.5f, 2.0f, 480, 58, 72)]
    [InlineData(3f, 3f, 320, 48, 48)]
    [InlineData(0f, 0f, 0, 48, 48)]
    public void TapTargetsAccountForDensityAndCanvasStretch(float scaleX, float scaleY, int densityDpi, int width, int height)
    {
        Assert.Equal(new Vector2(width, height), MatchLayout.GetTapTarget(new Vector2(scaleX, scaleY), densityDpi));
    }
}
