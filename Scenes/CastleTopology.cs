using System;
using CastrumTactics.Domain;

internal static class CastleTopology
{
    private const int East = 2;
    private const int South = 4;
    private const int West = 8;
    private static readonly BoardPosition[] Directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    // N/E/S/W are bits 0–3. The occupied cell stays independent of the artwork that crosses its edges.
    internal static int Mask(BoardPosition position, PlayerId owner, int width, int height, Func<BoardPosition, CellState> cellAt)
    {
        int mask = 0;
        for (int i = 0; i < Directions.Length; i++)
        {
            var offset = Directions[i];
            var side = new BoardPosition(position.X + offset.X, position.Y + offset.Y);
            if (HasFriendlyCastle(side, owner, width, height, cellAt))
            {
                mask |= 1 << i;
            }
        }
        return mask;
    }

    internal static int ArtworkMask(BoardPosition position, PlayerId owner, int width, int height,
        Func<BoardPosition, CellState> cellAt)
    {
        int mask = Mask(position, owner, width, height, cellAt);
        if ((mask & South) == 0) return mask;

        // A neighboring façade has no friendly castle immediately south of it. Reuse the
        // pre-composed rampart for that edge while keeping the actual castle connection intact.
        int southRow = position.Y + 1;
        if ((mask & East) != 0 && !HasFriendlyCastle(new(position.X + 1, southRow), owner, width, height, cellAt))
            mask &= ~East;
        if ((mask & West) != 0 && !HasFriendlyCastle(new(position.X - 1, southRow), owner, width, height, cellAt))
            mask &= ~West;

        return mask;
    }

    private static bool HasFriendlyCastle(BoardPosition position, PlayerId owner, int width, int height,
        Func<BoardPosition, CellState> cellAt) =>
        (uint)position.X < (uint)width && (uint)position.Y < (uint)height &&
        cellAt(position) is { Piece: PieceType.Castle } cell && cell.Owner == owner;
}
