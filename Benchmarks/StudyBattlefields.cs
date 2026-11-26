using System.Text.Json;
using CastrumTactics.Domain;

internal static class StudyBattlefields
{
    private static readonly Dictionary<string, PredefinedMap> Maps = Build();

    internal static PredefinedMap Resolve(string id) => Maps.TryGetValue(id, out var map)
        ? map : CastrumTactics.Development.HistoricalBattlefields.Resolve(id);

    private static Dictionary<string, PredefinedMap> Build()
    {
        var maps = new Dictionary<string, PredefinedMap>(StringComparer.Ordinal);
        foreach (int size in new[] { 4, 6, 8, 10, 12 })
        foreach (bool obstructed in new[] { false, true })
        {
            string id = $"study-{(obstructed ? "block" : "open")}-{size}";
            var layout = Enumerable.Range(0, size).Select(y => new string(Enumerable.Range(0, size)
                .Select(x => Cell(x, y)).ToArray())).ToArray();
            var (map, _) = PredefinedMapCatalog.Parse(JsonSerializer.Serialize(new
            {
                id, name = id, width = size, height = size, layout
            }), id);
            maps.Add(id, map);

            char Cell(int x, int y)
            {
                // Keep the same four corner starts. The central block leaves symmetric outer routes.
                if ((x == 0 || x == size - 1) && (y == 0 || y == size - 1)) return 'S';
                if (!obstructed) return '.';
                int radius = Math.Max(1, size / 6);
                bool Center(int i) => i >= size / 2 - radius && i < size / 2 + radius;
                return Center(x) && Center(y) ? '#' : '.';
            }
        }
        return maps;
    }
}
