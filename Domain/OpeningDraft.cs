namespace CastrumTactics.Domain;

internal sealed class OpeningDraft
{
    private readonly MapDefinition _map;
    private readonly bool _hasStartingKnights;

    internal OpeningDraft(MapDefinition map) : this(map, new BoardState(map), 0, false)
    {
    }

    private OpeningDraft(MapDefinition map, BoardState board, int resolvedRounds, bool hasStartingKnights)
    {
        _map = map;
        Board = board;
        ResolvedRounds = resolvedRounds;
        _hasStartingKnights = hasStartingKnights;
        EligiblePositions = Array.AsReadOnly(map.StartingPositions.Where(position =>
        {
            CellState cell = board.GetCell(position);
            // Every resolved position stays occupied throughout the draft.
            return cell.IsPlayable && cell.Piece == PieceType.None;
        }).ToArray());

        Phase = EligiblePositions.Count > 0 ? MatchPhase.Opening
            : hasStartingKnights ? MatchPhase.Main : MatchPhase.OpeningRestartRequired;
    }

    internal BoardState Board { get; }
    internal int ResolvedRounds { get; }
    internal IReadOnlyList<BoardPosition> EligiblePositions { get; }
    internal MatchPhase Phase { get; }

    internal bool CanSelect(BoardPosition position) => EligiblePositions.Contains(position);

    internal OpeningDraft Resolve(BoardPosition playerOne, BoardPosition playerTwo)
    {
        if (!CanSelect(playerOne) || !CanSelect(playerTwo))
        {
            throw new InvalidOperationException("Both selections must be eligible on the pre-round board.");
        }

        CellState first = Board.GetCell(playerOne);
        bool collision = playerOne == playerTwo;
        BoardState next = collision
            ? Board.WithResolvedCells(playerOne, first.AfterDirectCollision())
            : Board.WithResolvedCells(playerOne, first.WithOwnedPiece(PieceType.Knight, PlayerId.PlayerOne),
                (playerTwo, Board.GetCell(playerTwo).WithOwnedPiece(PieceType.Knight, PlayerId.PlayerTwo)));
        return new OpeningDraft(_map, next, ResolvedRounds + 1, _hasStartingKnights || !collision);
    }
}
