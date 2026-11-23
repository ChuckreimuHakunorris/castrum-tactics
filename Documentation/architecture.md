# Architecture

## Implemented

The Godot project references [CastrumTactics.Domain](xref:CastrumTactics.Domain) as a separate assembly. The Godot project's compile items exclude domain and test source files. The domain library targets .NET 8 and has no Godot or NuGet package dependencies. The xUnit test project exercises both rules and coordination in that assembly.

[`Main`](xref:Global.Main) creates a [`LocalGameSession`](xref:CastrumTactics.Domain.Session.LocalGameSession) for the 7-by-5 demonstration map. The scene uses [`IGameSession`](xref:CastrumTactics.Domain.Session.IGameSession) to issue commands and read state from the opening draft through final scoring. It passes the board, current targets for the active player, and territory analysis to [`BoardRenderer`](xref:Global.BoardRenderer).

`BoardRenderer` is a Godot `Control` that draws [textured terrain and secured territory](ArtDirection.md#8-placeholder-board-art), [knight and castle sprites with faction recoloring](ArtDirection.md#7-implemented-faction-sprites), and neutral gravestones. Opening targets use gold dots. Main-phase targets use gold tile-edge outlines with inward fades, and a brighter pale cyan version marks the provisional selection in either phase. The board stays centered with square cells in its allotted space.

Drawing, sprite placement, indicator placement, and hit testing share `BoardGeometry`, which derives a continuous grid of square tiles from the control's current size. Hit testing rejects the outer margins. At a shared edge, the tile on the right or below receives the hit. The presentation xUnit project tests this geometry, sprite fitting, terrain texture mapping, and the faction palette without launching Godot.

The renderer handles physical left-button and touch presses after GUI input, converts viewport coordinates into control-local coordinates, and ignores Godot's emulated pointer events so one press selects at most once. A hit on a position the session marks eligible moves the outline. Misses preserve it. The outline persists through resize and clears when the view changes players or receives a new snapshot. The session validates every submitted command independently.

- [`BoardPosition`](xref:CastrumTactics.Domain.BoardPosition) uses zero-based coordinates from the top-left. Terrain grids use row and column order, `[y, x]`.
- [`MapDefinition`](xref:CastrumTactics.Domain.MapDefinition) validates and copies the dimensions, terrain, and candidate starting positions. Starting positions describe eligibility and do not place pieces.
- [`BoardState`](xref:CastrumTactics.Domain.BoardState) initializes empty, unowned cells and exposes immutable [`CellState`](xref:CastrumTactics.Domain.CellState) values. Playable ground starts at 3 health. Inaccessible ground carries 0 health and cannot contain pieces. A grave makes playable terrain unusable while leaving the underlying terrain unchanged.
- [`TerrainType`](xref:CastrumTactics.Domain.TerrainType) has two values, playable and inaccessible. Rocks and rivers are visual treatments for inaccessible ground. [`PlayerId`](xref:CastrumTactics.Domain.PlayerId) identifies the player independently of faction color.
- [`TerritoryAnalyzer`](xref:CastrumTactics.Domain.TerritoryAnalyzer) flood-fills each traversable component once using eight-neighbor adjacency. [`MainPhaseState`](xref:CastrumTactics.Domain.MainPhaseState) exposes per-player legal selections, resolves a simultaneous pair, and reports a final score when either player has no legal selections.

## Milestone 4 decisions

- Session coordination lives in the `CastrumTactics.Domain.Session` namespace of the engine-independent assembly. This keeps the rules and board transitions internal and avoids another assembly for the single local coordinator.
- The draft computes eligibility and resolves the pair. A resolved position always stays occupied during the draft, so the board also records which starts are spent. Each non-collision places one knight per player, which makes one successful round sufficient to enter Main once eligibility is exhausted.
- The draft copies the board once per resolved round. A complete snapshot is simple to consume and safe to retain. The tradeoff is a copy of the full cell grid on each resolution. Submissions that are waiting for the opponent reuse the existing board and eligibility list.
- The local session processes commands serially and publishes state synchronously. The same snapshot contract serves both players with submission flags and resolved results. Round identifiers advance across both resolution and restart, while the visible round count resets. Asynchronous remote acknowledgments and transport are deferred.
- `MapDefinition` accepts fewer than two starting positions so incomplete maps can be represented. `LocalGameSession` requires at least two starting positions and rejects an incomplete map before initializing a draft. A valid map whose draft ends in all collisions still supports a restart.

## Milestone 5 decisions

- A main-phase evaluation binds the immutable board, territory grid, and both legal sets. Callers resolve a pair against that evaluation and evaluate the new board for the next turn. This keeps legal choices from different board snapshots from being combined.
- Territory is fully recomputed for each new resolved board. The conservative traversal counts bordering castles as terminal sources and knights at every health. No cache or health-aware reachability is needed for v1.
- Opening and main-phase resolution share the immutable board transition and direct-collision outcome. Each phase keeps responsibility for deciding whether a pair of selections is legal.

## Milestone 6 decisions

- The local session shares command validation, pending selections, round identifiers, and publication across phases. The opening and main-phase rules still own each phase's validation and resolution.
- Opening completion evaluates the existing draft board before publishing Main or Finished. Later pairs resolve against the retained pre-turn evaluation and publish the next evaluation. The final board, territory, and scores stay available for inspection.
- The scene keeps its player toggle and submission controls. Switching players updates the displayed targets and clears the provisional outline. Published snapshots expose submission status without pending coordinates.

## Milestone 6.1 decisions

- The session feeds the last remaining start into the ordinary collision rule and publishes only the resulting stable state. The identifier advances for each resolved round, including the automatic collision.
- Both terminal phases use the same restart command. The new draft restores the original map and clears the previous main-phase evaluation and pending choices. The scene returns to Player One and clears the provisional outline through the ordinary snapshot update.
- The existing scene switches its box container between a vertical stack and a wide layout. Resizing changes container geometry while the session and provisional selection stay in place. Godot 4.7.2 maps Sensor mode to Android's `fullUser` orientation, which lets Android apply the user's rotation preference.

## Milestone 7A decisions

- The board renderer positions child sprites from immutable board snapshots. A different board snapshot rebuilds the knight and castle visuals from piece type and ownership. Player switches and pending submissions can reuse the same board, and resizing repositions the existing sprites using current geometry.
- Knights and castles use the same shader, each with its own base texture, mask, and footprint. Each board renderer reuses one material per piece type and faction so the tint and mask stay specific to that combination.
- The shared presentation palette maps player IDs to sprite colors, territory accents, and player buttons. A piece's tint comes from its physical owner independently of the territory overlay.

## Milestone 7B decisions

- The board draws each terrain rectangle followed by its secured-territory tint. A child canvas at Z index 1 draws translucent tile edges with the indicator shader and a zero-opacity gradient. Piece sprites sit above the grid at Z index 2. Legal and provisional indicators use separate child canvases at Z indices 3 and 4. Godot uses those indices for stacking even when the piece nodes are rebuilt.
- A terrain texture depends on accessibility and ground health. The piece sprite comes from the same snapshot and is chosen independently, so a collision can show a gravestone over ground with health remaining.
- The main-phase legal canvas skips the provisional square so only one edge gradient covers it. Both canvases reuse materials and draw quads that match the tile bounds. The opening legal canvas draws the existing dots.

## Planned

The current scene supports two players sharing a device. CPU inputs and a remote session remain future work. Ranked map validation remains a separate design decision.

The [Game Design Document](GameDesign.md) records the rules and open design decisions. The [Session Architecture](SessionArchitecture.md) describes the authority and presentation boundaries. The [Main-Phase Rules and Architecture](MainPhaseArchitecture.md) documents the domain rules and local integration.
