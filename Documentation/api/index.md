# API Reference

DocFX generates this reference from the Godot and domain C# projects. Use the sidebar to browse namespaces and types, and the headings on a type's page to find individual members.

- [CastrumTactics.Domain](xref:CastrumTactics.Domain) contains the board model, beginning with [`MapDefinition`](xref:CastrumTactics.Domain.MapDefinition) and [`BoardState`](xref:CastrumTactics.Domain.BoardState).
- [`TerritoryAnalyzer`](xref:CastrumTactics.Domain.TerritoryAnalyzer) classifies a resolved board. [`MainPhaseState`](xref:CastrumTactics.Domain.MainPhaseState) evaluates the board for legal selections and final scores, and resolves a pair of player-tagged selections into a new board.
- [`IGameSession`](xref:CastrumTactics.Domain.Session.IGameSession) accepts [opening selections](xref:CastrumTactics.Domain.Session.OpeningSelection) and [main-phase commands](xref:CastrumTactics.Domain.Session.MainPhaseCommand), and publishes [`GameSessionSnapshot`](xref:CastrumTactics.Domain.Session.GameSessionSnapshot) updates. [`LocalGameSession`](xref:CastrumTactics.Domain.Session.LocalGameSession) implements local two-player coordination through final scoring.
- The Godot presentation exposes [`Main`](xref:Global.Main) and [`BoardRenderer`](xref:Global.BoardRenderer) in the [global namespace](xref:Global).
