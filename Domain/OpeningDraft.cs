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

        return new OpeningDraft(_map, Board.WithOpeningRound(playerOne, playerTwo), ResolvedRounds + 1,
            _hasStartingKnights || playerOne != playerTwo);
    }
}
