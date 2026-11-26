# Visual architecture and map sidecars

```text
Gameplay map -> LocalGameSession -> immutable snapshots -> Main -> BoardRenderer
                                     resolved turn -----^       -> score banner
Visual sidecar -> BoardVisuals (validated per map) -------------> terrain drawing
```

The application loads an optional embedded resource from `VisualMaps/<map-id>.json` when Main composes the selected map. A separate sidecar preserves the original catalog files, archived fixtures and benchmark fingerprints. A future map editor can save both documents together. Domain, Benchmarks and AI continue to consume the gameplay map independently. [Gameplay map format](MapFormat.md)

## Sidecar format

This development example uses Open Meadow. No production map ships with a sidecar. Tests inject this path through the same scene composition used by embedded sidecars.

```json
{
  "version": 1,
  "width": 6,
  "height": 5,
  "seed": 19,
  "tiles": [
    { "x": 0, "y": 0, "surface": "grass", "variant": "weathered", "prop": "stone-accent" },
    { "x": 1, "y": 0, "surface": "grass", "transition": "north-rock-edge" }
  ]
}
```

Version, dimensions and the tile array are required. Coordinates are zero-based. Entries are unique. The current surface IDs declare compatibility with the source map (`grass` for playable and `rock` for inaccessible). Accessibility and ground health continue to select the base texture. The placeholder transition and prop reuse the rock texture as a narrow north strip and a small in-cell accent. All variants preserve damage appearance. Missing files and unspecified cells use the existing visuals. An invalid sidecar logs one warning with the map ID and falls back for the entire map. Unknown fields, unsupported IDs, duplicate fields and tile coordinates, mismatched dimensions and out-of-bounds cells are invalid.

An explicit `original` or `weathered` wins. Each listed tile without a variant uses the high bit of 32-bit FNV-1a over the UTF-8 map ID followed by the seed, x and y as little-endian 32-bit integers. The default seed is zero. This happens when the descriptor loads. Resize, rotation and draft restart reuse the descriptor. New Match loads the actual selected map's descriptor, including when the map is chosen at random. No presentation code consumes the match or CPU random stream.

## Layers and transforms

| Order | Owner |
| --- | --- |
| Backing and chrome | The scene background and fit-space score banner. A later frame or shadow belongs outside logical cells. |
| Base, transition, prop, then territory tint | BoardRenderer's tile pass and BoardTerrainArt's cached terrain lookups. Optional decorations stay within the tile. Territory and damage remain visible. |
| Grid and pieces | Existing TileGrid overlay and piece sprites. FactionPieceArt caches materials by owner and fits artwork into the shared cell geometry. |
| Legal and provisional overlays | Existing LegalTargets and ProvisionalSelection overlays and shaders. They keep their current ordering, color and input behavior. |
| Resolved effects | Future render layer. The current observer maintains the latest record without animation or a visible marker. |

BoardGeometry defines shared square cells, bounds, margins, sprite fitting and the right/bottom shared-edge convention. Input converts a viewport point through Godot's inverse canvas transform before testing these cells. The preview has its own fit rectangle using the same geometry and intentionally shows schematic terrain and starts. A later preview can read the same resolved visual descriptors.

10D can apply one board-content transform and its inverse to both drawing and input. Keep the score and controls in fit space, and suppress taps from recognized gestures. 10A uses fit geometry. Tall props will need tile footprints and an explicit occlusion policy before adoption. For castles, derive any seam from neighboring friendly castles on the resolved board. Shape and adjacency conventions belong to 10B's asset review.

Assets stay in their existing `Assets/Sprites` and `Shaders` paths. Keep sidecar IDs independent of filenames. Introduce cached resources for new art by role (terrain, edges, props, pieces, chrome, effects) and preserve their Godot UIDs. Faction textures and alpha masks remain paired. Choose filtering and source dimensions during art review. [Art direction and provenance](ArtDirection.md)

## Legal targets and resolved turns

The renderer receives both public legal sets from the current perspective. During Draft both sides share the eligible starts. During Main the active human or selected local faction determines the first set. Terminal and restart states have empty sets. 10A draws the active set using the existing indicators. One-sided publications do not change the provisional selection because neither board nor legal set has changed. The existing submit action still clears the submitted highlight.

`IGameSession.LatestResolution` publishes both selected positions and the completed snapshot together. A live record retains the pre-resolution snapshot, the submitted round ID and phase, and any forced draft collision at the next round ID. Main also retains the existing ordinary-action board before the support pass. A future effect can classify a capture or upgrade with the two ordinary outcomes and distinguish a direct grave from support removal by comparing that board with the final board. Territory and scores come from the before and after snapshots. The record uses immutable Domain values and is separate from snapshot serialization and hypothetical search results.

One-sided updates retain the previous record without exposing new coordinates. Restart clears it. Main observes only the current snapshot of the currently composed session and uses the originating round within that session for deduplication. Replacement discards the observer and existing worker. 10E may add an effect consumer keyed by this session and round. A future remote implementation can supply the same metadata once it has an authoritative completed turn. [Session architecture](SessionArchitecture.md)
