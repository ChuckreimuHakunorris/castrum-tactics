namespace CastrumTactics.Domain;

internal sealed class OpeningDraft
{
    private readonly MapDefinition _map;
    private readonly bool _hasStartingPieces;

    internal OpeningDraft(MapDefinition map, RuleSet ruleSet = RuleSet.Classic)
        : this(map, ruleSet, new BoardState(map), 0, false)
    {
    }

    private OpeningDraft(MapDefinition map, RuleSet ruleSet, BoardState board, int resolvedRounds, bool hasStartingPieces)
    {
        _map = map;
        RuleSet = ruleSet;
        Board = board;
        ResolvedRounds = resolvedRounds;
        _hasStartingPieces = hasStartingPieces;
        EligiblePositions = Array.AsReadOnly(map.StartingPositions.Where(position =>
        {
            CellState cell = board.GetCell(position);
            // Every resolved position stays occupied throughout the draft.
            return cell.IsPlayable && cell.Piece == PieceType.None;
        }).ToArray());

        Phase = EligiblePositions.Count > 0 ? MatchPhase.Opening
            : hasStartingPieces ? MatchPhase.Main : MatchPhase.OpeningRestartRequired;
    }

    internal BoardState Board { get; }
    internal RuleSet RuleSet { get; }
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
        PieceType startingPiece = RuleSet == RuleSet.KnightSupport ? PieceType.Castle : PieceType.Knight;
        BoardState next = collision
            ? Board.WithResolvedCells(playerOne, first.AfterGraveConversion())
            : Board.WithResolvedCells(playerOne, first.WithOwnedPiece(startingPiece, PlayerId.PlayerOne),
                (playerTwo, Board.GetCell(playerTwo).WithOwnedPiece(startingPiece, PlayerId.PlayerTwo)));
        return new OpeningDraft(_map, RuleSet, next, ResolvedRounds + 1, _hasStartingPieces || !collision);
    }
}
