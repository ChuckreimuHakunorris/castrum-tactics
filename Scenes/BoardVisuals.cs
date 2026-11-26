#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CastrumTactics.Domain;

internal enum TileVariant { Original, Weathered }

internal readonly record struct TileVisual(TileVariant Variant, bool NorthRockEdge, bool StoneAccent);

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
            if (descriptor is null || descriptor.Version != 1)
                throw new InvalidDataException("Expected visual descriptor version 1.");
            if (descriptor.Width != map.Definition.Width || descriptor.Height != map.Definition.Height)
                throw new InvalidDataException("Descriptor dimensions differ from the battlefield.");

            var tiles = new Dictionary<BoardPosition, TileVisual>();
            foreach (Tile? tile in descriptor.Tiles ?? throw new InvalidDataException("Expected a tiles array."))
            {
                if (tile is null) throw new InvalidDataException("A tile entry cannot be null.");
                var position = new BoardPosition(tile.X, tile.Y);
                if (!map.Definition.IsWithinBounds(position))
                    throw new InvalidDataException($"Tile {position} is outside the battlefield.");

                string surface = map.Definition.GetTerrain(position) == TerrainType.Playable ? "grass" : "rock";
                if (tile.Surface is not null && tile.Surface != surface)
                    throw new InvalidDataException($"Surface '{tile.Surface}' is unsupported for {position} ({surface} terrain).");

                var variant = tile.Variant switch
                {
                    "original" => TileVariant.Original,
                    "weathered" => TileVariant.Weathered,
                    null => DeriveVariant(map.Id, descriptor.Seed, position),
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
                if (!tiles.TryAdd(position, new TileVisual(variant, edge, accent)))
                    throw new InvalidDataException($"Duplicate tile at {position}.");
            }
            return new BoardVisuals(tiles);
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            report($"Visual sidecar for '{map.Id}': {error.Message} Using legacy art.");
            return null;
        }
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

    private static TileVariant DeriveVariant(string mapId, int seed, BoardPosition position)
    {
        // FNV-1a over the UTF-8 ID and little-endian 32-bit values. Use the high bit to avoid a parity pattern.
        uint hash = 2166136261;
        foreach (byte value in Encoding.UTF8.GetBytes(mapId))
            hash = unchecked((hash ^ value) * 16777619);
        foreach (int value in new[] { seed, position.X, position.Y })
            for (int shift = 0; shift < 32; shift += 8)
                hash = unchecked((hash ^ (byte)(value >>> shift)) * 16777619);
        return (hash >> 31) == 0 ? TileVariant.Original : TileVariant.Weathered;
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
        public string? Variant { get; init; }
        public string? Transition { get; init; }
        public string? Prop { get; init; }
    }
}
