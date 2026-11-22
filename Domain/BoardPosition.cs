namespace CastrumTactics.Domain;

/// <summary>Identifies a board cell using coordinates measured from the top-left corner.</summary>
/// <param name="X">The column, increasing to the right from zero.</param>
/// <param name="Y">The row, increasing downward from zero.</param>
/// <remarks>Coordinates may be negative. Each map or board determines which positions are in bounds.</remarks>
public readonly record struct BoardPosition(int X, int Y);
