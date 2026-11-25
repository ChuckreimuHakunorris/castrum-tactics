using CastrumTactics.Domain;

namespace CastrumTactics.Development;

// Historical layouts live in the test and benchmark assemblies, separate from the application catalog.
internal static class HistoricalBattlefields
{
    private const string ResourcePrefix = "CastrumTactics.HistoricalBattlefields.";
    private static readonly Lazy<PredefinedMapCatalog> Catalog = new(Load);

    internal static IReadOnlyList<PredefinedMap> Maps => Catalog.Value.Maps;

    internal static PredefinedMap Get(string id) => Catalog.Value.Get(id);

    internal static PredefinedMap Resolve(string id) =>
        Maps.FirstOrDefault(map => map.Id == id) ?? PredefinedMapCatalog.BuiltIn.Get(id);

    private static PredefinedMapCatalog Load()
    {
        var assembly = typeof(HistoricalBattlefields).Assembly;
        var documents = assembly.GetManifestResourceNames()
            .Where(resource => resource.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(resource =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
                return (resource[ResourcePrefix.Length..], reader.ReadToEnd());
            });
        return new PredefinedMapCatalog(documents);
    }
}
