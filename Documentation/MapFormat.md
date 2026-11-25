# Predefined maps

The library contains 100 hand-authored battlefields in `Maps/`, including the original 50. Normal startup draws one uniformly at random. **New Match** offers every battlefield in alphabetical order with a map filter and static preview. **Random Map** draws from the full library when the match starts. The dialog remembers the last confirmed choice and uses the startup override as its initial selection. Cancel leaves the active session and the remembered choice in place. **Restart Draft** and **Restart Match** restore the same battlefield.

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

Use a distinct lowercase identifier with hyphens between words, a nonblank display name, dimensions from 3 through 12 on each axis, and at least two starts. Retain identifiers and layouts for existing maps so recorded walkthroughs and tests remain reproducible.

Favor open battlefields with both dimensions at least eight. Include square and rectangular layouts. Use sparse rock, irregular clusters, isolated formations, or broken barriers with wide openings to afford expansion, flanking, and multiple attack routes. Reserve narrow passes for local features, and check eight-neighbor traversal because diagonal cells connect. Offer central and interior starts, exposed or sheltered positions, and occasional peripheral and corner starts. Consider how invulnerable starting castles and draft graves immediately block traversal and how castles border the resulting regions. These are authoring guidelines. The loader applies the structural checks below to every entry.

Lantern Lane uses a single tile to link two rows of open ground. Island Keep encloses central starts in a separate region. Banner Plain leaves room for independent expansion, while the starts on Pocket Meadow bring players into contact quickly.

`PredefinedMapCatalog` rejects unsupported characters, invalid dimensions, malformed rows, insufficient starts, duplicate identifiers, and duplicate layouts. It constructs an immutable `MapDefinition` and initializes a local session for each entry. A failure names the source file or identifier and stops catalog loading. The automated suite checks that each map has a playable opening under both rulesets and tests legal CPU decisions for both players and all five difficulties. The catalog imposes no competitive fairness restrictions.

The 50 expansion maps add 25 squares and 25 rectangles, including 8 × 10, 9 × 12, 11 × 8, and 12 × 10. Each has 5–12 candidate starts. Forty-five have a majority of interior starts, including ten with every start in the interior. Alder Common and Ember Pastures leave formations free to develop across open ground. Quillstone Lawn pairs a symmetric rock layout with central and peripheral draft choices. Flint Crescent puts an irregular central formation between broad flanks, and Granite Orchard leaves several lanes around isolated rock clusters.

All 1,748 expansion start pairs reached Knight Support Main with one castle per faction and graves at the remaining starts. Each player had 2–8 legal first selections in that survey. Another 150 drafts, using opposite sides along each axis and a center-first draft on every new map, also reached Main with several castles per player. The original Island Keep still has eight immediate finishes in 15 distinct start pairs. Starts that become castles and those that become graves both block traversal. Replacing surplus castles with graves only removes regional sources, so a terminal layout with several castles has a terminal witness with one castle per faction.

All 50 new layouts have one eight-connected ground region, including when every candidate start is treated as a blocker. A check for single-cell bottlenecks found a few peripheral pockets of one or two ground cells. Reedcrest Heights moved an isolated corner start into the interior after the first survey found a single legal first move from that castle site. Opening coverage and connectivity describe how drafts reach gameplay. They leave long-run competitive balance and relative castle-site strength to playtesting.

The domain project embeds the JSON files in its assembly. Godot loads the same library from .NET on Windows and Android, including exported builds. The export has no loose map-path dependency.

## Reproduce a match

Use `godot --path . -- --mode local --ruleset classic --map demonstration` with Godot .NET to replay the README walkthrough. In the editor, set **Mode** to **Local Multiplayer**, **Rules** to **Classic**, and **Map Id Override** to a catalog ID. Knight Support is the application default and drafts castles. Leave the map override blank for random startup. The game logs the selected ID and board size. The match header shows the battlefield name and dimensions on Windows and Android.

Engine-independent tests and simulation tools can call `PredefinedMapCatalog.BuiltIn.Get("demonstration")` or enumerate `.Maps`. Pass `new Random(seed)` to `SelectRandom` for a reproducible sequence with the same catalog and .NET implementation.

The full board stays visible with square cells. Large boards use smaller targets, especially in a narrow Android portrait layout. Rotation may give maps with a wide aspect ratio more space.

## Catalog

### Original battlefields

The original counts by longest dimension are 10 maps at 3–4, 15 at 5–7, 15 at 8–10, and 10 at 11–12.

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

### Milestone 8B.2 battlefields

| Name | ID | Dimensions | Rocks | Starts |
| --- | --- | --- | ---: | ---: |
| Alder Common | `alder-common` | 8 × 8 | 0 | 5 |
| Amber Fallow | `amber-fallow` | 9 × 12 | 11 | 9 |
| Ashen Crown | `ashen-crown` | 9 × 9 | 8 | 8 |
| Birch Expanse | `birch-expanse` | 10 × 10 | 5 | 8 |
| Briar Commons | `briar-commons` | 9 × 9 | 5 | 7 |
| Broadshield Field | `broadshield-field` | 12 × 10 | 12 | 11 |
| Broken Standard | `broken-standard` | 11 × 8 | 10 | 9 |
| Bronze Paddock | `bronze-paddock` | 8 × 10 | 8 | 8 |
| Broomfield | `broomfield` | 10 × 8 | 7 | 8 |
| Cinder Fen | `cinder-fen` | 12 × 9 | 12 | 9 |
| Copper Bluffs | `copper-bluffs` | 9 × 9 | 11 | 6 |
| Corvid Park | `corvid-park` | 10 × 8 | 9 | 7 |
| Crownfall Basin | `crownfall-basin` | 10 × 10 | 13 | 7 |
| Dawnstone Green | `dawnstone-green` | 9 × 8 | 7 | 7 |
| Eider Crossing | `eider-crossing` | 8 × 10 | 10 | 8 |
| Ember Pastures | `ember-pastures` | 10 × 10 | 0 | 7 |
| Flint Crescent | `flint-crescent` | 10 × 10 | 11 | 8 |
| Foxglove Field | `foxglove-field` | 8 × 8 | 4 | 6 |
| Golden Weald | `golden-weald` | 9 × 9 | 0 | 8 |
| Gorse Canopy | `gorse-canopy` | 10 × 12 | 14 | 12 |
| Granite Orchard | `granite-orchard` | 12 × 10 | 14 | 10 |
| Greystone Fens | `greystone-fens` | 9 × 12 | 11 | 9 |
| Hartwood Meadow | `hartwood-meadow` | 8 × 9 | 8 | 7 |
| Hazelflint Vale | `hazelflint-vale` | 12 × 9 | 11 | 9 |
| Honeystone Green | `honeystone-green` | 10 × 10 | 8 | 8 |
| Ironwood Vale | `ironwood-vale` | 11 × 11 | 13 | 9 |
| Juniper March | `juniper-march` | 8 × 11 | 11 | 9 |
| Larch Watch | `larch-watch` | 9 × 9 | 9 | 9 |
| Laurel Plateau | `laurel-plateau` | 11 × 11 | 10 | 9 |
| Linnet Plain | `linnet-plain` | 11 × 8 | 4 | 9 |
| Millstone Downs | `millstone-downs` | 8 × 8 | 7 | 6 |
| Mossglass Plain | `mossglass-plain` | 11 × 11 | 6 | 8 |
| Orchard of Cairns | `orchard-of-cairns` | 11 × 11 | 15 | 10 |
| Osprey Heights | `osprey-heights` | 11 × 11 | 12 | 10 |
| Pale Flintmoor | `pale-flintmoor` | 12 × 12 | 16 | 12 |
| Plover Heath | `plover-heath` | 9 × 12 | 0 | 10 |
| Quillstone Lawn | `quillstone-lawn` | 12 × 12 | 4 | 8 |
| Rainmere Rise | `rainmere-rise` | 8 × 10 | 6 | 9 |
| Reedcrest Heights | `reedcrest-heights` | 10 × 12 | 15 | 12 |
| Roselight Moor | `roselight-moor` | 12 × 10 | 9 | 10 |
| Rowan Glade | `rowan-glade` | 8 × 8 | 8 | 5 |
| Sable Knolls | `sable-knolls` | 8 × 8 | 9 | 7 |
| Sentinel Pasture | `sentinel-pasture` | 11 × 10 | 15 | 11 |
| Silver Bracken | `silver-bracken` | 12 × 12 | 13 | 10 |
| Snowmelt Green | `snowmelt-green` | 8 × 11 | 4 | 8 |
| Summer Reach | `summer-reach` | 12 × 12 | 7 | 12 |
| Ternfall Park | `ternfall-park` | 10 × 8 | 8 | 8 |
| Vesper Barrows | `vesper-barrows` | 12 × 12 | 19 | 12 |
| Westerfold Reach | `westerfold-reach` | 12 × 10 | 13 | 12 |
| Whitehart Plain | `whitehart-plain` | 12 × 9 | 4 | 8 |
