# Repository guidance

- Add meaningful XML documentation to new or changed public C# APIs. Include parameters, return values, exceptions, and behavioral constraints where applicable.
- Update the affected documentation with any change to a public API or documented behavior. Record decisions that affect the overall design separately from the API reference.
- Follow the [documentation contributor guide](Documentation/contributing.md) when changing the site. Keep generated DocFX output out of version control.
- Use concise implementation comments to explain non-obvious calculations, important design decisions, invariants, and distinct logical phases of longer methods. Prefer comments that explain intent or reasoning rather than restating the code. Avoid unnecessary comments on straightforward operations.
- Follow [Session Architecture](Documentation/SessionArchitecture.md) when implementing gameplay, sessions, player input, AI, or multiplayer. Preserve the separation between presentation, session coordination, and authoritative domain rules.
