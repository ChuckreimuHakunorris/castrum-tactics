#nullable enable

using System;
using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

/// <summary>Draws physical cells, strategic territory, and selection highlights supplied by the match view.</summary>
/// <remarks>Centers the board in the control and keeps its cells square as the control resizes.</remarks>
public partial class BoardRenderer : Control
{
    internal static readonly Color PlayerOneColor = new("#f06c68");
    internal static readonly Color PlayerTwoColor = new("#69a4ff");

    private static readonly Color HealthyColor = new("#829661");
    private static readonly Color SlightlyDegradedColor = new("#9c875c");
    private static readonly Color HeavilyDegradedColor = new("#765b47");
    private static readonly Color DepletedColor = new("#473b35");
    private static readonly Color InaccessibleColor = new("#343a49");
    private static readonly Color LegalTargetColor = new("#f8d47b");
    private static readonly Color SelectionColor = new("#defaff");
    private static readonly Color PieceOutline = new("#252630");
    private static readonly Color GraveColor = new("#ccc4bd");

    private BoardState? _board;
    private TerritoryAnalysis? _territory;
    private HashSet<BoardPosition> _eligiblePositions = [];

    /// <summary>Gets the provisional selection for the current view, if any.</summary>
    public BoardPosition? SelectedPosition { get; private set; }

    /// <summary>Reports that a player highlighted a different eligible board position.</summary>
    public event Action? SelectionChanged;

    /// <summary>Displays the supplied board, clears the selection, and schedules a redraw.</summary>
    /// <param name="board">The snapshot providing the board's dimensions, terrain, and pieces.</param>
    /// <param name="eligiblePositions">
    /// The targets for the active player. Pass an empty collection or null to disable selection.
    /// </param>
    /// <param name="territory">The territory analysis for the same snapshot, or null during the opening draft.</param>
    /// <exception cref="ArgumentNullException"><paramref name="board"/> is null.</exception>
    public void SetBoard(BoardState board, IEnumerable<BoardPosition>? eligiblePositions = null, TerritoryAnalysis? territory = null)
    {
        ArgumentNullException.ThrowIfNull(board);

        _eligiblePositions = eligiblePositions is null ? [] : new HashSet<BoardPosition>(eligiblePositions);
        _board = board;
        _territory = territory;
        ClearSelection();
    }

    /// <summary>Removes the provisional highlight and schedules a redraw.</summary>
    public void ClearSelection()
    {
        SelectedPosition = null;
        QueueRedraw();
    }

    /// <summary>Schedules a redraw whenever the control's available size changes.</summary>
    public override void _Ready()
    {
        Resized += QueueRedraw;
    }

    /// <summary>Handles left-button and touch presses that reach the board after GUI input.</summary>
    /// <param name="inputEvent">A viewport event. Emulated, canceled, and release events are ignored.</param>
    /// <remarks>Misses preserve the current highlight. An eligible hit consumes the event.</remarks>
    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_board is not { } board || _eligiblePositions.Count == 0 ||
            !IsVisibleInTree() || inputEvent.Device == InputEvent.DeviceIdEmulation)
        {
            return;
        }

        Vector2? viewportPoint = inputEvent switch
        {
            InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true, Canceled: false } mouse => mouse.Position,
            InputEventScreenTouch { Pressed: true, Canceled: false } touch => touch.Position,
            _ => null,
        };
        if (viewportPoint is not { } point)
        {
            return;
        }

        var geometry = new BoardGeometry(Size, board.Width, board.Height);
        Vector2 localPoint = MakeCanvasPositionLocal(point);
        if (geometry.HitTest(localPoint) is not { } position || !_eligiblePositions.Contains(position))
        {
            return;
        }

        if (SelectedPosition != position)
        {
            SelectedPosition = position;
            QueueRedraw();
            SelectionChanged?.Invoke();
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>Draws the board in local coordinates using the control's current size.</summary>
    public override void _Draw()
    {
        if (_board is not { } board || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        var geometry = new BoardGeometry(Size, board.Width, board.Height);
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                Rect2 tile = geometry.GetCellRect(position);
                CellState cell = board.GetCell(position);
                DrawRect(tile, GroundColor(cell));
                DrawTerritory(tile, position);

                if (_eligiblePositions.Contains(position))
                {
                    if (_territory is null)
                    {
                        DrawCircle(tile.GetCenter(), geometry.CellPitch * 0.16f, LegalTargetColor, antialiased: true);
                    }
                    else
                    {
                        // Keep the legal-target frame inside the separate provisional selection outline.
                        float width = Mathf.Min(2.5f, tile.Size.X * 0.06f);
                        DrawRect(tile.Grow(-tile.Size.X * 0.15f), LegalTargetColor, filled: false, width: width);
                    }
                }

                DrawPiece(tile, cell);
                if (SelectedPosition == position)
                {
                    // Godot centers the stroke on its path, so inset it to preserve the gap.
                    float width = Mathf.Min(4f, tile.Size.X * 0.12f);
                    DrawRect(tile.Grow(-width / 2), SelectionColor, filled: false, width: width);
                }
            }
        }
    }

    private void DrawTerritory(Rect2 tile, BoardPosition position)
    {
        Color? factionColor = _territory?.GetControl(position) switch
        {
            TerritoryControl.SecuredByPlayerOne => PlayerOneColor,
            TerritoryControl.SecuredByPlayerTwo => PlayerTwoColor,
            _ => null
        };
        if (factionColor is { } color)
        {
            DrawRect(tile, new Color(color, 0.24f));
            // The edge distinguishes faction tint from degradation when their ground colors are similar.
            float edge = Mathf.Min(2.5f, tile.Size.Y * 0.07f);
            DrawRect(new Rect2(tile.Position.X, tile.End.Y - edge, tile.Size.X, edge), new Color(color, 0.68f));
        }
    }

    private static Color GroundColor(CellState cell)
    {
        if (cell.Terrain == TerrainType.Inaccessible)
        {
            return InaccessibleColor;
        }

        return cell.GroundHealth switch
        {
            0 => DepletedColor,
            1 => HeavilyDegradedColor,
            2 => SlightlyDegradedColor,
            _ => HealthyColor
        };
    }

    private void DrawPiece(Rect2 tile, CellState cell)
    {
        Vector2 center = tile.GetCenter();
        float size = tile.Size.X;
        if (cell.Piece == PieceType.Knight)
        {
            Color color = cell.Owner == PlayerId.PlayerOne ? PlayerOneColor : PlayerTwoColor;
            DrawCircle(center, size * 0.32f, PieceOutline, antialiased: true);
            DrawCircle(center, size * 0.26f, color, antialiased: true);
        }
        else if (cell.Piece == PieceType.Castle)
        {
            DrawCastle(center, size, cell.Owner == PlayerId.PlayerOne ? PlayerOneColor : PlayerTwoColor);
        }
        else if (cell.Piece == PieceType.Grave)
        {
            Vector2 arm = Vector2.One * (size * 0.19f);
            Vector2 otherArm = new(arm.X, -arm.Y);
            DrawLine(center - arm, center + arm, GraveColor, size * 0.09f, antialiased: true);
            DrawLine(center - otherArm, center + otherArm, GraveColor, size * 0.09f, antialiased: true);
        }
    }

    private void DrawCastle(Vector2 center, float size, Color color)
    {
        float unit = size / 10;
        Vector2 topLeft = center - new Vector2(3, 3) * unit;
        var keep = new Rect2(topLeft + new Vector2(0, 1.5f) * unit, new Vector2(6, 4.5f) * unit);
        DrawRect(keep, PieceOutline);

        // Three battlements and a central doorway give the castle a different silhouette from the knight.
        for (int i = 0; i < 3; i++)
        {
            var battlement = new Rect2(topLeft + new Vector2(2.25f * i, 0) * unit, new Vector2(1.5f, 2) * unit);
            DrawRect(battlement, PieceOutline);
            DrawRect(battlement.Grow(-0.25f * unit), color);
        }

        DrawRect(keep.Grow(-0.45f * unit), color);
        DrawRect(new Rect2(center + new Vector2(-0.75f, 0.75f) * unit, new Vector2(1.5f, 2.25f) * unit), PieceOutline);
    }
}
