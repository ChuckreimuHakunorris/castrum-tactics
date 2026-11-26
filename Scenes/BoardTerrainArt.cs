using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

internal static class BoardTerrainArt
{
    private static readonly Dictionary<string, Texture2D> Textures = [];
    private static readonly Dictionary<(string Type, string Variant, int Damage), Texture2D> Tiles = [];

    internal static string GetTexturePath(CellState cell, TileVisual visual = default)
    {
        var (type, variant, damage) = Key(cell, visual);
        return PathFor(type, variant, damage);
    }

    private static (string Type, string Variant, int Damage) Key(CellState cell, TileVisual visual) =>
        (visual.Terrain ?? TerrainCatalog.DefaultType(cell.Terrain), visual.ArtVariant ?? "plain", TerrainCatalog.Damage(cell));

    internal static string PathFor(string type, string variant, int damage)
    {
        string suffix = damage == 0 ? "" : $"-{damage}";
        return $"{TerrainCatalog.AssetDirectory}{type}-{variant}{suffix}.png";
    }

    internal static Texture2D GetTexture(string path)
    {
        if (!Textures.TryGetValue(path, out Texture2D texture))
        {
            texture = GD.Load<Texture2D>(path);
            Textures.Add(path, texture);
        }
        return texture;
    }

    internal static void Draw(CanvasItem canvas, Rect2 tile, CellState cell, TileVisual visual)
    {
        var key = Key(cell, visual);
        if (!Tiles.TryGetValue(key, out Texture2D texture))
        {
            texture = GetTexture(PathFor(key.Type, key.Variant, key.Damage));
            Tiles.Add(key, texture);
        }

        canvas.DrawTextureRect(texture, tile, tile: false,
            visual.Variant == TileVariant.Weathered ? new Color(0.91f, 0.94f, 0.90f) : Colors.White);
        if (visual.Prop is { } prop)
            canvas.DrawTextureRect(GetTexture(TerrainCatalog.AssetDirectory + $"accent-{prop}.png"), tile, tile: false);

        // Retain the two authored v1 marks on the new default art.
        if (visual.NorthRockEdge || visual.StoneAccent)
        {
            var rock = GetTexture(TerrainCatalog.AssetDirectory + "rock-plain.png");
            if (visual.NorthRockEdge)
                canvas.DrawTextureRect(rock, new Rect2(tile.Position, new Vector2(tile.Size.X, tile.Size.Y * 0.10f)), false);
            if (visual.StoneAccent)
                canvas.DrawTextureRect(rock, new Rect2(tile.Position + tile.Size * 0.76f, tile.Size * 0.15f), false);
        }
    }
}
