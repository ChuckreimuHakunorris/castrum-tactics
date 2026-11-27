#nullable enable

using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

internal partial class MapPreview : Control
{
    private static readonly Color Start = new("#ffda83");
    private static readonly Color Grid = new("#293b2a");
    private readonly HashSet<BoardPosition> _starts = [];
    private MapDefinition? _map;
    private BoardVisuals? _visuals;

    internal void ShowMap(MapDefinition? map, BoardVisuals? visuals)
    {
        _map = map;
        _visuals = visuals;
        _starts.Clear();
        if (map is not null) _starts.UnionWith(map.StartingPositions);
        QueueRedraw();
    }

    private Rect2 GetGridBounds() => _map is { } map ? GetGeometry(map).Bounds : default;

    private string GetTileKind(int x, int y) => TileAt(new BoardPosition(x, y)).ToString();

    private string? GetVisualTerrain(int x, int y) => _visuals?.At(new BoardPosition(x, y)).Terrain;

    public override void _Draw()
    {
        if (_map is not { } map || Size.X <= 0 || Size.Y <= 0) return;
        BoardGeometry geometry = GetGeometry(map);
        DrawRect(geometry.Bounds, Grid);
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var position = new BoardPosition(x, y);
                Rect2 tile = geometry.GetCellRect(position);
                TileKind kind = TileAt(position);
                var terrain = map.GetTerrain(position);
                var cell = new CellState(terrain, PieceType.None, null, terrain == TerrainType.Playable ? TerrainCatalog.FullHealth : 0);
                BoardTerrainArt.Draw(this, tile.Grow(-geometry.CellPitch * 0.035f), cell, _visuals?.At(position) ?? default);
                if (kind == TileKind.Start)
                {
                    DrawCircle(tile.GetCenter(), geometry.CellPitch * 0.26f, Grid, antialiased: true);
                    DrawCircle(tile.GetCenter(), geometry.CellPitch * 0.19f, Start, antialiased: true);
                }
            }
    }

    private BoardGeometry GetGeometry(MapDefinition map) => new(Size, map.Width, map.Height);

    private TileKind TileAt(BoardPosition position)
    {
        if (_starts.Contains(position)) return TileKind.Start;
        return _map?.GetTerrain(position) == TerrainType.Inaccessible ? TileKind.Rock : TileKind.Ground;
    }

    private enum TileKind { Ground, Rock, Start }
}
