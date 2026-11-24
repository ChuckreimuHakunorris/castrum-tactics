# Repository guidance

- Add meaningful XML documentation to new or changed public C# APIs. Include parameters, return values, exceptions, and behavioral constraints where applicable.
- Update the affected documentation with any change to a public API or documented behavior. Record decisions that affect the overall design separately from the API reference.
- Follow the [documentation contributor guide](Documentation/contributing.md) when changing the site. Keep generated DocFX output out of version control.
- Use concise implementation comments to explain non-obvious calculations, important design decisions, invariants, and distinct logical phases of longer methods. Prefer comments that explain intent or reasoning rather than restating the code. Avoid unnecessary comments on straightforward operations.
- Follow [Session Architecture](Documentation/SessionArchitecture.md) when implementing gameplay, sessions, player input, AI, or multiplayer. Preserve the separation between presentation, session coordination, and authoritative domain rules.
- Follow [Main-Phase Rules and Architecture](Documentation/MainPhaseArchitecture.md) for the implemented domain rules and local session integration. Keep secured territory separate from physical piece ownership, derive legal moves from territorial classification without circular dependencies, and validate both players against one pre-turn snapshot. Inaccessible terrain scores zero.
- When implementing main-phase rules, update the game design, session/public API docs, test coverage, and any affected architecture articles in the same change. Preserve opening-draft behavior and the common local/remote session direction.
- Follow [Predefined maps](Documentation/MapFormat.md) when editing built-in maps, and keep the catalog summary and validation tests in sync.
- Follow [CPU architecture](Documentation/CpuArchitecture.md) when changing CPU decisions, and keep the engine on the public snapshot boundary.
