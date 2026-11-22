#nullable enable

using System;
using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

/// <summary>Displays board terrain and optional starting-position markers in a Godot control.</summary>
/// <remarks>Centers the board in the control and keeps its cells square as the control resizes.</remarks>
public partial class BoardRenderer : Control
{
    private static readonly Color PlayableColor = new("#829661");
    private static readonly Color InaccessibleColor = new("#343a49");
    private static readonly Color StartingPositionColor = new("#f8d47b");

    private BoardState? _board;
    private HashSet<BoardPosition> _startingPositions = [];

    /// <summary>Displays the supplied board and schedules a redraw.</summary>
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
        QueueRedraw();
    }

    /// <summary>Schedules a redraw whenever the control's available size changes.</summary>
    public override void _Ready()
    {
        Resized += QueueRedraw;
    }

    /// <summary>Draws the board in local coordinates using the control's current size.</summary>
    public override void _Draw()
    {
        // Skip rendering until a board and a valid drawing area are available.
        if (_board is null || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        // Calculate square cell dimensions and center the board in the viewport.
        float cellSize = 0.9f * Mathf.Min(Size.X / _board.Width, Size.Y / _board.Height);
        var boardSize = new Vector2(_board.Width, _board.Height) * cellSize;
        Vector2 origin = (Size - boardSize) / 2;

        // Leave a small gap between cells that scales down on smaller screens.
        float gap = Mathf.Min(3f, cellSize * 0.12f);
        Vector2 cellInset = Vector2.One * (gap / 2);
        Vector2 tileSize = Vector2.One * (cellSize - gap);

        // Draw each cell using the terrain information from the domain model.
        for (int y = 0; y < _board.Height; y++)
        {
            for (int x = 0; x < _board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                Vector2 topLeft = origin + new Vector2(x, y) * cellSize;
                bool isPlayableTerrain = _board.GetCell(position).Terrain == TerrainType.Playable;

                DrawRect(new Rect2(topLeft + cellInset, tileSize),
                    isPlayableTerrain ? PlayableColor : InaccessibleColor);

                // Highlight eligible starting positions without modifying board state.
                if (isPlayableTerrain && _startingPositions.Contains(position))
                {
                    DrawCircle(topLeft + Vector2.One * (cellSize / 2), cellSize * 0.16f,
                        StartingPositionColor, antialiased: true);
                }
            }
        }
    }
}
