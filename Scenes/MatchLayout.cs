using System;
using Godot;

internal static class MatchLayout
{
    internal const float SidebarWidth = 126f;
    internal const int PanelSeparation = 8;
    private const float MinimumBoardWidth = 286f;
    private const float Inset = 10f;
    private const float TapTargetDp = 48f;
    private const float BaselineDensity = 160f;

    internal static Rect2 GetContentRect(Rect2 viewport, Rect2 safeArea)
    {
        Rect2 usable = viewport.Intersection(safeArea);
        if (!usable.HasArea())
        {
            usable = viewport;
        }

        Vector2 margin = (usable.Size * 0.5f).Min(new Vector2(Inset, Inset));
        return new Rect2(usable.Position + margin, usable.Size - 2 * margin);
    }

    internal static bool UseSidebar(Vector2 size, float panelWidth)
    {
        return size.X >= panelWidth + PanelSeparation + MinimumBoardWidth && size.X >= size.Y * 1.15f;
    }

    internal static Vector2 GetTapTarget(Vector2 pixelsPerUnit, int densityDpi)
    {
        var minimum = new Vector2(TapTargetDp, TapTargetDp);
        if (densityDpi <= 0 || pixelsPerUnit.X <= 0 || pixelsPerUnit.Y <= 0)
        {
            return minimum;
        }

        // Android's density converts dp to pixels before the canvas stretch is undone.
        float pixels = TapTargetDp * densityDpi / BaselineDensity;
        var scaled = new Vector2(MathF.Ceiling(pixels / pixelsPerUnit.X), MathF.Ceiling(pixels / pixelsPerUnit.Y));
        return scaled.Max(minimum);
    }
}
