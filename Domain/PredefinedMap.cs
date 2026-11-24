namespace CastrumTactics.Domain;

/// <summary>A built-in battlefield with a stable identifier and an immutable map definition.</summary>
public sealed class PredefinedMap
{
    internal PredefinedMap(string id, string name, MapDefinition definition)
    {
        Id = id;
        Name = name;
        Definition = definition;
    }

    /// <summary>Gets the lowercase, hyphen-separated identifier used for deterministic lookup.</summary>
    public string Id { get; }

    /// <summary>Gets the human-readable name of the battlefield.</summary>
    public string Name { get; }

    /// <summary>Gets the shared definition used to initialize a session.</summary>
    public MapDefinition Definition { get; }
}
