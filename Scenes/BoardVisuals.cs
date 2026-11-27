#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CastrumTactics.Domain;

internal enum TileVariant { Original, Weathered }

internal readonly record struct TileVisual(TileVariant Variant, bool NorthRockEdge, bool StoneAccent,
    string? Terrain = null, string? ArtVariant = null, string? Prop = null);

internal sealed class BoardVisuals
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly Dictionary<BoardPosition, TileVisual> _tiles;

    private BoardVisuals(Dictionary<BoardPosition, TileVisual> tiles) => _tiles = tiles;

    internal TileVisual At(BoardPosition position) => _tiles.GetValueOrDefault(position);

    internal static BoardVisuals? Load(PredefinedMap map, string? source, Action<string> report)
    {
        if (source is null) return null;

        try
        {
            using var document = JsonDocument.Parse(source);
            RejectDuplicateFields(document.RootElement);
            var descriptor = document.RootElement.Deserialize<Descriptor>(JsonOptions);
            if (descriptor is null || descriptor.Version is not (1 or 2))
                throw new InvalidDataException("Expected visual descriptor version 1 or 2.");
            if (descriptor.Width != map.Definition.Width || descriptor.Height != map.Definition.Height)
                throw new InvalidDataException("Descriptor dimensions differ from the battlefield.");

            var tiles = new Dictionary<BoardPosition, TileVisual>();
            foreach (Tile? tile in descriptor.Tiles ?? throw new InvalidDataException("Expected a tiles array."))
            {
                if (tile is null) throw new InvalidDataException("A tile entry cannot be null.");
                var position = new BoardPosition(tile.X, tile.Y);
                if (!map.Definition.IsWithinBounds(position))
                    throw new InvalidDataException($"Tile {position} is outside the battlefield.");

                var terrain = map.Definition.GetTerrain(position);
                TileVisual visual = descriptor.Version == 1
                    ? ReadLegacy(tile, terrain, map.Id, descriptor.Seed, position)
                    : ReadTerrain(tile, terrain, map.Id, descriptor.Seed, position);
                if (!tiles.TryAdd(position, visual))
                    throw new InvalidDataException($"Duplicate tile at {position}.");
            }
            return new BoardVisuals(tiles);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            report($"Visual sidecar for '{map.Id}': {error.Message} Using map defaults.");
            return null;
        }
    }

    private static TileVisual ReadLegacy(Tile tile, TerrainType terrain, string mapId, int seed, BoardPosition position)
    {
        string surface = TerrainCatalog.DefaultType(terrain);
        if (tile.Terrain is not null || (tile.Surface is not null && tile.Surface != surface))
            throw new InvalidDataException($"Surface '{tile.Surface}' is unsupported for {position} ({surface} terrain).");

        var variant = tile.Variant switch
        {
            "original" => TileVariant.Original,
            "weathered" => TileVariant.Weathered,
            null => (Hash(mapId, seed, position) >> 31) == 0 ? TileVariant.Original : TileVariant.Weathered,
            _ => throw new InvalidDataException($"Unknown variant '{tile.Variant}' at {position}."),
        };
        bool edge = tile.Transition switch
        {
            null => false,
            "north-rock-edge" => true,
            _ => throw new InvalidDataException($"Unknown transition '{tile.Transition}' at {position}."),
        };
        bool accent = tile.Prop switch
        {
            null => false,
            "stone-accent" => true,
            _ => throw new InvalidDataException($"Unknown prop '{tile.Prop}' at {position}."),
        };
        return new TileVisual(variant, edge, accent);
    }

    private static TileVisual ReadTerrain(Tile tile, TerrainType terrain, string mapId, int seed, BoardPosition position)
    {
        if (tile.Surface is not null || tile.Transition is not null)
            throw new InvalidDataException($"Use terrain and a named orientation variant in version 2 at {position}.");
        string type = tile.Terrain ?? TerrainCatalog.DefaultType(terrain);
        if (!TerrainCatalog.Types.TryGetValue(type, out var entry) || entry.Playable != (terrain == TerrainType.Playable))
            throw new InvalidDataException($"Terrain '{type}' is incompatible with {terrain} at {position}.");
        if (tile.Variant is null && entry.Directional)
            throw new InvalidDataException($"Terrain '{type}' requires an explicit orientation variant at {position}.");
        string variant = tile.Variant ?? entry.Variants[Hash(mapId, seed, position) % entry.Variants.Length];
        if (Array.IndexOf(entry.Variants, variant) < 0)
            throw new InvalidDataException($"Unknown variant '{variant}' for '{type}' at {position}.");
        if (tile.Prop is { } prop && !TerrainCatalog.AllowsProp(type, prop))
            throw new InvalidDataException($"Prop '{prop}' is incompatible with '{type}' at {position}.");
        return new TileVisual(TileVariant.Original, false, false, type, variant, tile.Prop);
    }

    internal static string? ReadEmbedded(string mapId)
    {
        using var stream = typeof(BoardVisuals).Assembly.GetManifestResourceStream($"CastrumTactics.VisualMaps.{mapId}.json");
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void RejectDuplicateFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in element.EnumerateObject())
            {
                if (!names.Add(field.Name)) throw new InvalidDataException($"Duplicate field '{field.Name}'.");
                RejectDuplicateFields(field.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                RejectDuplicateFields(child);
        }
    }

    private static uint Hash(string mapId, int seed, BoardPosition position)
    {
        // FNV-1a over the UTF-8 ID and little-endian 32-bit values. Use the high bit to avoid a parity pattern.
        uint hash = 2166136261;
        foreach (byte value in Encoding.UTF8.GetBytes(mapId))
            hash = unchecked((hash ^ value) * 16777619);
        foreach (int value in new[] { seed, position.X, position.Y })
            for (int shift = 0; shift < 32; shift += 8)
                hash = unchecked((hash ^ (byte)(value >>> shift)) * 16777619);
        return hash;
    }

    private sealed class Descriptor
    {
        public required int Version { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public int Seed { get; init; }
        public required Tile?[]? Tiles { get; init; }
    }

    private sealed class Tile
    {
        public required int X { get; init; }
        public required int Y { get; init; }
        public string? Surface { get; init; }
        public string? Terrain { get; init; }
        public string? Variant { get; init; }
        public string? Transition { get; init; }
        public string? Prop { get; init; }
    }
}
