#nullable enable

using System;
using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

/// <summary>Displays board terrain and lets the player highlight a playable cell.</summary>
/// <remarks>Centers the board in the control and keeps its cells square as the control resizes.</remarks>
public partial class BoardRenderer : Control
{
    private static readonly Color PlayableColor = new("#829661");
    private static readonly Color InaccessibleColor = new("#343a49");
    private static readonly Color StartingPositionColor = new("#f8d47b");
    private static readonly Color SelectionColor = new("#defaff");

    private BoardState? _board;
    private HashSet<BoardPosition> _startingPositions = [];
    private BoardPosition? _selectedPosition;

    /// <summary>Displays the supplied board, clears the selection, and schedules a redraw.</summary>
    /// <param name="board">The board whose dimensions and terrain will be drawn.</param>
    /// <param name="startingPositions">
    /// Candidate positions from the board's map. Omit this to hide all starting-position markers.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="board"/> is null.</exception>
    public void SetBoard(BoardState board, IEnumerable<BoardPosition>? startingPositions = null)
    {
        ArgumentNullException.ThrowIfNull(board);

        _startingPositions = startingPositions is null ? [] : new HashSet<BoardPosition>(startingPositions);
        _board = board;
        _selectedPosition = null;
        QueueRedraw();
    }

    /// <summary>Schedules a redraw whenever the control's available size changes.</summary>
    public override void _Ready()
    {
        Resized += QueueRedraw;
    }

    /// <summary>Handles left-button and touch presses that reach the board after GUI input.</summary>
    /// <param name="inputEvent">A viewport event. Emulated, canceled, and release events are ignored.</param>
    /// <remarks>Misses preserve the current highlight. A playable hit consumes the event.</remarks>
    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_board is not { } board || !IsVisibleInTree() || inputEvent.Device == InputEvent.DeviceIdEmulation)
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
        if (geometry.HitTest(localPoint) is not { } position ||
            board.GetCell(position).Terrain != TerrainType.Playable)
        {
            return;
        }

        if (_selectedPosition != position)
        {
            _selectedPosition = position;
            QueueRedraw();
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
                bool isPlayableTerrain = board.GetCell(position).Terrain == TerrainType.Playable;
                DrawRect(tile, isPlayableTerrain ? PlayableColor : InaccessibleColor);

                if (isPlayableTerrain && _startingPositions.Contains(position))
                {
                    DrawCircle(tile.GetCenter(), geometry.CellPitch * 0.16f,
                        StartingPositionColor, antialiased: true);
                }

                if (_selectedPosition == position)
                {
                    // Godot centers the stroke on its path, so inset it to preserve the gap.
                    float width = Mathf.Min(4f, tile.Size.X * 0.12f);
                    DrawRect(tile.Grow(-width / 2), SelectionColor, filled: false, width: width);
                }
            }
        }
    }
}
