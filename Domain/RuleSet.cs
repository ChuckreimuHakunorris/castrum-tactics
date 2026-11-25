namespace CastrumTactics.Domain;

/// <summary>Specifies the gameplay rules used from draft through final scoring.</summary>
public enum RuleSet
{
    /// <summary>Legacy development rules that start with knights and allow them to survive independently.</summary>
    Classic,

    /// <summary>Starts with castles and requires each knight to have an adjacent friendly piece.</summary>
    KnightSupport
}

/// <summary>Defines authoritative starting pieces, action reach, and survival support.</summary>
/// <remarks>Ranges use Chebyshev distance and exclude the source square. Territorial control is computed separately.</remarks>
public sealed class RuleSetDefinition
{
    private static readonly RuleSetDefinition Classic = new(PieceType.Knight, false, 1, 1, 1, 1);
    private static readonly RuleSetDefinition KnightSupport = new(PieceType.Castle, true, 1, 1, 1, 1);

    internal RuleSetDefinition(PieceType startingPiece, bool requiresKnightSupport, int knightPlacementRange,
        int castlePlacementRange, int knightSupportRange, int castleSupportRange)
    {
        StartingPiece = startingPiece;
        RequiresKnightSupport = requiresKnightSupport;
        KnightPlacementRange = knightPlacementRange;
        CastlePlacementRange = castlePlacementRange;
        KnightSupportRange = knightSupportRange;
        CastleSupportRange = castleSupportRange;
    }

    /// <summary>Gets the piece awarded for a noncolliding draft selection.</summary>
    public PieceType StartingPiece { get; }
    /// <summary>Gets whether isolated knights are eliminated after simultaneous ordinary actions.</summary>
    public bool RequiresKnightSupport { get; }
    /// <summary>Gets the action reach projected by a knight.</summary>
    public int KnightPlacementRange { get; }
    /// <summary>Gets the action reach projected by a castle.</summary>
    public int CastlePlacementRange { get; }
    /// <summary>Gets the survival support reach projected by a knight.</summary>
    public int KnightSupportRange { get; }
    /// <summary>Gets the survival support reach projected by a castle.</summary>
    public int CastleSupportRange { get; }

    /// <summary>Gets the shared immutable definition for an implemented ruleset.</summary>
    /// <param name="ruleSet">The ruleset selected for the match.</param>
    /// <returns>The authoritative rules definition.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The ruleset is undefined.</exception>
    public static RuleSetDefinition Get(RuleSet ruleSet) => ruleSet switch
    {
        RuleSet.Classic => Classic,
        RuleSet.KnightSupport => KnightSupport,
        _ => throw new ArgumentOutOfRangeException(nameof(ruleSet))
    };

    /// <summary>Gets the action reach projected by one piece.</summary>
    /// <param name="source">The piece providing reach.</param>
    /// <returns>The placement range, or zero for a square without a living piece.</returns>
    public int PlacementRange(PieceType source) => source switch
    {
        PieceType.Castle => CastlePlacementRange,
        PieceType.Knight => KnightPlacementRange,
        _ => 0
    };

    /// <summary>Gets the survival support reach projected by one piece.</summary>
    /// <param name="source">The potential supporting piece.</param>
    /// <returns>The support range, or zero for a square without a living piece.</returns>
    public int SurvivalSupportRange(PieceType source) => source switch
    {
        PieceType.Castle => CastleSupportRange,
        PieceType.Knight => KnightSupportRange,
        _ => 0
    };
}
