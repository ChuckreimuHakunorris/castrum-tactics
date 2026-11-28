#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CastrumTactics.Domain;

internal sealed record TerrainBaseOption(string Id, string Texture, int Weight = 1, bool AllowsAccent = false);
internal sealed record TerrainAccentOption(string Id, string Texture, int Weight, string[] CompatibleBases,
    float Width, float AspectHeight, float MinScale, float MaxScale, float OffsetX, float OffsetY, bool AllowMirror);
internal readonly record struct TerrainAccentPlacement(string Id, string Texture, float Scale, float CenterX,
    float CenterY, float Width, float Height, bool Mirror);
internal readonly record struct TerrainAppearance(TerrainBaseOption Base, TerrainAccentPlacement? Accent);

internal sealed class TerrainVisualPool
{
    private readonly TerrainBaseOption[] _bases;
    private readonly (TerrainAccentOption Option, TerrainBaseOption[] Bases)[] _accents;
    private readonly int _noneWeight;
    private readonly uint _terrainHash;

    internal TerrainVisualPool(string terrain, int noneWeight, TerrainBaseOption[] bases, TerrainAccentOption[] accents)
    {
        _terrainHash = TerrainVisualRandom.HashText(terrain);
        if (noneWeight < 0 || bases.Length == 0 || bases.Any(option => option.Weight <= 0)
            || bases.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count() != bases.Length)
            throw new ArgumentException($"Invalid base options or empty-accent weight for '{terrain}'.");
        if (accents.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count() != accents.Length
            || accents.Any(option => option.Weight < 0) || (long)noneWeight + accents.Sum(option => (long)option.Weight) is <= 0 or > int.MaxValue
            || bases.Sum(option => (long)option.Weight) > int.MaxValue)
            throw new ArgumentException($"Invalid accent options or total weights for '{terrain}'.");

        _bases = (TerrainBaseOption[])bases.Clone();
        _noneWeight = noneWeight;
        _accents = accents.Select(option =>
        {
            var compatible = _bases.Where(item => item.AllowsAccent && option.CompatibleBases.Contains(item.Id)).ToArray();
            if (compatible.Length == 0 || option.CompatibleBases.Any(id => !compatible.Any(item => item.Id == id)))
                throw new ArgumentException($"Accent '{option.Id}' has invalid base compatibility for '{terrain}'.");
            if (!(option.MinScale > 0 && option.MinScale <= option.MaxScale && option.MaxScale <= 1
                && option.Width > 0 && option.Width <= 1 && option.AspectHeight > 0 && option.Width * option.AspectHeight <= 1
                && option.OffsetX is >= 0 and <= 0.5f && option.OffsetY is >= 0 and <= 0.5f))
                throw new ArgumentException($"Invalid placement for accent '{option.Id}' on '{terrain}'.");
            return (option, compatible);
        }).ToArray();
    }

    internal TerrainAppearance Select(uint mapSeed, BoardPosition position, string? baseId = null, string? accentId = null,
        bool suppressAutomaticAccent = false)
    {
        uint tile = TerrainVisualRandom.Tile(mapSeed, position, _terrainHash);
        TerrainBaseOption? authoredBase = baseId is null ? null : Array.Find(_bases, item => item.Id == baseId);
        TerrainAccentOption? accent = null;
        var eligible = _bases;
        if (accentId is not null)
        {
            foreach (var candidate in _accents)
            {
                if (candidate.Option.Id != accentId) continue;
                (accent, eligible) = candidate;
                break;
            }
        }
        else if (!suppressAutomaticAccent)
        {
            int total = _noneWeight;
            foreach (var candidate in _accents)
                if (authoredBase is null || Array.IndexOf(candidate.Bases, authoredBase) >= 0) total += candidate.Option.Weight;

            int roll = TerrainVisualRandom.WeightedIndex(tile, TerrainRandomChannel.Accent, total) - _noneWeight;
            foreach (var candidate in _accents)
            {
                if (roll < 0) break;
                if (authoredBase is not null && Array.IndexOf(candidate.Bases, authoredBase) < 0) continue;
                roll -= candidate.Option.Weight;
                if (roll < 0) (accent, eligible) = candidate;
            }
        }

        if (authoredBase is null || Array.IndexOf(eligible, authoredBase) < 0)
        {
            int total = 0;
            foreach (var option in eligible) total += option.Weight;
            int roll = TerrainVisualRandom.WeightedIndex(tile, TerrainRandomChannel.Base, total);
            foreach (var option in eligible)
            {
                roll -= option.Weight;
                if (roll >= 0) continue;
                authoredBase = option;
                break;
            }
        }

        return new TerrainAppearance(authoredBase!, accent is null ? null : Place(accent, tile));
    }

    private static TerrainAccentPlacement Place(TerrainAccentOption accent, uint tile)
    {
        float scale = accent.MinScale + (accent.MaxScale - accent.MinScale) * TerrainVisualRandom.Unit(tile, TerrainRandomChannel.Scale);
        float width = accent.Width * scale;
        float height = width * accent.AspectHeight;
        float x = Center(TerrainRandomChannel.OffsetX, accent.OffsetX, width);
        float y = Center(TerrainRandomChannel.OffsetY, accent.OffsetY, height);
        bool mirror = accent.AllowMirror && TerrainVisualRandom.Unit(tile, TerrainRandomChannel.Mirror) < 0.5f;
        return new(accent.Id, accent.Texture, scale, x, y, width, height, mirror);

        float Center(TerrainRandomChannel channel, float displacement, float size) =>
            Math.Clamp(0.5f + (2 * TerrainVisualRandom.Unit(tile, channel) - 1) * displacement, size / 2, 1 - size / 2);
    }
}

internal enum TerrainRandomChannel : uint { Accent = 1, Base = 2, Scale = 3, OffsetX = 4, OffsetY = 5, Mirror = 6 }

internal static class TerrainVisualRandom
{
    internal static uint HashText(string text)
    {
        uint hash = 2166136261;
        foreach (byte value in Encoding.UTF8.GetBytes(text)) hash = unchecked((hash ^ value) * 16777619);
        return hash;
    }

    internal static uint MapSeed(string id, int salt) => Append(HashText(id), unchecked((uint)salt));

    internal static uint Tile(uint mapSeed, BoardPosition position, uint terrainHash) =>
        Append(Append(Append(mapSeed, unchecked((uint)position.X)), unchecked((uint)position.Y)), terrainHash);

    internal static uint Sample(uint tile, TerrainRandomChannel channel)
    {
        // MurmurHash3 fmix32 finalizes independently tagged FNV-1a inputs.
        uint value = Append(tile, (uint)channel);
        value = unchecked((value ^ (value >> 16)) * 0x85ebca6b);
        value = unchecked((value ^ (value >> 13)) * 0xc2b2ae35);
        return value ^ (value >> 16);
    }

    internal static int WeightedIndex(uint tile, TerrainRandomChannel channel, int total) =>
        (int)(((ulong)Sample(tile, channel) * (uint)total) >> 32);

    internal static float Unit(uint tile, TerrainRandomChannel channel) => (Sample(tile, channel) >> 8) * (1f / 16777216);

    private static uint Append(uint hash, uint value)
    {
        for (int shift = 0; shift < 32; shift += 8) hash = unchecked((hash ^ (byte)(value >> shift)) * 16777619);
        return hash;
    }
}
