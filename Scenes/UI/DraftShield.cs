#nullable enable

using Godot;

internal static class DraftShield
{
    private static readonly Color Rim = new("#593c20");
    private static readonly Color Gold = new("#ffda83");
    private static Texture2D? _texture;

    internal static void Draw(CanvasItem layer, Rect2 tile)
    {
        _texture ??= GD.Load<Texture2D>("res://Assets/UI/territory_shield.svg");
        float size = tile.Size.X * 0.45f;
        Vector2 center = tile.GetCenter();
        layer.DrawTextureRect(_texture, new Rect2(center - Vector2.One * size / 2, Vector2.One * size), false, Rim);
        size *= 0.76f;
        layer.DrawTextureRect(_texture, new Rect2(center - Vector2.One * size / 2, Vector2.One * size), false, Gold);
    }
}
