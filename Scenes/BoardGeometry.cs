using CastrumTactics.Domain;
using Godot;

internal readonly struct BoardGeometry
{
    private readonly Rect2 _bounds;
    private readonly Vector2 _cellInset;
    private readonly Vector2 _tileSize;

    internal BoardGeometry(Vector2 controlSize, int columns, int rows)
    {
        // Fit both axes with one pitch and leave a margin around the centered board.
        CellPitch = 0.9f * Mathf.Min(controlSize.X / columns, controlSize.Y / rows);
        Vector2 boardSize = new Vector2(columns, rows) * CellPitch;
        _bounds = new Rect2((controlSize - boardSize) / 2, boardSize);

        // Cap the gap so small cells retain most of their area.
        float gap = Mathf.Min(3f, CellPitch * 0.12f);
        _cellInset = Vector2.One * (gap / 2);
        _tileSize = Vector2.One * (CellPitch - gap);
    }

    internal float CellPitch { get; }

    internal Rect2 GetCellRect(BoardPosition position)
    {
        Vector2 topLeft = _bounds.Position + new Vector2(position.X, position.Y) * CellPitch;
        return new Rect2(topLeft + _cellInset, _tileSize);
    }

    internal BoardPosition? HitTest(Vector2 localPoint)
    {
        if (CellPitch <= 0 || !_bounds.HasPoint(localPoint))
        {
            return null;
        }

        // The pitch finds the grid slot. The inset draw rectangle rejects the gutters.
        Vector2 offset = (localPoint - _bounds.Position) / CellPitch;
        var position = new BoardPosition(Mathf.FloorToInt(offset.X), Mathf.FloorToInt(offset.Y));
        return GetCellRect(position).HasPoint(localPoint) ? position : null;
    }
}
