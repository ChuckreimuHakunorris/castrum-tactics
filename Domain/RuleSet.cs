namespace CastrumTactics.Domain;

/// <summary>Specifies the gameplay rules used from draft through final scoring.</summary>
public enum RuleSet
{
    /// <summary>Starts with knights and allows them to survive independently.</summary>
    Classic,

    /// <summary>Starts with castles and requires each knight to have an adjacent friendly piece.</summary>
    KnightSupport
}
