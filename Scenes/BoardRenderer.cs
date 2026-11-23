#nullable enable

using System;
using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

/// <summary>Presents the board snapshot, including terrain, territory, pieces, and selection highlights.</summary>
/// <remarks>Centers the board and fits its piece sprites to the same square cells used for hit testing.</remarks>
public partial class BoardRenderer : Control
{
    private static readonly Color HealthyColor = new("#829661");
    private static readonly Color SlightlyDegradedColor = new("#9c875c");
    private static readonly Color HeavilyDegradedColor = new("#765b47");
    private static readonly Color DepletedColor = new("#473b35");
    private static readonly Color InaccessibleColor = new("#343a49");
    private static readonly Color LegalTargetColor = new("#f8d47b");
    private static readonly Color SelectionColor = new("#defaff");
    private static readonly Color GraveColor = new("#ccc4bd");

    private BoardState? _board;
    private TerritoryAnalysis? _territory;
    private HashSet<BoardPosition> _eligiblePositions = [];
    private readonly List<(BoardPosition Position, Sprite2D Sprite, FactionPieceArt Art)> _pieceSprites = [];
    private FactionPieceArt? _knightArt;
    private FactionPieceArt? _castleArt;

    /// <summary>Gets the provisional selection for the current view, if any.</summary>
    public BoardPosition? SelectedPosition { get; private set; }

    /// <summary>Reports that a player highlighted a different eligible board position.</summary>
    public event Action? SelectionChanged;

    /// <summary>Displays the supplied board, updates its pieces, and clears the provisional selection.</summary>
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
        bool piecesMayHaveChanged = !ReferenceEquals(_board, board);
        _board = board;
        _territory = territory;
        if (piecesMayHaveChanged)
        {
            UpdatePieceSprites(board);
        }

        ClearSelection();
    }

    /// <summary>Removes the provisional highlight and schedules a redraw.</summary>
    public void ClearSelection()
    {
        SelectedPosition = null;
        QueueRedraw();
    }

    /// <summary>Keeps sprite placement and board drawing in sync with the control's size.</summary>
    public override void _Ready()
    {
        Resized += RefreshLayout;
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

    /// <summary>Draws terrain, indicators, and graves in the control's local coordinates.</summary>
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

                if (cell.Piece == PieceType.Grave)
                {
                    DrawGrave(tile);
                }

                if (SelectedPosition == position)
                {
                    // Godot centers the stroke on its path, so inset it to preserve the gap.
                    float width = Mathf.Min(4f, tile.Size.X * 0.12f);
                    DrawRect(tile.Grow(-width / 2), SelectionColor, filled: false, width: width);
                }
            }
        }
    }

    private void UpdatePieceSprites(BoardState board)
    {
        foreach (var (_, sprite, _) in _pieceSprites)
        {
            RemoveChild(sprite);
            sprite.QueueFree();
        }

        _pieceSprites.Clear();
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                CellState cell = board.GetCell(position);
                if (cell.Owner is { } owner && GetPieceArt(cell.Piece) is { } art)
                {
                    Sprite2D sprite = art.CreateSprite(owner);
                    AddChild(sprite);
                    _pieceSprites.Add((position, sprite, art));
                }
            }
        }

        LayoutPieceSprites(board);
    }

    private FactionPieceArt? GetPieceArt(PieceType piece) => piece switch
    {
        PieceType.Knight => _knightArt ??= new FactionPieceArt(
            GD.Load<Texture2D>("res://Assets/Sprites/Units/Knight/knight_base.png"),
            GD.Load<Texture2D>("res://Assets/Sprites/Units/Knight/knight_mask.png"),
            0.94f),
        PieceType.Castle => _castleArt ??= new FactionPieceArt(
            GD.Load<Texture2D>("res://Assets/Sprites/Units/Castle/castle_base.png"),
            GD.Load<Texture2D>("res://Assets/Sprites/Units/Castle/castle_mask.png"),
            0.98f),
        _ => null,
    };

    private void RefreshLayout()
    {
        if (_board is { } board)
        {
            LayoutPieceSprites(board);
        }

        QueueRedraw();
    }

    private void LayoutPieceSprites(BoardState board)
    {
        var geometry = new BoardGeometry(Size, board.Width, board.Height);
        foreach (var (position, sprite, art) in _pieceSprites)
        {
            art.Place(sprite, geometry.GetCellRect(position));
        }
    }

    private void DrawTerritory(Rect2 tile, BoardPosition position)
    {
        Color? factionColor = _territory?.GetControl(position) switch
        {
            TerritoryControl.SecuredByPlayerOne => FactionPalette.GetColor(PlayerId.PlayerOne),
            TerritoryControl.SecuredByPlayerTwo => FactionPalette.GetColor(PlayerId.PlayerTwo),
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

    private void DrawGrave(Rect2 tile)
    {
        Vector2 center = tile.GetCenter();
        float size = tile.Size.X;
        Vector2 arm = Vector2.One * (size * 0.19f);
        Vector2 otherArm = new(arm.X, -arm.Y);
        DrawLine(center - arm, center + arm, GraveColor, size * 0.09f, antialiased: true);
        DrawLine(center - otherArm, center + otherArm, GraveColor, size * 0.09f, antialiased: true);
    }
}
