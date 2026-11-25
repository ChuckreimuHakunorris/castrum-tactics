# Architecture

## Implemented

The Godot project references [CastrumTactics.Domain](xref:CastrumTactics.Domain) as a separate assembly. The Godot project's compile items exclude domain and test source files. The domain library targets .NET 8 and has no Godot or NuGet package dependencies. The xUnit test project exercises both rules and coordination in that assembly.

[`Main`](xref:Global.Main) creates a [`LocalGameSession`](xref:CastrumTactics.Domain.Session.LocalGameSession) on one of 100 predefined maps. Both input modes use [`IGameSession`](xref:CastrumTactics.Domain.Session.IGameSession) to issue commands and read state from the opening draft through final scoring. The scene passes the board, current targets for the active human, and territory analysis to [`BoardRenderer`](xref:Global.BoardRenderer). [Predefined maps](MapFormat.md)

`BoardRenderer` is a Godot `Control` that draws [textured terrain and secured territory](ArtDirection.md#8-placeholder-board-art), [knight and castle sprites with faction recoloring](ArtDirection.md#7-implemented-faction-sprites), and neutral gravestones. Opening targets use gold dots. Main-phase targets use gold tile-edge outlines with inward fades, and a brighter pale cyan version marks the provisional selection in either phase. The board stays centered with square cells in its allotted space.

Drawing, sprite placement, indicator placement, and hit testing share `BoardGeometry`, which derives a continuous grid of square tiles from the control's current size. Hit testing rejects the outer margins. At a shared edge, the tile on the right or below receives the hit. The presentation xUnit project tests this geometry, sprite fitting, terrain texture mapping, and the faction palette without launching Godot.

The renderer handles physical left-button and touch presses after GUI input, converts viewport coordinates into control-local coordinates, and ignores Godot's emulated pointer events so a physical press is handled once. An eligible square takes the highlight on the first press. A press on the highlighted square asks the scene to submit through the same handler as the button. Misses preserve the highlight. The highlight persists through resize and clears when the view changes players or rounds. The session validates every submitted command independently.

- [`BoardPosition`](xref:CastrumTactics.Domain.BoardPosition) uses zero-based coordinates from the top-left. Terrain grids use row and column order, `[y, x]`.
- [`MapDefinition`](xref:CastrumTactics.Domain.MapDefinition) validates and copies the dimensions, terrain, and candidate starting positions. Starting positions describe eligibility and do not place pieces.
- [`BoardState`](xref:CastrumTactics.Domain.BoardState) initializes empty, unowned cells and exposes immutable [`CellState`](xref:CastrumTactics.Domain.CellState) values. Playable ground starts at 3 health. Inaccessible ground carries 0 health and cannot contain pieces. A grave makes playable terrain unusable while leaving the underlying terrain unchanged.
- [`TerrainType`](xref:CastrumTactics.Domain.TerrainType) has two values, playable and inaccessible. Rocks and rivers are visual treatments for inaccessible ground. [`PlayerId`](xref:CastrumTactics.Domain.PlayerId) identifies the player independently of faction color.
- [`TerritoryAnalyzer`](xref:CastrumTactics.Domain.TerritoryAnalyzer) flood-fills each traversable component once using eight-neighbor adjacency. [`MainPhaseState`](xref:CastrumTactics.Domain.MainPhaseState) exposes per-player legal selections, resolves a simultaneous pair, and reports a final score when either player has no legal selections.

## Milestone 4 decisions

- Session coordination lives in the `CastrumTactics.Domain.Session` namespace of the engine-independent assembly. This keeps the rules and board transitions internal and avoids another assembly for the single local coordinator.
- The draft computes eligibility and resolves the pair. A resolved position always stays occupied during the draft, so the board also records which starts are spent. Each distinct pair places one starting piece per player, which makes one successful round sufficient to reach the main-phase endgame check once eligibility is exhausted.
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

- The board draws each terrain rectangle followed by its secured-territory tint. A child canvas at Z index 1 draws the grid on a single quad sized by board geometry. The grid shader computes pixel coverage so fractional tile edges stay visible on dense boards. Piece sprites sit above the grid at Z index 2. Legal and provisional indicators use separate child canvases at Z indices 3 and 4. Godot uses those indices for stacking even when the piece nodes are rebuilt.
- A terrain texture depends on accessibility and ground health. The piece sprite comes from the same snapshot and is chosen independently, so a collision can show a gravestone over ground with health remaining.
- The main-phase legal canvas skips the provisional square so only one edge gradient covers it. Both canvases reuse materials and draw quads that match the tile bounds. The opening legal canvas draws the existing dots.

## Milestone 7C decisions

- The gameplay scene inherits a shared native Godot theme. Theme variations define control states and visual hierarchy. `Main` applies faction accents from the presentation palette and renders phase, submission flags, feedback, and final scores from the existing session contract.
- A full-width header shows phase and turn context. The match sets the wrapped status and instruction labels' maximum width to the available panel width. Instructions, waiting text, and rejections share a bounded slot whose height accounts for the longest wrap. The slot clips overflow, and the draft controls reserve button space for their widest captions so submitting or revising a selection keeps the panel and board in place. The board expands to fill the remaining area and uses 96% of its limiting dimension. On Android, the layout converts the safe area to stretched canvas coordinates and uses display density to maintain a 48 × 48 dp target for the player and action buttons. Resizing retains the session and provisional selection.
- The reusable [animated background](xref:Global.AnimatedBackground) stays below the gameplay canvas siblings and ignores pointer events. The Compatibility shader calculates lifecycle parameters per vertex and blends bounded, soft lights per fragment. Reduced motion swaps in a separate static shader and avoids the per-frame animation work. See the [interface and background conventions](ArtDirection.md#9-interface-and-typography).
- The sidebar breakpoint reserves the widest player and action captions, plus panel padding and the board gap. It also requires 286 canvas units for the board and checks the aspect ratio of the inset safe area. Portrait groups the actions in one row. The measurements stay independent of the current match phase and container sizes. `StableInstructionLayout` measures the instruction slot only when its available width changes.

## Milestone 7D decisions

- The domain assembly embeds the JSON maps so desktop, Android, and engine-independent simulations share one validated catalog. Catalog constraints apply to the built-in files. `MapDefinition` retains its general-purpose dimension and terrain validation.
- Startup selects once using the catalog's uniform random selection, an explicit user argument, or a scene override. The local session retains the definition for both restart paths. Rendering and resize continue to consume session snapshots.

## Milestone 8A decisions

- [The CPU decision engine](CpuArchitecture.md) lives in a separate .NET assembly and reads immutable public domain snapshots. A single static evaluator ranks legal selections for five provisional profiles with phase-specific sampling. Stateless calls can run concurrently with independent random sources. The caller owns randomness and session submissions, and the session owns simultaneous resolution and pending selections.

## Milestone 8B decisions

- A match configuration fixes input ownership and difficulty independently of match phase. Versus CPU is the startup default. Local Multiplayer keeps its two-human player toggle and uses the same session and battlefield.
- The application coordinator starts one CPU work item per playable round. It owns request cancellation and validates the originating session and round. The engine evaluates snapshots on a worker, and the Godot scene consumes completions from its process callback so commands stay serial and non-reentrant.
- The domain analysis stores secured territory totals alongside the cell classifications. Snapshots and final results share those totals. The renderer reserves up to 36 canvas units for the banner and computes one square-cell geometry for drawing, sprites, hit testing, and banner placement. The display-only banner takes its width from the resulting grid rectangle and discards a long label before compromising that alignment.

## Milestone 8B.1 decisions

- A domain [`RuleSet`](xref:CastrumTactics.Domain.RuleSet) fixes gameplay independently of the input mode and difficulty. Both rulesets share opening eligibility, main-phase selection generation, turn resolution, and final scoring.
- The draft chooses the starting piece type. The main-phase resolver applies Knight Support once against the complete post-action board and keeps the territory analyzer independent of piece removal. The snapshot records the ruleset so future search can evaluate hypothetical pairs with the authoritative resolver.
- New Match is available in either mode. Confirmation replaces the session, and Versus CPU replaces the coordinator with it. In-session restarts keep the existing configuration.

## Milestone 8B.2 decisions

- Knight Support is the application default in both input modes. The scene retains the configured ruleset across New Match and restarts. Development configuration still supports Classic. Domain callers can continue supplying either ruleset.
- New Match owns tentative map and difficulty choices in presentation. The map preview uses the catalog's immutable definition and shared square-cell geometry. Confirmation creates a replacement session and invalidates the prior CPU coordinator. The session retains its original map through restarts, even when the player's next-match preference is Random Map.

## Milestone 8C decisions

- Knight Support drives strategic CPU development and benchmarking. Deprecated Classic keeps its static CPU policy and remains available through development configuration.
- Immutable ruleset definitions separate legal action reach and survival support. The draft, Main legality, and main-phase support pass consume the applicable values. Territory classification remains independent of the CPU's influence graph.
- The public snapshot exposes the domain's immutable Main evaluation and a draft preview. Search advances both players' choices together with the live domain resolver. One engine shares tactical ordering, strategic evaluation, sampling, and bounded search across all five difficulties. The worker continues to submit proposals through the same coordinator.
- Benchmarks use a development-only executable with the original 8B.1 AI sources in a separate assembly. Dot's Probe ranks candidates through that frozen Normal implementation. The application has no reference to the benchmark assembly. [Benchmark report and reproduction](CpuBenchmarks.md)

## Planned

Broader CPU competition and tuning, a remote session, and ranked map validation remain future work.

The [Game Design Document](GameDesign.md) records the rules and open design decisions. The [Session Architecture](SessionArchitecture.md) describes the authority and presentation boundaries. The [Main-Phase Rules and Architecture](MainPhaseArchitecture.md) documents the domain rules and local integration.
