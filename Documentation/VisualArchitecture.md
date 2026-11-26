# Visual architecture and map sidecars

```text
Gameplay map -> LocalGameSession -> immutable snapshots -> Main -> BoardRenderer
                                     resolved turn -----^       -> score banner
Visual sidecar -> BoardVisuals (validated per map) -------------> terrain drawing
```

The application loads an optional embedded resource from `VisualMaps/<map-id>.json` when Main composes the selected map. A separate sidecar preserves the original catalog files, archived fixtures and benchmark fingerprints. A future map editor can save both documents together. Domain, Benchmarks and AI continue to consume the gameplay map independently. [Gameplay map format](MapFormat.md)

## Sidecar format

Version 2 uses fixed terrain IDs. The `TerrainCatalog` registry owns accessibility, variants, accents and resource roles. Domain and AI read only the gameplay data. A future editor can derive accessibility when saving both files. In 10B, sidecars validate against existing cells.

```json
{
  "version": 2,
  "width": 12,
  "height": 12,
  "seed": 29,
  "tiles": [
    { "x": 5, "y": 4, "terrain": "river", "variant": "ns" },
    { "x": 5, "y": 5, "terrain": "bridge", "variant": "ew" },
    { "x": 4, "y": 9, "terrain": "snow", "prop": "pebbles" }
  ]
}
```

This example fits Great Divide. Grass, sand, snow and bridge require a playable source cell. Rock, bush, forest, mountain, snow-mountain, river, lake, house, town and ruin require inaccessible terrain. Decorative `flowers` and `grass-tuft` fit grass. Small `pebbles` also fit sand and snow. A bridge variant names the direction you walk. River variants name the connected edges. Lake variants name the shore edges, with `open` for water without a shore. See the [art sheets](BoardArtVerification.md#asset-sheets) for the authored vocabulary.

An explicit variant takes precedence. Otherwise the 32-bit FNV-1a hash over the UTF-8 map ID followed by seed, x and y as little-endian 32-bit values chooses among compatible variants. Bridge, river and lake require explicit variants. Missing entries or files use new grass or rock defaults. Version 1 still accepts compatible `surface`, `original` and `weathered`, `north-rock-edge` and `stone-accent`. Resize, restart and rotation preserve visual choices and consume no match RNG.

Unknown fields, duplicate entries, invalid dimensions, out-of-range cells, and incompatible types or variants produce a warning and whole-map fallback. Tests validate the embedded descriptors across the catalog. The picker uses the same intact assets and metadata for terrain and starts.

## Layers and transforms

| Order | Owner |
| --- | --- |
| Backing and chrome | The scene background and fit-space score banner. A later frame or shadow belongs outside logical cells. |
| Base, transition, prop, then territory tint | BoardRenderer's tile pass and BoardTerrainArt's cached terrain lookups. Optional decorations stay within the tile. Territory and damage remain visible. |
| Grid and pieces | Existing TileGrid overlay and piece sprites. FactionPieceArt caches materials by owner and fits artwork into the shared cell geometry. |
| Damage cues | A shared layer above pieces and tint and below legal indicators. Authoritative health selects the surface wear and the number of corner marks. |
| Legal and provisional overlays | Existing LegalTargets and ProvisionalSelection overlays and shaders. They keep their current ordering, color and input behavior. |
| Resolved effects | Future render layer. The current observer maintains the latest record without animation or a visible marker. |

BoardGeometry defines shared square cells, bounds, margins, sprite fitting and the right/bottom shared-edge convention. Input converts a viewport point through Godot's inverse canvas transform before testing these cells. The preview uses its own fit rectangle with the same geometry. It displays intact terrain, accents and starts using the selected map’s visual descriptors.

10D can apply one board-content transform and its inverse to both drawing and input. Keep the score and controls in fit space, and suppress taps from recognized gestures. The current board uses fit geometry. Tall props will need tile footprints and an explicit occlusion policy before adoption. CastleTopology derives N/E/S/W bits from same-owner castles on the final board. Joins cross the visual edge while each castle occupies exactly one logical cell. Mask zero uses the isolated asset.

Assets stay in their existing `Assets/Sprites` and `Shaders` paths. Keep sidecar IDs independent of filenames. Introduce cached resources for new art by role (terrain, edges, props, pieces, chrome, effects) and preserve their Godot UIDs. Faction textures and alpha masks remain paired. The terrain cache loads on demand, with shared linear sampling for the new terrain and aligned base/mask pairs. [Art direction and provenance](ArtDirection.md)

## Legal targets and resolved turns

The renderer receives both public legal sets from the current perspective. During Draft both sides share the eligible starts. During Main the active human or selected local faction determines the first set. Terminal and restart states have empty sets. The board draws the active set using the existing indicators. One-sided publications do not change the provisional selection because neither board nor legal set has changed. The existing submit action still clears the submitted highlight.

`IGameSession.LatestResolution` publishes both selected positions and the completed snapshot together. A live record retains the pre-resolution snapshot, the submitted round ID and phase, and any forced draft collision at the next round ID. Main also retains the existing ordinary-action board before the support pass. A future effect can classify a capture or upgrade with the two ordinary outcomes and distinguish a direct grave from support removal by comparing that board with the final board. Territory and scores come from the before and after snapshots. The record uses immutable Domain values and is separate from snapshot serialization and hypothetical search results.

One-sided updates retain the previous record without exposing new coordinates. Restart clears it. Main observes only the current snapshot of the currently composed session and uses the originating round within that session for deduplication. Replacement discards the observer and existing worker. 10E may add an effect consumer keyed by this session and round. A future remote implementation can supply the same metadata once it has an authoritative completed turn. [Session architecture](SessionArchitecture.md)
