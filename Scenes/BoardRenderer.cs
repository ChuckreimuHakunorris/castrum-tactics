#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
    private const int DamageLayer = 3;
    private const int LegalTargetLayer = 4;
    private const int SelectionLayer = 5;
    private const float GraveFootprint = 1f;
    private static readonly Color GridColor = new("#18200d55");
    private static readonly Color LegalTargetColor = new("#f8d47b");
    private static readonly Color SelectionColor = new("#defaff");

    private BoardState? _board;
    private BoardVisuals? _visuals;
    private TerritoryAnalysis? _territory;
    private HashSet<BoardPosition> _eligiblePositions = [];
    private readonly List<(BoardPosition Position, Sprite2D Sprite, float Footprint)> _pieceSprites = [];
    internal IEnumerable<BoardPosition> OpposingTargets { get; private set; } = [];
    private FactionPieceArt? _knightArt;
    private FactionPieceArt? _castleArt;
    private readonly Dictionary<int, FactionPieceArt> _wallArt = [];
    private Texture2D? _graveTexture;
    private Node2D? _gridOverlay;
    private Node2D? _damageOverlay;
    private Node2D? _legalOverlay;
    private Node2D? _selectionOverlay;
    private Texture2D _tileQuad = null!;
    private ShaderMaterial _gridMaterial = null!;
    private ShaderMaterial _legalMaterial = null!;
    private TerritoryScoreBanner? _scoreBanner;

    /// <summary>Gets the provisional selection for the current view, if any.</summary>
    public BoardPosition? SelectedPosition { get; private set; }

    /// <summary>Reports that a player highlighted a different eligible board position.</summary>
    public event Action? SelectionChanged;

    /// <summary>Requests confirmation when the current provisional square is pressed again while it is still eligible.</summary>
    /// <remarks>The renderer leaves the highlight in place for the caller to submit or clear.</remarks>
    public event Action? SelectionConfirmed;

    /// <summary>Gets the actual square-cell grid bounds in the renderer's local coordinates.</summary>
    /// <returns>The rendered grid rectangle, or an empty rectangle before a board is assigned.</returns>
    public Rect2 GetRenderedGridBounds() => _board is { } board ? GetGeometry(board).Bounds : default;

    /// <summary>Displays the supplied board, updates its pieces, and clears the provisional selection.</summary>
    /// <param name="board">The snapshot providing the board's dimensions, terrain, and pieces.</param>
    /// <param name="eligiblePositions">
    /// The targets for the active player. Pass an empty collection or null to disable selection.
    /// </param>
    /// <param name="territory">The territory analysis for the same snapshot, or null during the opening draft.</param>
    /// <exception cref="ArgumentNullException"><paramref name="board"/> is null.</exception>
    public void SetBoard(BoardState board, IEnumerable<BoardPosition>? eligiblePositions = null, TerritoryAnalysis? territory = null)
        => SetMatchBoard(board, eligiblePositions, [], territory);

    internal void SetVisuals(BoardVisuals? visuals)
    {
        _visuals = visuals;
        QueueRedraw();
    }

    internal void SetMatchBoard(BoardState board, IEnumerable<BoardPosition>? eligiblePositions,
        IEnumerable<BoardPosition> opposing, TerritoryAnalysis? territory)
    {
        ArgumentNullException.ThrowIfNull(board);

        OpposingTargets = opposing;
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
            LayoutBanner(board);
            UpdatePieceSprites(board);
            _gridOverlay?.QueueRedraw();
            _damageOverlay?.QueueRedraw();
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
        _scoreBanner = GetNodeOrNull<TerritoryScoreBanner>("TerritoryScoreBanner");
        TextureFilter = TextureFilterEnum.Linear;
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
        _damageOverlay = new Node2D { Name = "GroundDamage", ZIndex = DamageLayer };
        _damageOverlay.Draw += DrawGroundDamage;
        _gridOverlay.Draw += DrawTileGrid;
        _legalOverlay.Draw += DrawLegalTargets;
        _selectionOverlay.Draw += DrawProvisionalSelection;
        AddChild(_gridOverlay);
        AddChild(_damageOverlay);
        AddChild(_legalOverlay);
        AddChild(_selectionOverlay);
        Resized += RefreshLayout;
        RefreshLayout();
    }

    /// <summary>Handles left-button and touch presses that reach the board after GUI input.</summary>
    /// <param name="inputEvent">A viewport event. Emulated, canceled, and release events are ignored.</param>
    /// <remarks>Misses preserve the highlight. An eligible hit consumes the event and highlights or confirms the square.</remarks>
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

        if (HitTestViewport(board, point) is not { } position || !_eligiblePositions.Contains(position))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        if (SelectedPosition == position)
        {
            SelectionConfirmed?.Invoke();
            return;
        }

        SelectedPosition = position;
        RedrawIndicators();
        SelectionChanged?.Invoke();
    }

    /// <summary>Draws terrain and secured-territory overlays beneath the piece and indicator layers.</summary>
    public override void _Draw()
    {
        if (_board is not { } board || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        var geometry = GetGeometry(board);
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                Rect2 tile = geometry.GetCellRect(position);
                CellState cell = board.GetCell(position);
                BoardTerrainArt.Draw(this, tile, cell, _visuals?.At(position) ?? default);
                DrawTerritory(tile, position);
            }
        }
    }

    private BoardPosition? HitTestViewport(BoardState board, Vector2 point) =>
        GetGeometry(board).HitTest(MakeCanvasPositionLocal(point));

    private void DrawGroundDamage()
    {
        if (_board is not { } board || _damageOverlay is not { } overlay) return;
        var geometry = GetGeometry(board);
        if (geometry.CellPitch <= 0) return;

        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                int count = TerrainCatalog.Damage(board.GetCell(position));
                if (count == 0) continue;

                // Marks stay above pieces and tint, inside the legal frame, in the same corner on every surface.
                Rect2 tile = geometry.GetCellRect(position);
                for (int mark = 0; mark < count; mark++)
                {
                    Vector2 center = tile.Position + tile.Size * new Vector2(0.87f - mark * 0.10f, 0.88f);
                    float radius = geometry.CellPitch * 0.042f;
                    overlay.DrawCircle(center, radius * 1.28f, new Color("#362624"), antialiased: true);
                    overlay.DrawCircle(center, radius * 0.72f, new Color("#ffe3a2"), antialiased: true);
                }
            }
    }

    private string DescribeVisualTile(int x, int y)
    {
        var visual = _visuals?.At(new BoardPosition(x, y)) ?? default;
        return visual.Terrain is { } terrain ? $"{terrain}/{visual.ArtVariant}"
            : $"{visual.Variant}/{visual.NorthRockEdge}/{visual.StoneAccent}";
    }

    private int ReadGroundDamage(int x, int y) =>
        _board is { } board ? TerrainCatalog.Damage(board.GetCell(new BoardPosition(x, y))) : 0;

    private string[] ReadTerrainCatalogPaths() => TerrainCatalog.Types
        .SelectMany(pair => pair.Value.Variants.SelectMany(variant =>
            Enumerable.Range(0, pair.Value.Playable ? TerrainCatalog.FullHealth + 1 : 1)
                .Select(damage => BoardTerrainArt.PathFor(pair.Key, variant, damage))))
        .ToArray();

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

        var geometry = GetGeometry(board);
        _gridMaterial.SetShaderParameter("grid_size", new Vector2(board.Width, board.Height));
        overlay.DrawTextureRect(_tileQuad, geometry.Bounds, tile: false);
    }

    private void DrawLegalTargets()
    {
        if (_board is not { } board || _legalOverlay is not { } overlay || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        var geometry = GetGeometry(board);
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
            var geometry = GetGeometry(board);
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
        Func<BoardPosition, CellState> cellAt = board.GetCell;
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                CellState cell = cellAt(position);
                int mask = cell is { Piece: PieceType.Castle, Owner: { } owner }
                    ? CastleTopology.Mask(position, owner, board.Width, board.Height, cellAt) : 0;
                if (CreatePieceSprite(cell, mask) is { } piece)
                {
                    piece.Sprite.ZIndex = PieceLayer;
                    AddChild(piece.Sprite);
                    _pieceSprites.Add((position, piece.Sprite, piece.Footprint));
                }
            }
        }

        LayoutPieceSprites(board);
    }

    private (Sprite2D Sprite, float Footprint)? CreatePieceSprite(CellState cell, int castleMask)
    {
        if (cell.Piece == PieceType.Grave)
        {
            _graveTexture ??= BoardTerrainArt.GetTexture(TerrainCatalog.AssetDirectory + "grave.png");
            return (new Sprite2D { Texture = _graveTexture }, GraveFootprint);
        }

        return cell.Owner is { } owner && (castleMask == 0 ? GetPieceArt(cell.Piece) : GetWallArt(castleMask)) is { } art
            ? (art.CreateSprite(owner), art.CellFootprint)
            : null;
    }

    private FactionPieceArt? GetPieceArt(PieceType piece) => piece switch
    {
        PieceType.Knight => _knightArt ??= new FactionPieceArt(
            BoardTerrainArt.GetTexture(TerrainCatalog.AssetDirectory + "knight_base.png"),
            BoardTerrainArt.GetTexture(TerrainCatalog.AssetDirectory + "knight_mask.png"),
            0.94f, linear: true),
        PieceType.Castle => _castleArt ??= new FactionPieceArt(
            BoardTerrainArt.GetTexture(TerrainCatalog.AssetDirectory + "castle_base.png"),
            BoardTerrainArt.GetTexture(TerrainCatalog.AssetDirectory + "castle_mask.png"),
            0.98f, linear: true),
        _ => null,
    };

    private FactionPieceArt GetWallArt(int mask)
    {
        if (!_wallArt.TryGetValue(mask, out var art))
        {
            string name = TerrainCatalog.AssetDirectory + $"wall-{mask}";
            art = new FactionPieceArt(BoardTerrainArt.GetTexture(name + "_base.png"),
                BoardTerrainArt.GetTexture(name + "_mask.png"), 1f, linear: true);
            _wallArt.Add(mask, art);
        }
        return art;
    }

    private void RefreshLayout()
    {
        if (_board is { } board)
        {
            LayoutBanner(board);
            LayoutPieceSprites(board);
        }

        QueueRedraw();
        _gridOverlay?.QueueRedraw();
        _damageOverlay?.QueueRedraw();
        RedrawIndicators();
    }

    private void LayoutPieceSprites(BoardState board)
    {
        var geometry = GetGeometry(board);
        foreach (var (position, sprite, footprint) in _pieceSprites)
        {
            FactionPieceArt.Place(sprite, geometry.GetCellRect(position), footprint);
        }
    }

    private float BannerReserve => _scoreBanner is null ? 0 : TerritoryScoreBanner.ReservedHeight(Size.Y);

    private BoardGeometry GetGeometry(BoardState board) => new(Size, board.Width, board.Height, BannerReserve);

    private void LayoutBanner(BoardState board) => _scoreBanner?.AlignWith(GetGeometry(board).Bounds, BannerReserve);

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
