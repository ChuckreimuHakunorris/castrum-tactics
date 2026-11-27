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
    private const float FrameReserve = 6f;
    private static readonly Color GridColor = new("#18200d55");
    private static readonly Color SelectionColor = new("#defaff");

    private BoardState? _board;
    private BoardVisuals? _visuals;
    private TerritoryAnalysis? _territory;
    private HashSet<BoardPosition> _eligiblePositions = [];
    private readonly List<(BoardPosition Position, Sprite2D Sprite, float Footprint)> _pieceSprites = [];
    private HashSet<BoardPosition> _opposingPositions = [];
    private PlayerId _activePlayer = PlayerId.PlayerOne;
    private PresentationPreferences _preferences = new();
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
    private Shader _animatedLegalShader = null!;
    private Shader _staticLegalShader = null!;
    private StyleBoxFlat _frameStyle = null!;
    private TerritoryScoreBanner? _scoreBanner;
    internal Action<long>? BenchmarkSpriteSync { get; set; }

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
        => SetMatchBoard(board, eligiblePositions, [], territory, PlayerId.PlayerOne);

    internal void SetVisuals(BoardVisuals? visuals)
    {
        _visuals = visuals;
        QueueRedraw();
    }

    internal void SetMatchBoard(BoardState board, IEnumerable<BoardPosition>? eligiblePositions,
        IEnumerable<BoardPosition> opposing, TerritoryAnalysis? territory, PlayerId activePlayer)
    {
        ArgumentNullException.ThrowIfNull(board);

        _opposingPositions = new HashSet<BoardPosition>(opposing);
        _eligiblePositions = eligiblePositions is null ? [] : new HashSet<BoardPosition>(eligiblePositions);
        _activePlayer = activePlayer;
        bool piecesMayHaveChanged = !ReferenceEquals(_board, board);
        _board = board;
        _territory = territory;
        ApplyLegalMaterial();

        if (piecesMayHaveChanged)
        {
            LayoutBanner(board);
            long syncStart = BenchmarkSpriteSync is null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            UpdatePieceSprites(board);
            BenchmarkSpriteSync?.Invoke(syncStart);
            _gridOverlay?.QueueRedraw();
            _damageOverlay?.QueueRedraw();
        }

        QueueRedraw();
        ClearSelection();
    }

    internal void ApplyPreferences(PresentationPreferences preferences)
    {
        bool changed = _preferences.ShowOpponentLegalTargets != preferences.ShowOpponentLegalTargets ||
            _preferences.ReduceMotion != preferences.ReduceMotion;
        _preferences = preferences;
        if (!changed) return;
        ApplyLegalMaterial();
        RedrawIndicators();
    }

    private void ApplyLegalMaterial()
    {
        if (_legalOverlay is null) return;

        bool showFrames = _territory is not null && (_eligiblePositions.Count > 0 ||
            (_preferences.ShowOpponentLegalTargets && _opposingPositions.Count > 0));
        _legalOverlay.Material = showFrames ? _legalMaterial : null;
        if (!showFrames) return;

        var shader = _preferences.ReduceMotion ? _staticLegalShader : _animatedLegalShader;
        if (_legalMaterial.Shader != shader) _legalMaterial.Shader = shader;
        PlayerId opposing = _activePlayer == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
        _legalMaterial.SetShaderParameter("active_color", FactionPalette.GetColor(_activePlayer));
        _legalMaterial.SetShaderParameter("opposing_color", FactionPalette.GetColor(opposing));
    }

    /// <summary>Removes the provisional highlight and restores the usual legal frame.</summary>
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
        _animatedLegalShader = GD.Load<Shader>("res://Shaders/legal_frame.gdshader");
        _staticLegalShader = GD.Load<Shader>("res://Shaders/legal_frame_static.gdshader");
        _legalMaterial = new ShaderMaterial();
        _frameStyle = new StyleBoxFlat
        {
            BgColor = new Color("#cbdff5"),
            ShadowColor = new Color("#0f264d52"),
            ShadowSize = 3,
            ShadowOffset = new Vector2(0, 1),
        };
        _frameStyle.SetCornerRadiusAll(4);
        _legalOverlay = new Node2D
        {
            Name = "LegalTargets",
            ZIndex = LegalTargetLayer,
        };
        _selectionOverlay = new Node2D
        {
            Name = "ProvisionalSelection",
            ZIndex = SelectionLayer,
            Material = CreateIndicatorMaterial(shader, SelectionColor, 0.10f),
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
        ApplyLegalMaterial();
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

    /// <summary>Draws the frame, terrain and secured territory beneath pieces and indicators.</summary>
    public override void _Draw()
    {
        if (_board is not { } board || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        var geometry = GetGeometry(board);
        if (geometry.CellPitch <= 0 || _frameStyle is null) return;
        DrawStyleBox(_frameStyle, geometry.Bounds.Grow(2));
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
                int count = TerrainCatalog.DamageMarkerCount(board.GetCell(position));
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

    private int ReadDamageMarkerCount(int x, int y) =>
        _board is { } board ? TerrainCatalog.DamageMarkerCount(board.GetCell(new BoardPosition(x, y))) : 0;

    private int ReadGroundDamage(int x, int y) =>
        _board is { } board ? TerrainCatalog.Damage(board.GetCell(new BoardPosition(x, y))) : 0;

    private string[] ReadTerrainCatalogPaths() => TerrainCatalog.Types
        .SelectMany(pair => pair.Value.Variants.SelectMany(variant =>
            Enumerable.Range(0, pair.Value.Playable ? TerrainCatalog.FullHealth + 1 : 1)
                .Select(damage => BoardTerrainArt.PathFor(pair.Key, variant, damage))))
        .ToArray();

    private int ReadLegalTargetKind(int x, int y)
    {
        var position = new BoardPosition(x, y);
        return _territory is null ? 0 : (int)TargetKind(position);
    }

    private LegalTargetKind TargetKind(BoardPosition position) => MatchPresentation.Classify(
        _eligiblePositions.Contains(position), _opposingPositions.Contains(position), _preferences.ShowOpponentLegalTargets);

    private static ShaderMaterial CreateIndicatorMaterial(Shader shader, Color color, float borderWidth)
    {
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("indicator_color", color);
        material.SetShaderParameter("border_width", borderWidth);
        material.SetShaderParameter("fade_depth", 0.06f);
        material.SetShaderParameter("fade_opacity", 0.22f);
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
            if (!board.IsWithinBounds(position)) continue;

            Rect2 tile = geometry.GetCellRect(position);
            if (_territory is null)
            {
                DraftShield.Draw(overlay, tile);
                continue;
            }

            var kind = TargetKind(position);
            if (position != SelectedPosition || kind == LegalTargetKind.Shared)
            {
                DrawFrame(tile, kind);
            }
        }

        if (_territory is null || !_preferences.ShowOpponentLegalTargets) return;
        foreach (BoardPosition position in _opposingPositions)
        {
            if (!_eligiblePositions.Contains(position) && board.IsWithinBounds(position))
                DrawFrame(geometry.GetCellRect(position), LegalTargetKind.Opposing);
        }

        void DrawFrame(Rect2 tile, LegalTargetKind kind) =>
            overlay.DrawTextureRect(_tileQuad, tile, tile: false, new Color((int)kind / 4f, 0, 0));
    }

    private void DrawProvisionalSelection()
    {
        if (_board is { } board && _selectionOverlay is { } overlay &&
            SelectedPosition is { } position && Size.X > 0 && Size.Y > 0)
        {
            var geometry = GetGeometry(board);
            var tile = geometry.GetCellRect(position);
            if (_territory is not null && TargetKind(position) == LegalTargetKind.Shared)
                tile = tile.Grow(-geometry.CellPitch * 0.115f);
            overlay.DrawTextureRect(_tileQuad, tile, tile: false);
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
                int artworkMask = cell is { Piece: PieceType.Castle, Owner: { } owner }
                    ? CastleTopology.ArtworkMask(position, owner, board.Width, board.Height, cellAt) : 0;
                if (CreatePieceSprite(cell, artworkMask) is { } piece)
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
        var (baseMask, tintMask) = GetWallArtworkMasks(mask);
        if (!_wallArt.TryGetValue(baseMask, out var art))
        {
            art = new FactionPieceArt(
                BoardTerrainArt.GetTexture(TerrainCatalog.AssetDirectory + $"wall-{baseMask}_base.png"),
                BoardTerrainArt.GetTexture(TerrainCatalog.AssetDirectory + $"wall-{tintMask}_mask.png"),
                1f, linear: true);
            _wallArt.Add(baseMask, art);
        }
        return art;
    }

    // Group the two façades by doorway. All south-connected bases share the centered crystal mask.
    internal static (int Base, int Tint) GetWallArtworkMasks(int mask) => mask switch
    {
        1 or 2 or 3 or 8 or 9 => (1, 10),
        10 or 11 => (10, 10),
        _ => (mask, 15),
    };

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

    private BoardGeometry GetGeometry(BoardState board) => new(Size, board.Width, board.Height, BannerReserve, FrameReserve);

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
