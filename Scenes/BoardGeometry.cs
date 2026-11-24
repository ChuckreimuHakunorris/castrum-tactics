using CastrumTactics.Domain;
using Godot;

internal readonly struct BoardGeometry
{
    private readonly Rect2 _bounds;
    private readonly int _columns;
    private readonly int _rows;

    internal BoardGeometry(Vector2 controlSize, int columns, int rows)
    {
        // Fit both axes with one pitch and leave a narrow margin around the centered board.
        CellPitch = 0.96f * Mathf.Min(controlSize.X / columns, controlSize.Y / rows);
        Vector2 boardSize = new Vector2(columns, rows) * CellPitch;
        _bounds = new Rect2((controlSize - boardSize) / 2, boardSize);
        _columns = columns;
        _rows = rows;
    }

    internal float CellPitch { get; }

    internal Rect2 GetCellRect(BoardPosition position)
    {
        Vector2 topLeft = GetCorner(position.X, position.Y);
        Vector2 bottomRight = GetCorner(position.X + 1, position.Y + 1);
        return new Rect2(topLeft, bottomRight - topLeft);
    }

    internal BoardPosition? HitTest(Vector2 localPoint)
    {
        if (CellPitch <= 0 || !_bounds.HasPoint(localPoint))
        {
            return null;
        }

        return new BoardPosition(
            HitAxis(localPoint.X, _bounds.Position.X, _columns),
            HitAxis(localPoint.Y, _bounds.Position.Y, _rows));
    }

    private Vector2 GetCorner(int x, int y) => new(
        GetEdge(_bounds.Position.X, x), GetEdge(_bounds.Position.Y, y));

    private float GetEdge(float start, int index) => start + index * CellPitch;

    private int HitAxis(float point, float start, int count)
    {
        int index = Mathf.Clamp(Mathf.FloorToInt((point - start) / CellPitch), 0, count - 1);
        // Rounding near a shared edge can put the estimate in the adjacent cell.
        if (index > 0 && point < GetEdge(start, index))
        {
            return index - 1;
        }

        return index + 1 < count && point >= GetEdge(start, index + 1) ? index + 1 : index;
    }
}
