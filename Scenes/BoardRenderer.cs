#nullable enable

using System;
using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

/// <summary>Presents the board snapshot, including terrain, territory, pieces, and selection highlights.</summary>
/// <remarks>
/// Uses the same square cells for drawing, sprite placement, and hit testing.
/// Terrain, secured territory, and the tile grid draw beneath the pieces. Turn indicators draw above them.
/// </remarks>
public partial class BoardRenderer : Control
{
    private const int GridLayer = 1;
    private const int PieceLayer = 2;
    private const int LegalTargetLayer = 3;
    private const int SelectionLayer = 4;
    private const float GraveFootprint = 1f;
    private static readonly Color GridColor = new("#18200d55");
    private static readonly Color LegalTargetColor = new("#f8d47b");
    private static readonly Color SelectionColor = new("#defaff");

    private readonly BoardTerrainArt _terrainArt = new();
    private BoardState? _board;
    private TerritoryAnalysis? _territory;
    private HashSet<BoardPosition> _eligiblePositions = [];
    private readonly List<(BoardPosition Position, Sprite2D Sprite, float Footprint)> _pieceSprites = [];
    private FactionPieceArt? _knightArt;
    private FactionPieceArt? _castleArt;
    private Texture2D? _graveTexture;
    private Node2D? _gridOverlay;
    private Node2D? _legalOverlay;
    private Node2D? _selectionOverlay;
    private Texture2D _tileQuad = null!;
    private ShaderMaterial _gridMaterial = null!;
    private ShaderMaterial _legalMaterial = null!;

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
        if (_legalOverlay is not null)
        {
            _legalOverlay.Material = territory is null ? null : _legalMaterial;
        }

        if (piecesMayHaveChanged)
        {
            UpdatePieceSprites(board);
            _gridOverlay?.QueueRedraw();
        }

        QueueRedraw();
        ClearSelection();
    }

    /// <summary>Removes the provisional highlight and restores any legal indicator it replaced.</summary>
    public void ClearSelection()
    {
        SelectedPosition = null;
        RedrawIndicators();
    }

    /// <summary>Initializes the grid and indicator layers and keeps them aligned during resize.</summary>
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        var shader = GD.Load<Shader>("res://Shaders/selection_indicator.gdshader");
        // Textured quads give the shader a full 0–1 UV range for every tile.
        using var pixel = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        pixel.Fill(Colors.White);
        _tileQuad = ImageTexture.CreateFromImage(pixel);
        _gridMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/tile_grid.gdshader") };
        _gridMaterial.SetShaderParameter("grid_color", GridColor);
        _gridOverlay = new Node2D
        {
            Name = "TileGrid",
            ZIndex = GridLayer,
            Material = _gridMaterial,
        };
        _legalMaterial = CreateIndicatorMaterial(shader, LegalTargetColor, 0.045f);
        _legalOverlay = new Node2D
        {
            Name = "LegalTargets",
            ZIndex = LegalTargetLayer,
            Material = _territory is null ? null : _legalMaterial,
        };
        _selectionOverlay = new Node2D
        {
            Name = "ProvisionalSelection",
            ZIndex = SelectionLayer,
            Material = CreateIndicatorMaterial(shader, SelectionColor, 0.065f),
        };
        _gridOverlay.Draw += DrawTileGrid;
        _legalOverlay.Draw += DrawLegalTargets;
        _selectionOverlay.Draw += DrawProvisionalSelection;
        AddChild(_gridOverlay);
        AddChild(_legalOverlay);
        AddChild(_selectionOverlay);
        Resized += RefreshLayout;
        RefreshLayout();
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
            RedrawIndicators();
            SelectionChanged?.Invoke();
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>Draws terrain and secured-territory overlays beneath the piece and indicator layers.</summary>
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
                DrawTextureRect(_terrainArt.GetTexture(cell), tile, tile: false);
                DrawTerritory(tile, position);
            }
        }
    }

    private static ShaderMaterial CreateIndicatorMaterial(Shader shader, Color color, float borderWidth)
    {
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("indicator_color", color);
        material.SetShaderParameter("border_width", borderWidth);
        return material;
    }

    private void DrawTileGrid()
    {
        if (_board is not { } board || _gridOverlay is not { } overlay || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        var geometry = new BoardGeometry(Size, board.Width, board.Height);
        _gridMaterial.SetShaderParameter("grid_size", new Vector2(board.Width, board.Height));
        overlay.DrawTextureRect(_tileQuad, geometry.Bounds, tile: false);
    }

    private void DrawLegalTargets()
    {
        if (_board is not { } board || _legalOverlay is not { } overlay || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        var geometry = new BoardGeometry(Size, board.Width, board.Height);
        foreach (BoardPosition position in _eligiblePositions)
        {
            if (!board.IsWithinBounds(position))
            {
                continue;
            }

            Rect2 tile = geometry.GetCellRect(position);
            if (_territory is null)
            {
                overlay.DrawCircle(tile.GetCenter(), geometry.CellPitch * 0.16f, LegalTargetColor, antialiased: true);
            }
            else if (position != SelectedPosition)
            {
                overlay.DrawTextureRect(_tileQuad, tile, tile: false);
            }
        }
    }

    private void DrawProvisionalSelection()
    {
        if (_board is { } board && _selectionOverlay is { } overlay &&
            SelectedPosition is { } position && Size.X > 0 && Size.Y > 0)
        {
            var geometry = new BoardGeometry(Size, board.Width, board.Height);
            overlay.DrawTextureRect(_tileQuad, geometry.GetCellRect(position), tile: false);
        }
    }

    private void RedrawIndicators()
    {
        _legalOverlay?.QueueRedraw();
        _selectionOverlay?.QueueRedraw();
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
                if (CreatePieceSprite(board.GetCell(position)) is { } piece)
                {
                    piece.Sprite.ZIndex = PieceLayer;
                    AddChild(piece.Sprite);
                    _pieceSprites.Add((position, piece.Sprite, piece.Footprint));
                }
            }
        }

        LayoutPieceSprites(board);
    }

    private (Sprite2D Sprite, float Footprint)? CreatePieceSprite(CellState cell)
    {
        if (cell.Piece == PieceType.Grave)
        {
            _graveTexture ??= GD.Load<Texture2D>("res://Assets/Sprites/Objects/Grave/grave.png");
            return (new Sprite2D { Texture = _graveTexture }, GraveFootprint);
        }

        return cell.Owner is { } owner && GetPieceArt(cell.Piece) is { } art
            ? (art.CreateSprite(owner), art.CellFootprint)
            : null;
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
        _gridOverlay?.QueueRedraw();
        RedrawIndicators();
    }

    private void LayoutPieceSprites(BoardState board)
    {
        var geometry = new BoardGeometry(Size, board.Width, board.Height);
        foreach (var (position, sprite, footprint) in _pieceSprites)
        {
            FactionPieceArt.Place(sprite, geometry.GetCellRect(position), footprint);
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
}
