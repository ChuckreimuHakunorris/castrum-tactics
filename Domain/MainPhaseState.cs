using System.Collections.Frozen;

namespace CastrumTactics.Domain;

/// <summary>Evaluates one resolved board and resolves legal simultaneous choices against it.</summary>
/// <remarks>Evaluate the opening board before the first main-phase turn and evaluate each resulting board again.</remarks>
public sealed class MainPhaseState
{
    private readonly FrozenSet<BoardPosition> _playerOneSelections;
    private readonly FrozenSet<BoardPosition> _playerTwoSelections;

    private MainPhaseState(BoardState board, TerritoryAnalysis territory, FrozenSet<BoardPosition> playerOneSelections,
        FrozenSet<BoardPosition> playerTwoSelections, MainPhaseResult? result)
    {
        Board = board;
        Territory = territory;
        _playerOneSelections = playerOneSelections;
        _playerTwoSelections = playerTwoSelections;
        Result = result;
    }

    /// <summary>Gets the immutable board used for territory, legal selections, and resolution.</summary>
    public BoardState Board { get; }

    /// <summary>Gets the derived strategic control of every cell on this board.</summary>
    public TerritoryAnalysis Territory { get; }

    /// <summary>Gets the final scores and winner, or null while both players can still select.</summary>
    public MainPhaseResult? Result { get; }

    /// <summary>Gets whether either player is out of legal selections and the match has ended.</summary>
    public bool IsFinished => Result.HasValue;

    /// <summary>Analyzes territory, generates legal selections for both players, and scores a finished board.</summary>
    /// <param name="board">The resolved board, including a board from a successful opening draft.</param>
    /// <returns>The immutable evaluation of the supplied board.</returns>
    /// <exception cref="ArgumentNullException">The board is null.</exception>
    public static MainPhaseState Evaluate(BoardState board)
    {
        TerritoryAnalysis territory = TerritoryAnalyzer.Analyze(board);
        var playerOne = new HashSet<BoardPosition>();
        var playerTwo = new HashSet<BoardPosition>();
        int playerOneScore = 0;
        int playerTwoScore = 0;

        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                var position = new BoardPosition(x, y);
                TerritoryControl control = territory.GetControl(position);
                if (control == TerritoryControl.SecuredByPlayerOne)
                {
                    playerOneScore++;
                }
                else if (control == TerritoryControl.SecuredByPlayerTwo)
                {
                    playerTwoScore++;
                }

                CellState cell = board.GetCell(position);
                if (cell.Owner is PlayerId owner)
                {
                    AddLegalSelections(board, territory, position, cell, control,
                        owner == PlayerId.PlayerOne ? playerOne : playerTwo);
                }
            }
        }

        MainPhaseResult? result = playerOne.Count == 0 || playerTwo.Count == 0
            ? new MainPhaseResult(playerOneScore, playerTwoScore)
            : null;
        return new MainPhaseState(board, territory, playerOne.ToFrozenSet(), playerTwo.ToFrozenSet(), result);
    }

    /// <summary>Gets one player's immutable set of legal selections on this board.</summary>
    /// <param name="player">The player whose selections to return.</param>
    /// <returns>The player's legal squares. An empty set means the player cannot act.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The player is undefined.</exception>
    public IReadOnlySet<BoardPosition> GetLegalSelections(PlayerId player) => player switch
    {
        PlayerId.PlayerOne => _playerOneSelections,
        PlayerId.PlayerTwo => _playerTwoSelections,
        _ => throw new ArgumentOutOfRangeException(nameof(player), player, "Unknown player.")
    };

    /// <summary>Resolves a legal selection from each player, supplied in either order.</summary>
    /// <param name="first">The selection from one player.</param>
    /// <param name="second">The selection from the other player.</param>
    /// <returns>A new board with both outcomes applied to the original state.</returns>
    /// <exception cref="InvalidOperationException">The match has already finished.</exception>
    /// <exception cref="ArgumentException">
    /// A player is undefined, the selections come from the same player, or a position is illegal for its player.
    /// </exception>
    public BoardState ResolveTurn(MainPhaseSelection first, MainPhaseSelection second)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("The match has already finished.");
        }

        ValidateSelection(first, nameof(first));
        if (first.Player == second.Player)
        {
            throw new ArgumentException("The second selection must come from the other player.", nameof(second));
        }
        ValidateSelection(second, nameof(second));

        CellState firstCell = Board.GetCell(first.Position);
        if (first.Position == second.Position)
        {
            return Board.WithResolvedCells(first.Position, firstCell.AfterDirectCollision());
        }

        // Derive both ordinary outcomes from the same board, even if one capture destroys the other's source of range.
        return Board.WithResolvedCells(first.Position, ApplyOrdinaryAction(firstCell, first.Player),
            (second.Position, ApplyOrdinaryAction(Board.GetCell(second.Position), second.Player)));
    }

    private static void AddLegalSelections(BoardState board, TerritoryAnalysis territory, BoardPosition position,
        CellState cell, TerritoryControl control, HashSet<BoardPosition> selections)
    {
        if (cell.Piece == PieceType.Knight && control == TerritoryControl.Contested)
        {
            selections.Add(position);
        }

        foreach (BoardPosition neighbor in board.NeighborsOf(position))
        {
            // A piece makes every adjacent traversable component at least secured for its owner.
            // Only contested cells are therefore reachable and eligible after excluding secured territory.
            if (territory.GetControl(neighbor) == TerritoryControl.Contested)
            {
                selections.Add(neighbor);
            }
        }
    }

    private void ValidateSelection(MainPhaseSelection selection, string parameter)
    {
        if (selection.Player is not (PlayerId.PlayerOne or PlayerId.PlayerTwo))
        {
            throw new ArgumentException("The selection must identify one of the two players.", parameter);
        }

        if (!GetLegalSelections(selection.Player).Contains(selection.Position))
        {
            throw new ArgumentException(
                $"Position {selection.Position} is not a legal main-phase selection for {selection.Player}.", parameter);
        }
    }

    private static CellState ApplyOrdinaryAction(CellState cell, PlayerId player)
    {
        if (cell.Piece == PieceType.None)
        {
            return cell.WithOwnedPiece(PieceType.Knight, player);
        }

        if (cell.Owner == player)
        {
            return cell.WithOwnedPiece(PieceType.Castle, player);
        }

        int health = cell.GroundHealthAfterBattle;
        return health == 0 ? new CellState(cell.Terrain, PieceType.Grave, null, health)
            : new CellState(cell.Terrain, PieceType.Knight, player, health);
    }
}
