#nullable enable

using System;
using System.Collections.Generic;
using CastrumTactics.Domain;

internal static class TerrainCatalog
{
    internal const string AssetDirectory = "res://Assets/Sprites/Board/";
    internal const int FullHealth = 3;
    internal const int DefaultPoolVersion = 1;

    // Keep published pools intact. New artwork can opt in through a new ID.
    private static readonly IReadOnlyDictionary<(string Terrain, int Version), TerrainVisualPool> VariationPools =
        new Dictionary<(string, int), TerrainVisualPool>
        {
            [("grass", 1)] = new("grass", 75,
                [new("grass_base_01", AssetDirectory + "grass_base_01.png", AllowsAccent: true),
                 new("grass_base_02", AssetDirectory + "grass_base_02.png"),
                 new("grass_base_03", AssetDirectory + "grass_base_03.png"),
                 new("grass_base_04", AssetDirectory + "grass_base_04.png")],
                [new("flowers", AssetDirectory + "grass-accent-flowers.png", 15, ["grass_base_01"],
                     0.48f, 91f / 128, 0.65f, 1, 0.2f, 0.2f, AllowMirror: true),
                 new("pebbles", AssetDirectory + "grass-accent-pebbles.png", 10, ["grass_base_01"],
                     0.38f, 94f / 128, 0.65f, 1, 0.2f, 0.2f, AllowMirror: true)]),
        };

    internal static TerrainVisualPool? VariationPool(string terrain, int version) =>
        VariationPools.GetValueOrDefault((terrain, version));

    // These IDs describe the artwork and its permitted pairing with the gameplay map.
    internal static readonly IReadOnlyDictionary<string, TerrainArtType> Types =
        new Dictionary<string, TerrainArtType>(StringComparer.Ordinal)
        {
            ["grass"] = new(true, ["plain", "tufts", "flowers", "pebbles"]),
            ["sand"] = new(true, ["plain", "pebbles"]),
            ["snow"] = new(true, ["plain", "pebbles"]),
            ["bridge"] = new(true, ["ew", "ns"], Directional: true),
            ["rock"] = new(false, ["plain", "cluster"]),
            ["bush"] = new(false, ["plain", "berries"]),
            ["forest"] = new(false, ["single", "cluster"]),
            ["mountain"] = new(false, ["plain", "ridge"]),
            ["snow-mountain"] = new(false, ["plain", "ridge"]),
            ["river"] = new(false, ["ew", "ns", "ne", "es", "sw", "wn", "n", "e", "s", "w"], Directional: true),
            ["lake"] = new(false, ["open", "n", "e", "s", "w", "ne", "es", "sw", "wn"], Directional: true),
            ["house"] = new(false, ["plain"]),
            ["town"] = new(false, ["plain"]),
            ["ruin"] = new(false, ["plain"]),
        };

    internal static string DefaultType(TerrainType terrain) => terrain == TerrainType.Playable ? "grass" : "rock";

    internal static bool AllowsProp(string type, string prop) => prop switch
    {
        "grass-tuft" or "flowers" => type == "grass",
        "pebbles" => type is "grass" or "sand" or "snow",
        _ => false,
    };

    internal static int Damage(CellState cell) => cell.Terrain == TerrainType.Inaccessible
        ? 0
        : FullHealth - Math.Min(cell.GroundHealth, FullHealth);

    internal static int DamageMarkerCount(CellState cell) =>
        cell.Piece is PieceType.None or PieceType.Knight ? Damage(cell) : 0;
}

internal sealed record TerrainArtType(bool Playable, string[] Variants, bool Directional = false);
