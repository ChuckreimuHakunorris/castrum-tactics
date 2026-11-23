# Architecture

## Implemented

The Godot project references [CastrumTactics.Domain](xref:CastrumTactics.Domain) as a separate assembly. The Godot project's compile items exclude domain and test source files. The domain library targets .NET 8 and has no Godot or NuGet package dependencies. The xUnit test project exercises both rules and coordination in that assembly.

[`Main`](xref:Global.Main) creates a [`LocalGameSession`](xref:CastrumTactics.Domain.Session.LocalGameSession) for the 7-by-5 demonstration map. The scene then uses [`IGameSession`](xref:CastrumTactics.Domain.Session.IGameSession) to issue commands and read state. It passes the board snapshot and current eligibility to [`BoardRenderer`](xref:Global.BoardRenderer).

`BoardRenderer` is a Godot `Control` that draws terrain in ground-health shades and overlays eligible starting markers, knights, and graves. The board stays centered with square cells in the space above the draft controls.

Drawing and hit testing share `BoardGeometry`, which uses the control's current size and derives each inset tile rectangle. Hit testing rejects the outer margins and the gaps between tiles. Tile rectangles include their top and left edges and exclude their bottom and right edges. The presentation xUnit project tests this geometry without launching Godot.

The renderer handles physical left-button and touch presses after GUI input, converts viewport coordinates into control-local coordinates, and ignores Godot's emulated pointer events so one press selects at most once. A hit on a position the session marks eligible moves the outline. Misses preserve it. The outline persists through resize and clears when the view changes players or receives a new snapshot. The session validates every submitted command independently.

- [`BoardPosition`](xref:CastrumTactics.Domain.BoardPosition) uses zero-based coordinates from the top-left. Terrain grids use row and column order, `[y, x]`.
- [`MapDefinition`](xref:CastrumTactics.Domain.MapDefinition) validates and copies the dimensions, terrain, and candidate starting positions. Starting positions describe eligibility and do not place pieces.
- [`BoardState`](xref:CastrumTactics.Domain.BoardState) initializes empty, unowned cells and exposes immutable [`CellState`](xref:CastrumTactics.Domain.CellState) values. Playable ground starts at 3 health. Inaccessible ground carries 0 health and cannot contain pieces. A grave makes playable terrain unusable while leaving the underlying terrain unchanged.
- [`TerrainType`](xref:CastrumTactics.Domain.TerrainType) has two values, playable and inaccessible. Rocks and rivers are visual treatments for inaccessible ground. [`PlayerId`](xref:CastrumTactics.Domain.PlayerId) identifies the player independently of faction color.

## Milestone 4 decisions

- Session coordination lives in the `CastrumTactics.Domain.Session` namespace of the engine-independent assembly. This keeps the rules and board transitions internal and avoids another assembly for the single local coordinator.
- The draft computes eligibility and resolves the pair. A resolved position always stays occupied during the draft, so the board also records which starts are spent. Each non-collision places one knight per player, which makes one successful round sufficient to enter Main once eligibility is exhausted.
- The draft copies the board once per resolved round. A complete snapshot is simple to consume and safe to retain. The tradeoff is a copy of the full cell grid on each resolution. Submissions that are waiting for the opponent reuse the existing board and eligibility list.
- The local session processes commands serially and publishes state synchronously. The same snapshot contract serves both players with submission flags and resolved results. Round identifiers advance across both resolution and restart, while the visible round count resets. Asynchronous remote acknowledgments and transport are deferred.
- `MapDefinition` accepts fewer than two starting positions so incomplete maps can be represented. `LocalGameSession` requires at least two starting positions and rejects an incomplete map before initializing a draft. A valid map whose draft ends in all collisions still supports a restart.

## Planned

The current Main phase status reports successful opening completion. Main-phase selections and combat, scoring, CPU inputs, and a remote session remain future work. Ranked map validation remains a separate design decision.

The [Game Design Document](GameDesign.md) records the rules and open design decisions. The [Session Architecture](SessionArchitecture.md) describes the authority and presentation boundaries.
