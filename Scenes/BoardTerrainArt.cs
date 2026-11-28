using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

internal static class BoardTerrainArt
{
    private static readonly Dictionary<string, Texture2D> Textures = [];
    private static readonly Dictionary<(string Type, string Variant, int Damage), Texture2D> Tiles = [];
    private static readonly Color Weathered = new(0.91f, 0.94f, 0.90f);
    private static readonly Color[] GrassWear = [Colors.White, new(0.98f, 0.92f, 0.83f),
        new(0.94f, 0.81f, 0.69f), new(0.87f, 0.70f, 0.58f)];

    internal static string GetTexturePath(CellState cell, TileVisual visual = default)
    {
        if (cell.Terrain == TerrainType.Playable && visual.Appearance is { } appearance)
            return appearance.Base.Texture;
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
        Color tint = visual.Variant == TileVariant.Weathered ? Weathered : Colors.White;
        if (cell.Terrain == TerrainType.Playable && visual.Appearance is { } appearance)
        {
            if (visual.Terrain is null or "grass") tint *= GrassWear[TerrainCatalog.Damage(cell)];
            canvas.DrawTextureRect(GetTexture(appearance.Base.Texture), tile, false, tint);
            if (appearance.Accent is { } accent) DrawAccent(canvas, tile, accent);
        }
        else
        {
            var key = Key(cell, visual);
            if (!Tiles.TryGetValue(key, out Texture2D texture))
            {
                texture = GetTexture(PathFor(key.Type, key.Variant, key.Damage));
                Tiles.Add(key, texture);
            }
            canvas.DrawTextureRect(texture, tile, false, tint);
        }

        if (visual.Prop is { } prop && visual.Appearance?.Accent is null)
            canvas.DrawTextureRect(GetTexture(TerrainCatalog.AssetDirectory + $"accent-{prop}.png"), tile, false);

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

    private static void DrawAccent(CanvasItem canvas, Rect2 tile, TerrainAccentPlacement accent)
    {
        Vector2 center = tile.Position + tile.Size * new Vector2(accent.CenterX, accent.CenterY);
        Vector2 size = tile.Size * new Vector2(accent.Width, accent.Height);
        var texture = GetTexture(accent.Texture);
        if (accent.Mirror)
        {
            canvas.DrawSetTransform(center, 0, new Vector2(-1, 1));
            canvas.DrawTextureRect(texture, new Rect2(-size / 2, size), false);
            canvas.DrawSetTransform(Vector2.Zero);
        }
        else
        {
            canvas.DrawTextureRect(texture, new Rect2(center - size / 2, size), false);
        }
    }
}
