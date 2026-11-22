namespace CastrumTactics.Domain;

/// <summary>Identifies the piece or permanent obstacle occupying a cell.</summary>
public enum PieceType
{
    /// <summary>The cell contains no piece and has no owner.</summary>
    None = 0,

    /// <summary>A player's basic piece.</summary>
    Knight,

    /// <summary>A player's upgraded piece.</summary>
    Castle,

    /// <summary>An unowned obstacle that makes the cell permanently unusable.</summary>
    Grave
}
