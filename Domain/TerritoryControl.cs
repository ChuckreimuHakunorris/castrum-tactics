namespace CastrumTactics.Domain;

/// <summary>Describes strategic control independently of the piece occupying a cell.</summary>
public enum TerritoryControl
{
    /// <summary>Fixed inaccessible terrain. It is outside territorial control.</summary>
    Inaccessible,

    /// <summary>A grave or a playable component that encounters neither player's pieces.</summary>
    Neutral,

    /// <summary>A playable component that encounters pieces from both players.</summary>
    Contested,

    /// <summary>A castle owned by Player One or a component that encounters only that player's pieces.</summary>
    SecuredByPlayerOne,

    /// <summary>A castle owned by Player Two or a component that encounters only that player's pieces.</summary>
    SecuredByPlayerTwo
}
