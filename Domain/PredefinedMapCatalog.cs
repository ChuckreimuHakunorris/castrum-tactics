using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.Domain;

/// <summary>Provides the validated built-in map library independently of the game engine.</summary>
public sealed class PredefinedMapCatalog
{
    private const string ResourcePrefix = "CastrumTactics.Maps.";
    private static readonly Lazy<PredefinedMapCatalog> BuiltInCatalog = new(LoadEmbedded);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly Dictionary<string, PredefinedMap> _byId = new(StringComparer.Ordinal);

    /// <summary>Gets the built-in catalog, validating it on first access.</summary>
    /// <exception cref="InvalidDataException">A built-in map is invalid or the library is incomplete.</exception>
    public static PredefinedMapCatalog BuiltIn => BuiltInCatalog.Value;

    /// <summary>Gets all maps in ordinal identifier order, which is also the order used for seeded selection.</summary>
    public IReadOnlyList<PredefinedMap> Maps { get; }

    internal PredefinedMapCatalog(IEnumerable<(string Source, string Json)> documents)
    {
        var layouts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (source, json) in documents)
        {
            var (map, layout) = Parse(json, source);
            if (!_byId.TryAdd(map.Id, map))
            {
                throw new InvalidDataException($"Map '{map.Id}' in '{source}' has a duplicate identifier.");
            }

            if (!layouts.TryAdd(layout, map.Id))
            {
                throw new InvalidDataException($"Map '{map.Id}' duplicates the layout of '{layouts[layout]}'.");
            }

            try
            {
                _ = new LocalGameSession(map.Definition);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException($"Map '{map.Id}' cannot initialize a local session: {ex.Message}", ex);
            }
        }

        Maps = Array.AsReadOnly(_byId.Values.OrderBy(map => map.Id, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Finds a map by its case-sensitive identifier.</summary>
    /// <param name="id">The stable map identifier.</param>
    /// <returns>The requested map.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The identifier is absent from the catalog.</exception>
    public PredefinedMap Get(string id) => _byId.TryGetValue(id, out var map)
        ? map
        : throw new KeyNotFoundException($"No predefined map has identifier '{id}'.");

    /// <summary>Selects one map uniformly from the catalog.</summary>
    /// <param name="random">A random source, or null to use the shared source. Use a seeded instance for reproducible selections.</param>
    /// <returns>The selected catalog entry.</returns>
    public PredefinedMap SelectRandom(Random? random = null) => Maps[(random ?? Random.Shared).Next(Maps.Count)];

    private static PredefinedMapCatalog LoadEmbedded()
    {
        var assembly = typeof(PredefinedMapCatalog).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .ToArray();
        if (resources.Length != 50)
        {
            throw new InvalidDataException($"Expected 50 built-in map resources, found {resources.Length}.");
        }

        return new PredefinedMapCatalog(resources.Select(resource =>
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            return (resource[ResourcePrefix.Length..], reader.ReadToEnd());
        }));
    }

    internal static (PredefinedMap Map, string Layout) Parse(string json, string source)
    {
        MapData data;
        try
        {
            data = JsonSerializer.Deserialize<MapData>(json, JsonOptions)
                ?? throw new JsonException("Expected a map object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid map JSON in '{source}': {ex.Message}", ex);
        }

        InvalidDataException Invalid(string reason) => new($"Map '{data.Id ?? "<missing id>"}' in '{source}': {reason}.");

        if (data.Id is null || !Regex.IsMatch(data.Id, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z"))
        {
            throw Invalid("expected a lowercase, hyphen-separated identifier");
        }

        if (string.IsNullOrWhiteSpace(data.Name))
        {
            throw Invalid("expected a nonempty display name");
        }

        if (data.Width is < 3 or > 12 || data.Height is < 3 or > 12)
        {
            throw Invalid("width and height must each be between 3 and 12");
        }

        if (data.Layout is null || data.Layout.Length != data.Height)
        {
            throw Invalid($"expected {data.Height} layout rows");
        }

        var terrain = new TerrainType[data.Height, data.Width];
        var starts = new List<BoardPosition>();
        for (int y = 0; y < data.Height; y++)
        {
            if (data.Layout[y] is not { } row || row.Length != data.Width)
            {
                throw Invalid($"row {y} must contain exactly {data.Width} characters");
            }

            for (int x = 0; x < data.Width; x++)
            {
                terrain[y, x] = row[x] switch
                {
                    '.' or 'S' => TerrainType.Playable,
                    '#' => TerrainType.Inaccessible,
                    _ => throw Invalid($"unsupported character '{row[x]}' at ({x}, {y})"),
                };
                if (row[x] == 'S')
                {
                    starts.Add(new BoardPosition(x, y));
                }
            }
        }

        if (starts.Count < 2)
        {
            throw Invalid("at least two starting positions are required");
        }

        var definition = new MapDefinition(data.Width, data.Height, terrain, starts);
        return (new PredefinedMap(data.Id, data.Name, definition), string.Join('\n', data.Layout));
    }

    private sealed class MapData
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public string?[]? Layout { get; init; }
    }
}
