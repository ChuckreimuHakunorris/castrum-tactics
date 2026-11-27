#nullable enable

using CastrumTactics.Domain;

internal readonly record struct TerritoryFill(int One, int Two, int Claimable)
{
    internal float OneFraction => Claimable == 0 ? 0 : (float)One / Claimable;
    internal float TwoFraction => Claimable == 0 ? 0 : (float)Two / Claimable;

    internal static TerritoryFill From(BoardState board, TerritoryAnalysis? territory)
    {
        if (territory is null) return default;

        int claimable = territory.PlayerOneScore + territory.PlayerTwoScore;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                if (territory.GetControl(new BoardPosition(x, y)) == TerritoryControl.Contested) claimable++;
            }

        return new TerritoryFill(territory.PlayerOneScore, territory.PlayerTwoScore, claimable);
    }
}
