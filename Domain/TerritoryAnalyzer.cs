namespace CastrumTactics.Domain;

/// <summary>Classifies eight-connected territory on resolved boards.</summary>
public static class TerritoryAnalyzer
{
    [Flags]
    private enum Owners
    {
        None = 0,
        PlayerOne = 1,
        PlayerTwo = 2
    }

    /// <summary>Classifies each component of empty cells and knights, counting adjacent castles as terminal sources.</summary>
    /// <param name="board">The immutable board to analyze. Knight health does not affect traversal.</param>
    /// <returns>A new immutable territory analysis.</returns>
    /// <exception cref="ArgumentNullException">The board is null.</exception>
    public static TerritoryAnalysis Analyze(BoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);

        var control = new TerritoryControl[board.Height, board.Width];
        var component = new List<BoardPosition>();
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                CellState cell = board.GetCell(position);
                if (CanTraverse(cell))
                {
                    if (control[y, x] == TerritoryControl.Inaccessible)
                    {
                        ClassifyComponent(board, position, control, component);
                    }
                }
                else
                {
                    control[y, x] = cell.Piece switch
                    {
                        PieceType.Castle => Classify(OwnerOf(cell)),
                        PieceType.Grave => TerritoryControl.Neutral,
                        _ => TerritoryControl.Inaccessible
                    };
                }
            }
        }

        return new TerritoryAnalysis(control);
    }

    private static void ClassifyComponent(BoardState board, BoardPosition start, TerritoryControl[,] control,
        List<BoardPosition> component)
    {
        // On traversable cells the default value means unvisited. Mark on enqueue to visit each cell once.
        component.Clear();
        component.Add(start);
        control[start.Y, start.X] = TerritoryControl.Neutral;
        Owners owners = Owners.None;

        for (int i = 0; i < component.Count; i++)
        {
            BoardPosition position = component[i];
            owners |= OwnerOf(board.GetCell(position));
            foreach (BoardPosition neighbor in board.NeighborsOf(position))
            {
                CellState cell = board.GetCell(neighbor);
                if (cell.Piece == PieceType.Castle)
                {
                    owners |= OwnerOf(cell);
                }
                else if (CanTraverse(cell) && control[neighbor.Y, neighbor.X] == TerritoryControl.Inaccessible)
                {
                    control[neighbor.Y, neighbor.X] = TerritoryControl.Neutral;
                    component.Add(neighbor);
                }
            }
        }

        TerritoryControl territory = Classify(owners);
        foreach (BoardPosition position in component)
        {
            control[position.Y, position.X] = territory;
        }
    }

    private static bool CanTraverse(CellState cell) => cell.IsPlayable && cell.Piece != PieceType.Castle;

    private static Owners OwnerOf(CellState cell) => cell.Owner switch
    {
        PlayerId.PlayerOne => Owners.PlayerOne,
        PlayerId.PlayerTwo => Owners.PlayerTwo,
        _ => Owners.None
    };

    private static TerritoryControl Classify(Owners owners) => owners switch
    {
        Owners.None => TerritoryControl.Neutral,
        Owners.PlayerOne => TerritoryControl.SecuredByPlayerOne,
        Owners.PlayerTwo => TerritoryControl.SecuredByPlayerTwo,
        _ => TerritoryControl.Contested
    };
}
