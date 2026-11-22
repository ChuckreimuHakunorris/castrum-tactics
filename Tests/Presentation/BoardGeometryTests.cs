using CastrumTactics.Domain;
using Godot;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class BoardGeometryTests
{
    [Theory]
    [InlineData(1120, 400)]
    [InlineData(360, 640)]
    [InlineData(140, 100)]
    public void DrawnCellsStaySquareCenteredAndHittable(int width, int height)
    {
        var geometry = new BoardGeometry(new Vector2(width, height), 7, 5);
        Rect2 first = geometry.GetCellRect(new BoardPosition(0, 0));
        Rect2 last = geometry.GetCellRect(new BoardPosition(6, 4));

        Assert.Equal(first.Position.X, width - last.End.X, 3);
        Assert.Equal(first.Position.Y, height - last.End.Y, 3);
        Assert.Equal(0.9f * Math.Min(width / 7f, height / 5f), geometry.CellPitch, 3);
        float gap = Math.Min(3, geometry.CellPitch * 0.12f);

        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                var position = new BoardPosition(x, y);
                Rect2 cell = geometry.GetCellRect(position);
                Assert.Equal(cell.Size.X, cell.Size.Y);
                Assert.Equal(geometry.CellPitch - gap, cell.Size.X, 3);
                Assert.Equal(position, geometry.HitTest(cell.GetCenter()));
            }
        }
    }

    [Theory]
    [InlineData(-1, 70)]
    [InlineData(700, 70)]
    [InlineData(80, -1)]
    [InlineData(80, 500)]
    [InlineData(20, 70)]
    [InlineData(80, 10)]
    [InlineData(35, 70)]
    [InlineData(665, 70)]
    [InlineData(80, 25)]
    [InlineData(80, 475)]
    [InlineData(125, 70)]
    [InlineData(80, 115)]
    public void OutsidePointsMarginsAndInterCellGapsMiss(float x, float y)
    {
        var geometry = new BoardGeometry(new Vector2(700, 500), 7, 5);

        Assert.Null(geometry.HitTest(new Vector2(x, y)));
    }

    [Fact]
    public void HitTestIncludesTopAndLeftEdgesAndExcludesBottomAndRightEdges()
    {
        var geometry = new BoardGeometry(new Vector2(700, 500), 7, 5);
        var position = new BoardPosition(3, 2);
        Rect2 cell = geometry.GetCellRect(position);
        Vector2 center = cell.GetCenter();

        Assert.Equal(position, geometry.HitTest(cell.Position));
        Assert.Equal(position, geometry.HitTest(cell.End - Vector2.One * 0.01f));
        Assert.Null(geometry.HitTest(new Vector2(cell.Position.X - 0.01f, center.Y)));
        Assert.Null(geometry.HitTest(new Vector2(center.X, cell.Position.Y - 0.01f)));
        Assert.Null(geometry.HitTest(new Vector2(cell.End.X, center.Y)));
        Assert.Null(geometry.HitTest(new Vector2(center.X, cell.End.Y)));
    }

    [Fact]
    public void HitTestUsesTheCurrentControlSize()
    {
        var landscape = new BoardGeometry(new Vector2(700, 500), 7, 5);
        var portrait = new BoardGeometry(new Vector2(360, 640), 7, 5);
        var position = new BoardPosition(0, 0);
        Vector2 originalCenter = landscape.GetCellRect(position).GetCenter();

        Assert.Equal(position, landscape.HitTest(originalCenter));
        Assert.Null(portrait.HitTest(originalCenter));
        Assert.Equal(position, portrait.HitTest(portrait.GetCellRect(position).GetCenter()));
    }

    [Fact]
    public void EmptyControlDoesNotHitAnyCell()
    {
        var geometry = new BoardGeometry(new Vector2(0, 500), 7, 5);

        Assert.Null(geometry.HitTest(new Vector2(0, 250)));
    }
}
