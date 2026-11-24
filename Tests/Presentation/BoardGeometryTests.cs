using CastrumTactics.Domain;
using Godot;
using Xunit;

namespace CastrumTactics.Presentation.Tests;

public sealed class BoardGeometryTests
{
    [Theory]
    [InlineData(1120, 400, 7, 5)]
    [InlineData(360, 640, 7, 5)]
    [InlineData(140, 100, 7, 5)]
    [InlineData(140, 100, 3, 3)]
    [InlineData(360, 640, 4, 8)]
    [InlineData(1120, 400, 8, 4)]
    [InlineData(360, 640, 6, 12)]
    [InlineData(1120, 400, 12, 6)]
    [InlineData(140, 100, 12, 12)]
    [InlineData(360, 640, 12, 12)]
    public void DrawnCellsStaySquareCenteredAndHittable(int width, int height, int columns, int rows)
    {
        var geometry = new BoardGeometry(new Vector2(width, height), columns, rows);
        Rect2 first = geometry.GetCellRect(new BoardPosition(0, 0));
        Rect2 last = geometry.GetCellRect(new BoardPosition(columns - 1, rows - 1));

        Assert.Equal(first.Position.X, width - last.End.X, 3);
        Assert.Equal(first.Position.Y, height - last.End.Y, 3);
        Assert.Equal(0.96f * Math.Min((float)width / columns, (float)height / rows), geometry.CellPitch, 3);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var position = new BoardPosition(x, y);
                Rect2 cell = geometry.GetCellRect(position);
                Assert.Equal(geometry.CellPitch, cell.Size.X, 3);
                Assert.Equal(geometry.CellPitch, cell.Size.Y, 3);
                Assert.Equal(position, geometry.HitTest(cell.GetCenter()));
            }
        }
    }

    [Theory]
    [InlineData(-1, 70)]
    [InlineData(700, 70)]
    [InlineData(80, -1)]
    [InlineData(80, 500)]
    [InlineData(5, 70)]
    [InlineData(80, 5)]
    [InlineData(695, 70)]
    [InlineData(80, 495)]
    public void OutsidePointsAndMarginsMiss(float x, float y)
    {
        var geometry = new BoardGeometry(new Vector2(700, 500), 7, 5);

        Assert.Null(geometry.HitTest(new Vector2(x, y)));
    }

    [Theory]
    [InlineData(700, 500)]
    [InlineData(361, 647)]
    [InlineData(1049, 517)]
    public void SharedEdgesAreContinuousAndBelongToTheNextCell(int width, int height)
    {
        var geometry = new BoardGeometry(new Vector2(width, height), 7, 5);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                var position = new BoardPosition(x, y);
                var right = new BoardPosition(x + 1, y);
                var below = new BoardPosition(x, y + 1);
                var diagonal = new BoardPosition(x + 1, y + 1);
                Rect2 cell = geometry.GetCellRect(position);
                Vector2 center = cell.GetCenter();
                float rightEdge = geometry.GetCellRect(right).Position.X;
                float bottomEdge = geometry.GetCellRect(below).Position.Y;

                Assert.Equal(rightEdge, cell.End.X, 4);
                Assert.Equal(bottomEdge, cell.End.Y, 4);
                Assert.Equal(position, geometry.HitTest(cell.Position));
                Assert.Equal(position, geometry.HitTest(new Vector2(MathF.BitDecrement(rightEdge), center.Y)));
                Assert.Equal(position, geometry.HitTest(new Vector2(center.X, MathF.BitDecrement(bottomEdge))));
                Assert.Equal(right, geometry.HitTest(new Vector2(rightEdge, center.Y)));
                Assert.Equal(below, geometry.HitTest(new Vector2(center.X, bottomEdge)));
                Assert.Equal(diagonal, geometry.HitTest(new Vector2(rightEdge, bottomEdge)));
            }
        }
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
