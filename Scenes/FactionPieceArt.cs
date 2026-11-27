using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

internal sealed class FactionPieceArt(Texture2D texture, Texture2D mask, float cellFootprint, bool linear = false)
{
    private readonly Shader _shader = GD.Load<Shader>("res://Shaders/faction_recolor.gdshader");
    private readonly Dictionary<PlayerId, ShaderMaterial> _materials = [];

    internal float CellFootprint { get; } = cellFootprint;

    internal Sprite2D CreateSprite(PlayerId owner)
    {
        if (!_materials.TryGetValue(owner, out ShaderMaterial material))
        {
            material = new ShaderMaterial { Shader = _shader };
            material.SetShaderParameter("recolor_mask", mask);
            material.SetShaderParameter("recolor_mask_linear", mask);
            material.SetShaderParameter("linear_mask", linear);
            material.SetShaderParameter("faction_color", FactionPalette.GetColor(owner));
            _materials.Add(owner, material);
        }

        return new Sprite2D
        {
            Texture = texture,
            Material = material,
            TextureFilter = linear ? CanvasItem.TextureFilterEnum.Linear : CanvasItem.TextureFilterEnum.Nearest,
            Centered = true,
        };
    }

    internal static void Place(Sprite2D sprite, Rect2 tile, float cellFootprint)
    {
        float scale = GetScale(sprite.Texture.GetSize(), tile.Size, cellFootprint);
        sprite.Visible = scale > 0;
        sprite.Position = tile.GetCenter();
        sprite.Scale = Vector2.One * scale;
    }

    internal static float GetScale(Vector2 textureSize, Vector2 tileSize, float cellFootprint)
    {
        return cellFootprint * Mathf.Min(tileSize.X / textureSize.X, tileSize.Y / textureSize.Y);
    }
}
