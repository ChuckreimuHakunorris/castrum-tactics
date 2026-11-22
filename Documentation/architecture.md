# Architecture

## Implemented

The Godot project references [CastrumTactics.Domain](xref:CastrumTactics.Domain) as a separate assembly. The Godot project's compile items exclude domain and test source files. The domain library targets .NET 8 and has no Godot or NuGet package dependencies. It builds independently, and the xUnit test project references it directly.

When the main scene is ready, [`Main`](xref:Global.Main) creates a [`BoardState`](xref:CastrumTactics.Domain.BoardState) from the 7-by-5 demonstration map and passes the board and the map's candidate starting positions to [`BoardRenderer`](xref:Global.BoardRenderer). The demonstration map is defined separately from the scene's renderer.

`BoardRenderer` is a full-rect Godot `Control` that draws terrain from cell snapshots and can overlay candidate starting positions. It maps columns to horizontal positions and rows to vertical positions. The fit uses the smaller of the available width per column and height per row so that the board stays centered and its cells stay square as the control resizes. The scene contains one renderer for the whole board.

- [`BoardPosition`](xref:CastrumTactics.Domain.BoardPosition) uses zero-based coordinates from the top-left. Terrain grids use row and column order, `[y, x]`.
- [`MapDefinition`](xref:CastrumTactics.Domain.MapDefinition) validates and copies the dimensions, terrain, and candidate starting positions. Starting positions describe eligibility and do not place pieces.
- [`BoardState`](xref:CastrumTactics.Domain.BoardState) initializes empty, unowned cells and exposes them through immutable [`CellState`](xref:CastrumTactics.Domain.CellState) snapshots. Playable ground starts at 3 health. Inaccessible ground carries 0 health and cannot contain pieces. A grave makes playable terrain unusable while leaving the underlying terrain unchanged.
- [`TerrainType`](xref:CastrumTactics.Domain.TerrainType) has two values, playable and inaccessible. Rocks and rivers are visual treatments for inaccessible ground. [`PlayerId`](xref:CastrumTactics.Domain.PlayerId) identifies the player independently of faction color.

## Planned

The domain will add controlled board changes, legal moves, turn resolution, and win conditions. Opening eligibility beyond the map's candidate list and ground health restoration remain design decisions.

The [`CellState`](xref:CastrumTactics.Domain.CellState) constructor accepts any nonnegative health on playable terrain. This includes an owned knight or castle at zero, an empty cell that remains playable at zero, a grave with positive health, and values above the initial health of 3. Before adding state transitions, the rules need to settle collision grave health and which snapshots are reachable in a match. The design already specifies that capture creates a grave when health drops to zero.

Godot scripts and scenes will handle player input and connect the game logic to the UI and platform services. Godot code will pass player commands to the rules and display the resulting state. The separate domain assembly lets the local game, AI, and future multiplayer validation share the rules.

The [Game Design Document](GameDesign.md) records the planned rules and open decisions.
