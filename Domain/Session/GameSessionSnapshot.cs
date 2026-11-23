namespace CastrumTactics.Domain.Session;

/// <summary>Public match state shared by both players, with submission status and resolved board results.</summary>
public sealed class GameSessionSnapshot
{
    private readonly bool _playerOneSubmitted;
    private readonly bool _playerTwoSubmitted;

    internal GameSessionSnapshot(OpeningDraft draft, long roundId, bool playerOneSubmitted, bool playerTwoSubmitted)
    {
        Board = draft.Board;
        Phase = draft.Phase;
        EligiblePositions = draft.EligiblePositions;
        ResolvedRounds = draft.ResolvedRounds;
        RoundId = roundId;
        _playerOneSubmitted = playerOneSubmitted;
        _playerTwoSubmitted = playerTwoSubmitted;
    }

    /// <summary>Gets the immutable board after the last resolution, or the initial board.</summary>
    public BoardState Board { get; }

    /// <summary>Gets the current match phase, including whether a restart is required.</summary>
    public MatchPhase Phase { get; }

    /// <summary>Gets the positions either player may still select, including any privately pending choice.</summary>
    public IReadOnlyList<BoardPosition> EligiblePositions { get; }

    /// <summary>Gets the number of rounds resolved in this draft. Resets on restart.</summary>
    public int ResolvedRounds { get; }

    /// <summary>Gets the command identifier, which advances on resolution and restart, including terminal updates.</summary>
    public long RoundId { get; }

    /// <summary>Checks whether the specified player has a pending submission, without revealing its position.</summary>
    /// <param name="player">The participant whose status to read.</param>
    /// <returns>True if the player has submitted in the current unresolved round.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value does not identify either player.</exception>
    public bool HasSubmitted(PlayerId player) => player switch
    {
        PlayerId.PlayerOne => _playerOneSubmitted,
        PlayerId.PlayerTwo => _playerTwoSubmitted,
        _ => throw new ArgumentOutOfRangeException(nameof(player), player, "Unknown player.")
    };
}
