namespace CastrumTactics.Domain;

/// <summary>An immutable snapshot of a cell's terrain, piece, owner, and ground health.</summary>
/// <remarks>The default value represents an inaccessible, empty, unowned cell with zero ground health.</remarks>
public readonly record struct CellState
{
    /// <summary>Creates a snapshot after checking terrain, piece, ownership, and health invariants.</summary>
    /// <param name="terrain">The cell's static terrain accessibility.</param>
    /// <param name="piece">The piece occupying the cell, if any.</param>
    /// <param name="owner">The owning player for a knight or castle. Pass null for an empty cell or grave.</param>
    /// <param name="groundHealth">The nonnegative ground health. Inaccessible terrain requires zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">A value is undefined or ground health is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The piece and owner are inconsistent, or an inaccessible cell has a piece or nonzero ground health.
    /// </exception>
    /// <remarks>
    /// Snapshot validation accepts any nonnegative health on playable terrain. In particular, a grave
    /// may have positive health and is permanently unplayable regardless of its health.
    /// </remarks>
    public CellState(TerrainType terrain, PieceType piece, PlayerId? owner, int groundHealth)
    {
        if (!Enum.IsDefined(terrain))
        {
            throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Terrain must be a defined value.");
        }

        if (!Enum.IsDefined(piece))
        {
            throw new ArgumentOutOfRangeException(nameof(piece), piece, "Piece must be a defined value.");
        }

        if (owner is PlayerId player && !Enum.IsDefined(player))
        {
            throw new ArgumentOutOfRangeException(nameof(owner), player, "Owner must identify one of the two players.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(groundHealth);

        if (terrain == TerrainType.Inaccessible)
        {
            if (piece != PieceType.None)
            {
                throw new ArgumentException("Inaccessible terrain cannot contain a piece.", nameof(piece));
            }

            if (groundHealth != 0)
            {
                throw new ArgumentException("Inaccessible terrain must have zero ground health.", nameof(groundHealth));
            }
        }

        bool requiresOwner = piece is PieceType.Knight or PieceType.Castle;
        if (requiresOwner != owner.HasValue)
        {
            throw new ArgumentException("Knights and castles require an owner. Empty cells and graves must be unowned.", nameof(owner));
        }

        Terrain = terrain;
        Piece = piece;
        Owner = owner;
        GroundHealth = groundHealth;
    }

    /// <summary>Gets the fixed terrain accessibility beneath any piece.</summary>
    public TerrainType Terrain { get; }

    /// <summary>Gets the piece occupying the cell, or <see cref="PieceType.None"/> for an empty cell.</summary>
    public PieceType Piece { get; }

    /// <summary>Gets the player who owns the piece, or null for an unowned cell.</summary>
    public PlayerId? Owner { get; }

    /// <summary>Gets the cell's nonnegative ground health, which is zero for inaccessible terrain.</summary>
    public int GroundHealth { get; }

    /// <summary>Gets whether the terrain can participate in gameplay and is free of a grave.</summary>
    /// <remarks>Selection eligibility also depends on the rules of the current match phase.</remarks>
    public bool IsPlayable => Terrain == TerrainType.Playable && Piece != PieceType.Grave;

    internal int GroundHealthAfterBattle => Math.Max(0, GroundHealth - 1);

    internal CellState AfterDirectCollision() => new(Terrain, PieceType.Grave, null, GroundHealthAfterBattle);

    internal CellState WithOwnedPiece(PieceType piece, PlayerId owner) => new(Terrain, piece, owner, GroundHealth);
}
