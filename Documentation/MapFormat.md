# Predefined maps

The library contains 50 hand-authored battlefields in `Maps/`. Normal startup draws one uniformly at random. The local session retains that definition through the opening, main phase, and final scoring. **Restart Draft** and **Restart Match** restore the same battlefield. **New Match** in Versus CPU draws again unless a map override is active.

## File format

Each JSON file contains one map with a stable lowercase identifier, a display name, and dimensions in cells.

```json
{
  "id": "stone-center",
  "name": "Stone Center",
  "width": 3,
  "height": 3,
  "layout": [".S.", ".#.", ".S."]
}
```

- `.` is playable terrain.
- `#` is inaccessible terrain.
- `S` is playable terrain eligible in the opening draft.
- Coordinates are zero-based. Columns increase from left to right, rows from top to bottom, and terrain grids use `[y, x]` indexing.

Use one row per declared height, with exactly the declared width in characters. Board and session initialization supply pieces, ownership, and starting health.

## Authoring and validation

Use a distinct lowercase identifier with hyphens between words, a nonblank display name, dimensions from 3 through 12 on each axis, and at least two starts. Keep the built-in library at 50 entries by replacing one entry when adding another. Retain identifiers for existing maps so recorded walkthroughs and tests remain reproducible.

Design barriers for eight-neighbor traversal. Diagonally adjacent playable squares remain connected. A straight rock wall with a one-tile gate can create a contested route that a grave closes. Continuous walls and closed rings can deliberately separate regions.

Lantern Lane uses a single tile to link two rows of open ground. Island Keep encloses central starts in a separate region. Banner Plain leaves room for independent expansion, while the starts on Pocket Meadow bring players into contact quickly.

`PredefinedMapCatalog` rejects unsupported characters, invalid dimensions, malformed rows, insufficient starts, duplicate identifiers, and duplicate layouts. It constructs an immutable `MapDefinition` and initializes a local session for each entry. A failure names the source file or identifier and stops catalog loading. The automated suite also looks for an opening on each map that reaches Main by placing a pair of knights and resolving the other starts as collisions. The catalog imposes no competitive fairness restrictions.

The domain project embeds the JSON files in its assembly. Godot loads the same library from .NET on Windows and Android, including exported builds. The export has no loose map-path dependency.

## Reproduce a match

Use `godot --path . -- --mode local --map demonstration` with Godot .NET to replay the README walkthrough. In the editor, set **Mode** to **Local Multiplayer** for the walkthrough and **Map Id Override** to a particular catalog ID. Leave the map override blank for random startup. The game logs the selected ID and board size and shows the display name and dimensions in the desktop window title and phase tooltip.

Engine-independent tests and simulation tools can call `PredefinedMapCatalog.BuiltIn.Get("demonstration")` or enumerate `.Maps`. Pass `new Random(seed)` to `SelectRandom` for a reproducible sequence with the same catalog and .NET implementation.

The full board stays visible with square cells. Large boards use smaller targets, especially in a narrow Android portrait layout. Rotation may give maps with a wide aspect ratio more space.

## Catalog

The counts by longest dimension are 10 maps at 3–4, 15 at 5–7, 15 at 8–10, and 10 at 11–12.

| ID                  | Name              | Dimensions | Starts | Rocks |
| ------------------- | ----------------- | ---------- | -----: | ----: |
| `banner-plain`      | Banner Plain      | 12 × 12    |      4 |     0 |
| `barrow-ring`       | Barrow Ring       | 6 × 6      |      3 |    11 |
| `blockhouse`        | Blockhouse        | 8 × 7      |      6 |    12 |
| `braided-ridges`    | Braided Ridges    | 9 × 11     |      8 |    16 |
| `broken-corner`     | Broken Corner     | 4 × 4      |      3 |     3 |
| `corner-watch`      | Corner Watch      | 3 × 3      |      2 |     2 |
| `crag-cross`        | Crag Cross        | 5 × 7      |      3 |     8 |
| `crescent-quarry`   | Crescent Quarry   | 8 × 8      |      6 |    18 |
| `deep-steps`        | Deep Steps        | 7 × 9      |      6 |    16 |
| `demonstration`     | Demonstration     | 7 × 5      |      5 |     6 |
| `edge-harbor`       | Edge Harbor       | 7 × 6      |      4 |    12 |
| `four-posts`        | Four Posts        | 4 × 4      |      4 |     0 |
| `gate-of-reeds`     | Gate Of Reeds     | 11 × 8     |      6 |    10 |
| `great-divide`      | Great Divide      | 12 × 12    |      6 |    20 |
| `highland-forks`    | Highland Forks    | 6 × 10     |      6 |    20 |
| `island-keep`       | Island Keep       | 10 × 9     |      6 |    14 |
| `lantern-lane`      | Lantern Lane      | 7 × 3      |      4 |     6 |
| `little-crossing`   | Little Crossing   | 4 × 4      |      2 |     3 |
| `long-pasture`      | Long Pasture      | 8 × 4      |      6 |     0 |
| `long-ravine`       | Long Ravine       | 6 × 12     |      6 |    18 |
| `market-march`      | Market March      | 9 × 5      |     10 |     4 |
| `narrow-yard`       | Narrow Yard       | 3 × 4      |      2 |     2 |
| `open-meadow`       | Open Meadow       | 6 × 5      |      3 |     0 |
| `outer-wall`        | Outer Wall        | 8 × 10     |      6 |    26 |
| `pocket-meadow`     | Pocket Meadow     | 3 × 3      |      2 |     0 |
| `quarry-mouth`      | Quarry Mouth      | 6 × 5      |      3 |    10 |
| `ridge-gates`       | Ridge Gates       | 10 × 6     |      6 |    10 |
| `ring-field`        | Ring Field        | 4 × 4      |      2 |     4 |
| `ring-of-stone`     | Ring Of Stone     | 12 × 12    |      8 |    20 |
| `river-bend`        | River Bend        | 5 × 7      |      4 |     5 |
| `saddle-pass`       | Saddle Pass       | 6 × 6      |      4 |     6 |
| `salt-way`          | Salt Way          | 12 × 6     |      6 |    12 |
| `scattered-cairns`  | Scattered Cairns  | 7 × 7      |      6 |     5 |
| `serpent-cut`       | Serpent Cut       | 10 × 8     |      4 |    16 |
| `seven-cairns`      | Seven Cairns      | 9 × 9      |      8 |     7 |
| `sixteen-standards` | Sixteen Standards | 12 × 10    |     16 |    12 |
| `split-orchard`     | Split Orchard     | 7 × 5      |      5 |     4 |
| `stone-center`      | Stone Center      | 3 × 3      |      2 |     1 |
| `stone-islands`     | Stone Islands     | 10 × 7     |      6 |    18 |
| `the-commons`       | The Commons       | 5 × 5      |      7 |     0 |
| `the-crossroads`    | The Crossroads    | 7 × 5      |      4 |     6 |
| `three-banners`     | Three Banners     | 3 × 3      |      3 |     0 |
| `three-fiefs`       | Three Fiefs       | 12 × 9     |      8 |    16 |
| `three-terraces`    | Three Terraces    | 6 × 7      |      3 |    10 |
| `toll-gate`         | Toll Gate         | 7 × 5      |      4 |     4 |
| `twelve-towers`     | Twelve Towers     | 12 × 12    |     12 |    20 |
| `twin-basins`       | Twin Basins       | 10 × 10    |      8 |    24 |
| `twin-ledges`       | Twin Ledges       | 4 × 3      |      4 |     2 |
| `watch-road`        | Watch Road        | 4 × 8      |      4 |     8 |
| `western-cut`       | Western Cut       | 9 × 6      |      4 |    10 |
