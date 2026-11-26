using System.Collections.Generic;
using CastrumTactics.Domain;
using Godot;

internal sealed class BoardTerrainArt
{
    private const string AssetDirectory = "res://Assets/Sprites/Terrain/";
    private readonly Dictionary<string, Texture2D> _textures = [];

    internal Texture2D GetTexture(CellState cell) => GetTexture(GetTexturePath(cell));

    internal Texture2D GetRockTexture() => GetTexture(AssetDirectory + "rock.png");

    internal Color GetTint(TileVariant variant) => variant == TileVariant.Weathered
        ? new Color(0.91f, 0.94f, 0.90f)
        : Colors.White;

    private Texture2D GetTexture(string path)
    {
        if (!_textures.TryGetValue(path, out Texture2D texture))
        {
            texture = GD.Load<Texture2D>(path);
            _textures.Add(path, texture);
        }

        return texture;
    }

    internal static string GetTexturePath(CellState cell)
    {
        if (cell.Terrain == TerrainType.Inaccessible)
        {
            return AssetDirectory + "rock.png";
        }

        return cell.GroundHealth switch
        {
            0 => AssetDirectory + "ground_health_0.png",
            1 => AssetDirectory + "ground_health_1.png",
            2 => AssetDirectory + "ground_health_2.png",
            _ => AssetDirectory + "ground_health_3.png",
        };
    }
}
