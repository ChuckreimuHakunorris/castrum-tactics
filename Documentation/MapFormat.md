# Predefined maps

The library contains 100 hand-authored battlefields in `Maps/`, including 29 retained original maps, the 50 Milestone 8B.2 maps, and 21 Milestone 8C.1 replacements. Normal startup draws one uniformly at random. **New Match** offers every battlefield in alphabetical order with a map filter and static preview. **Random Map** draws from the full library when the match starts. The dialog remembers the last confirmed choice and uses the startup override as its initial selection. Cancel leaves the active session and the remembered choice in place. **Restart Draft** and **Restart Match** restore the same battlefield.

## File format

Each JSON file contains one map with a stable lowercase identifier, a display name, and dimensions in cells.

```json
{
  "id": "sample-map",
  "name": "Sample Map",
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

Optional [visual sidecars](VisualArchitecture.md#sidecar-format) live in the application. The 10B registry validates each visual type against the underlying gameplay cell. Map identity, accessibility and draft order still come from the gameplay file.

## Authoring and validation

Use a distinct lowercase identifier with hyphens between words, a nonblank display name, dimensions from 3 through 12 on each axis, and at least two starts. Retain identifiers and layouts for existing maps. Archive retired definitions when recorded walkthroughs or tests depend on them.

Favor open battlefields with both dimensions at least eight. Include square and rectangular layouts. Use sparse rock, irregular clusters, isolated formations, or broken barriers with wide openings to afford expansion, flanking, and multiple attack routes. Reserve narrow passes for local features, and check eight-neighbor traversal because diagonal cells connect. Offer central and interior starts, exposed or sheltered positions, and occasional peripheral and corner starts. Consider how invulnerable starting castles and draft graves immediately block traversal and how castles border the resulting regions. These are authoring guidelines. The loader applies the structural checks below to every entry.

The Knight Support playtesting review retired maps with both axes at most four and layouts dominated by long narrow routes. Long Pasture stays in the catalog because its open four-row board allows several lines of advance. Great Divide and Twin Basins keep a local crossover between sizable maneuvering areas. Island Keep preserves a distinct enclosed center and broad exterior, while Banner Plain leaves room for independent expansion. These are catalog judgments and do not change structural validation or user-created map rules.

`PredefinedMapCatalog` rejects unsupported characters, invalid dimensions, malformed rows, insufficient starts, duplicate identifiers, and duplicate layouts. It constructs an immutable `MapDefinition` and initializes a local session for each entry. A failure names the source file or identifier and stops catalog loading. The automated suite checks that each map has a playable opening under both rulesets and tests legal CPU decisions for both players and all five difficulties. The catalog imposes no competitive fairness restrictions. The [8E battlefield balance audit](BattlefieldBalance.md) records structural and opening indicators for later review. Ranked eligibility remains open.

The 50 expansion maps add 25 squares and 25 rectangles, including 8 × 10, 9 × 12, 11 × 8, and 12 × 10. Each has 5–12 candidate starts. Forty-five have a majority of interior starts, including ten with every start in the interior. Alder Common and Ember Pastures leave formations free to develop across open ground. Quillstone Lawn pairs a symmetric rock layout with central and peripheral draft choices. Flint Crescent puts an irregular central formation between broad flanks, and Granite Orchard leaves several lanes around isolated rock clusters.

All 1,748 expansion start pairs reached Knight Support Main with one castle per faction and graves at the remaining starts. Each player had 2–8 legal first selections in that survey. Another 150 drafts, using opposite sides along each axis and a center-first draft on every new map, also reached Main with several castles per player. The original Island Keep still has eight immediate finishes in 15 distinct start pairs. Starts that become castles and those that become graves both block traversal. Replacing surplus castles with graves only removes regional sources, so a terminal layout with several castles has a terminal witness with one castle per faction.

All 50 new layouts have one eight-connected ground region, including when every candidate start is treated as a blocker. A check for single-cell bottlenecks found a few peripheral pockets of one or two ground cells. Reedcrest Heights moved an isolated corner start into the interior after the first survey found a single legal first move from that castle site. Opening coverage and connectivity describe how drafts reach gameplay. They leave long-run competitive balance and relative castle-site strength to playtesting.

The domain project embeds the JSON files in its assembly. Godot loads the same library from .NET on Windows and Android, including exported builds. The export has no loose map-path dependency.

## Reproduce a match

Use `godot --path . -- --mode local --ruleset classic --map demonstration` with Godot .NET to replay the README walkthrough. In the editor, set **Mode** to **Local Multiplayer**, **Rules** to **Classic**, and **Map Id Override** to a catalog ID. Knight Support is the application default and drafts castles. Leave the map override blank for random startup. The game logs the selected ID and board size. The match header shows the battlefield name and dimensions on Windows and Android.

Engine-independent tests and simulation tools can call `PredefinedMapCatalog.BuiltIn.Get("demonstration")` or enumerate `.Maps`. Pass `new Random(seed)` to `SelectRandom` for a reproducible sequence with the same catalog and .NET implementation.

The full board stays visible with square cells. Large boards use smaller targets, especially in a narrow Android portrait layout. Rotation may give maps with a wide aspect ratio more space.

## Catalog

### Retained original battlefields

| ID                  | Name              | Dimensions | Starts | Rocks |
| ------------------- | ----------------- | ---------- | -----: | ----: |
| `banner-plain`      | Banner Plain      | 12 × 12    |      4 |     0 |
| `blockhouse`        | Blockhouse        | 8 × 7      |      6 |    12 |
| `braided-ridges`    | Braided Ridges    | 9 × 11     |      8 |    16 |
| `crag-cross`        | Crag Cross        | 5 × 7      |      3 |     8 |
| `deep-steps`        | Deep Steps        | 7 × 9      |      6 |    16 |
| `demonstration`     | Demonstration     | 7 × 5      |      5 |     6 |
| `edge-harbor`       | Edge Harbor       | 7 × 6      |      4 |    12 |
| `gate-of-reeds`     | Gate Of Reeds     | 11 × 8     |      6 |    10 |
| `great-divide`      | Great Divide      | 12 × 12    |      6 |    20 |
| `island-keep`       | Island Keep       | 10 × 9     |      6 |    14 |
| `long-pasture`      | Long Pasture      | 8 × 4      |      6 |     0 |
| `market-march`      | Market March      | 9 × 5      |     10 |     4 |
| `open-meadow`       | Open Meadow       | 6 × 5      |      3 |     0 |
| `outer-wall`        | Outer Wall        | 8 × 10     |      6 |    26 |
| `ring-of-stone`     | Ring Of Stone     | 12 × 12    |      8 |    20 |
| `river-bend`        | River Bend        | 5 × 7      |      4 |     5 |
| `saddle-pass`       | Saddle Pass       | 6 × 6      |      4 |     6 |
| `salt-way`          | Salt Way          | 12 × 6     |      6 |    12 |
| `scattered-cairns`  | Scattered Cairns  | 7 × 7      |      6 |     5 |
| `seven-cairns`      | Seven Cairns      | 9 × 9      |      8 |     7 |
| `sixteen-standards` | Sixteen Standards | 12 × 10    |     16 |    12 |
| `stone-islands`     | Stone Islands     | 10 × 7     |      6 |    18 |
| `the-commons`       | The Commons       | 5 × 5      |      7 |     0 |
| `the-crossroads`    | The Crossroads    | 7 × 5      |      4 |     6 |
| `three-fiefs`       | Three Fiefs       | 12 × 9     |      8 |    16 |
| `toll-gate`         | Toll Gate         | 7 × 5      |      4 |     4 |
| `twelve-towers`     | Twelve Towers     | 12 × 12    |     12 |    20 |
| `twin-basins`       | Twin Basins       | 10 × 10    |      8 |    24 |
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

### Milestone 8C.1 replacements

The 21 replacements vary the relationship between rock, interior castle sites, and the perimeter. Their non-start ground is eight-connected even with every start blocked. Removing any one additional ground cell still leaves the remaining ground connected. This measures alternate terrain routes without imposing a rule on other maps.

All 765 distinct start pairs reached Knight Support Main when the other sites became graves, with 3–8 legal first moves per player. Each replacement also passed the three multi-castle draft tests covering horizontal spread, vertical spread, and center-first picks. No replacement duplicates a catalog layout under rotation or reflection. These checks establish opening playability and alternate routes. Competitive balance still depends on playtesting.

| Name | ID | Dimensions | Rocks | Starts | Strategic layout |
| --- | --- | --- | ---: | ---: | --- |
| Bellflower Reach | `bellflower-reach` | 8 × 8 | 6 | 8 | Half-turn rock elbows frame an open diagonal lane and two broad outside routes. |
| Chalkwater Banks | `chalkwater-banks` | 10 × 8 | 9 | 8 | Offset low rock banks leave a broad middle with asymmetric side approaches. |
| Dovetail Green | `dovetail-green` | 9 × 9 | 10 | 7 | Mirrored shelter wings open into a central green with two peripheral flanks. |
| Elder Steppe | `elder-steppe` | 11 × 9 | 4 | 7 | Two short shelves give distinct anchoring options without dividing the open steppe. |
| Farside Orchard | `farside-orchard` | 10 × 10 | 10 | 10 | Four offset groves leave the central crossing and perimeter open. |
| Glassfen Terrace | `glassfen-terrace` | 8 × 11 | 10 | 8 | Offset shelves make inside and outside approaches distinct on a tall field. |
| Harebell Common | `harebell-common` | 8 × 8 | 0 | 8 | An open board tests near and distant castle spacing without terrain gates. |
| Ivory Ford | `ivory-ford` | 12 × 8 | 8 | 10 | Two staggered bars leave a western lane, a central diagonal, and an eastern lane. |
| Kestrel Overlook | `kestrel-overlook` | 9 × 10 | 11 | 9 | Three unequal terraces leave several ways around the central castle sites. |
| Limestone Crescent | `limestone-crescent` | 11 × 11 | 11 | 10 | A broken rim connects the interior to the exterior past its shoulders and southern gap. |
| Marlstone Downs | `marlstone-downs` | 12 × 10 | 13 | 11 | Four unequal outcrops support a central fight or a wider flank between them. |
| Northwind Heath | `northwind-heath` | 10 × 9 | 6 | 8 | A tiny central ridge and four isolated stones retain most of the open field. |
| Oatgrass Rise | `oatgrass-rise` | 8 × 10 | 6 | 6 | Two diagonal rock elbows offer six footholds without closing the middle. |
| Pewter Hollow | `pewter-hollow` | 12 × 12 | 16 | 12 | Several small rock groups create enclosure opportunities around overlapping fronts. |
| Rookery Vale | `rookery-vale` | 9 × 9 | 10 | 8 | Four shelters leave a broad cross through the center and routes around the rim. |
| Saffron Bluffs | `saffron-bluffs` | 10 × 12 | 14 | 10 | Short paired bluffs bracket a central route and two long open flanks. |
| Thistlewater Plain | `thistlewater-plain` | 12 × 9 | 11 | 9 | Asymmetric clusters create separate upper and lower fronts with a broad midline. |
| Umber Step | `umber-step` | 11 × 8 | 8 | 8 | Two opposed stair-steps offer both a short central crossing and open end runs. |
| Verdigris Meadow | `verdigris-meadow` | 10 × 10 | 7 | 10 | Sparse, uneven cover lets interior castle pairs contest the middle or push outward. |
| Wrenfield | `wrenfield` | 8 × 9 | 6 | 8 | Mirrored stones frame lateral approaches while the middle stays crossable. |
| Yarrow Crown | `yarrow-crown` | 12 × 12 | 16 | 12 | Four rock wings around a central block offer several routes and enclosure edges. |

### Retired battlefields

The following exact definitions are archived under `Benchmarks/Reference/Maps/`. Test and benchmark projects embed this fixture collection separately. The application picker, `--map`, and Random Map use only the production catalog. [Historical replay instructions](CpuBenchmarks.md)

| Name | Dimensions | Reason for retirement |
| --- | --- | --- |
| Barrow Ring | 6 × 6 | The inner start has a narrow exit, and most of the outer route wraps around the rock ring. |
| Broken Corner | 4 × 4 | The short distances and split corners force early contact around the rocks. |
| Corner Watch | 3 × 3 | Both starts contest the same small patch of ground almost immediately. |
| Crescent Quarry | 8 × 8 | The rock crescent encloses its inner starts and leaves a thin outer rim for most maneuvering. |
| Four Posts | 4 × 4 | The open board is too small for distinct fronts after the corner castles are placed. |
| Highland Forks | 6 × 10 | Repeated two-cell tracks and a narrow central crossing funnel both approaches. |
| Lantern Lane | 7 × 3 | Two one-cell lanes meet at a single doorway. |
| Little Crossing | 4 × 4 | A small footprint and staggered rocks make the first engagement highly constrained. |
| Long Ravine | 6 × 12 | Long two-cell side strips lead into a short central pocket with few ways around the opposition. |
| Narrow Yard | 3 × 4 | The short board and central rocks limit formation development and flanking. |
| Pocket Meadow | 3 × 3 | The castles start close enough to force the opening into a small, repetitive fight. |
| Quarry Mouth | 6 × 5 | Rocks consume most of the western half, leaving a narrow route between the lower start and the eastern shelf. |
| Ridge Gates | 10 × 6 | Staggered gates link three narrow strips, making gate control dominate expansion. |
| Ring Field | 4 × 4 | The central block leaves a one-cell rim for both players. |
| Serpent Cut | 10 × 8 | Two long walls force a zigzag through side passages between shallow strips. |
| Split Orchard | 7 × 5 | The barrier leaves only one end crossing between two three-cell-wide halves. |
| Stone Center | 3 × 3 | The center rock splits an already tiny field into thin side routes. |
| Three Banners | 3 × 3 | The remaining space after the draft quickly becomes a single engagement. |
| Three Terraces | 6 × 7 | Offset terrace walls make expansion follow alternating openings between shallow shelves. |
| Twin Ledges | 4 × 3 | Rock ledges and four start sites leave little space for future expansion. |
| Watch Road | 4 × 8 | Two long rock pairs repeatedly reduce the board to single-cell edge tracks. |

The [8E battlefield audit](BattlefieldBalance.md) records structural checks, reachable openings and tournament observations for all 100 entries, including follow-up flags for map review.
