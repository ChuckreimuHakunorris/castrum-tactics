# Architecture

## Implemented

The proof of concept uses a single Godot scene. Its [`Main`](xref:Global.Main) script extends Godot's `Node2D`. When the scene is ready, it updates the on-screen status label and writes a startup message to the log. This verifies that C# runs in the exported game on Windows and Android.

The [CastrumTactics.Domain](xref:CastrumTactics.Domain) library targets .NET 8 and has no Godot or NuGet package dependencies. It builds independently, and the xUnit test project references the domain directly. The Godot scene still runs the startup proof of concept.

- [`BoardPosition`](xref:CastrumTactics.Domain.BoardPosition) uses zero-based coordinates from the top-left. Terrain grids use row and column order, `[y, x]`.
- [`MapDefinition`](xref:CastrumTactics.Domain.MapDefinition) validates and copies the dimensions, terrain, and candidate starting positions. Starting positions describe eligibility and do not place pieces.
- [`BoardState`](xref:CastrumTactics.Domain.BoardState) initializes empty, unowned cells and exposes them through immutable [`CellState`](xref:CastrumTactics.Domain.CellState) snapshots. Playable ground starts at 3 health. Inaccessible ground carries 0 health and cannot contain pieces. A grave makes playable terrain unusable while leaving the underlying terrain unchanged.
- [`TerrainType`](xref:CastrumTactics.Domain.TerrainType) has two values, playable and inaccessible. Rocks and rivers are visual treatments for inaccessible ground. [`PlayerId`](xref:CastrumTactics.Domain.PlayerId) identifies the player independently of faction color.

## Planned

The domain will add controlled board changes, legal moves, turn resolution, and win conditions. Opening eligibility beyond the map's candidate list and ground health restoration remain design decisions.

Godot scripts and scenes will handle player input, render board state, and connect the game logic to the UI and platform services. Godot code will pass player commands to the rules and display the resulting state. Dependencies will flow from Godot integration to the game logic so the rules can be shared by the local game, AI, and future multiplayer validation.

The [Game Design Document](GameDesign.md) records the planned rules and open decisions.
