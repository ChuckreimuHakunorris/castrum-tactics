using System;
using CastrumTactics.Domain;

internal static class CastleTopology
{
    private static readonly BoardPosition[] Directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    // N/E/S/W are bits 0–3. The occupied cell stays independent of the artwork that crosses its edges.
    internal static int Mask(BoardPosition position, PlayerId owner, int width, int height, Func<BoardPosition, CellState> cellAt)
    {
        int mask = 0;
        for (int i = 0; i < Directions.Length; i++)
        {
            var offset = Directions[i];
            var side = new BoardPosition(position.X + offset.X, position.Y + offset.Y);
            if ((uint)side.X < (uint)width && (uint)side.Y < (uint)height &&
                cellAt(side) is { Piece: PieceType.Castle } cell && cell.Owner == owner)
            {
                mask |= 1 << i;
            }
        }
        return mask;
    }
}
