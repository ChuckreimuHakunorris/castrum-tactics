# API Reference

DocFX generates this reference from the Godot and domain C# projects. Use the sidebar to browse namespaces and types, and the headings on a type's page to find individual members.

- [CastrumTactics.Domain](xref:CastrumTactics.Domain) contains the board model, beginning with [`MapDefinition`](xref:CastrumTactics.Domain.MapDefinition) and [`BoardState`](xref:CastrumTactics.Domain.BoardState).
- [`IGameSession`](xref:CastrumTactics.Domain.Session.IGameSession) accepts opening commands and publishes [`GameSessionSnapshot`](xref:CastrumTactics.Domain.Session.GameSessionSnapshot) updates. [`LocalGameSession`](xref:CastrumTactics.Domain.Session.LocalGameSession) implements local two-player coordination.
- The Godot presentation exposes [`Main`](xref:Global.Main) and [`BoardRenderer`](xref:Global.BoardRenderer) in the [global namespace](xref:Global).
