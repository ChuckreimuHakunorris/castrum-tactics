using System.Collections.Frozen;

namespace CastrumTactics.Domain.Session;

/// <summary>Public match state shared by both players, with submission status and resolved board results.</summary>
public sealed class GameSessionSnapshot
{
    private readonly bool _playerOneSubmitted;
    private readonly bool _playerTwoSubmitted;
    private readonly MainPhaseState? _mainPhase;

    internal GameSessionSnapshot(OpeningDraft draft, MainPhaseState? mainPhase, int resolvedMainTurns, long roundId,
        bool playerOneSubmitted, bool playerTwoSubmitted)
    {
        _mainPhase = mainPhase;
        Board = mainPhase?.Board ?? draft.Board;
        Phase = mainPhase?.IsFinished == true ? MatchPhase.Finished : draft.Phase;
        EligiblePositions = draft.EligiblePositions;
        ResolvedRounds = draft.ResolvedRounds;
        ResolvedMainTurns = resolvedMainTurns;
        RoundId = roundId;
        _playerOneSubmitted = playerOneSubmitted;
        _playerTwoSubmitted = playerTwoSubmitted;
    }

    /// <summary>Gets the immutable board after the last resolution, or the initial board.</summary>
    public BoardState Board { get; }

    /// <summary>Gets the current match phase, including whether a restart is required.</summary>
    public MatchPhase Phase { get; }

    /// <summary>Gets the shared eligible opening positions, including any privately pending choice. Empty outside Opening.</summary>
    public IReadOnlyList<BoardPosition> EligiblePositions { get; }

    /// <summary>Gets the number of rounds resolved in this draft, including an automatic final collision. Resets on restart.</summary>
    public int ResolvedRounds { get; }

    /// <summary>Gets the number of main-phase turns resolved in this match. Resets on restart.</summary>
    public int ResolvedMainTurns { get; }

    /// <summary>Gets strategic control on the current board in Main or Finished. Null during the draft.</summary>
    public TerritoryAnalysis? Territory => _mainPhase?.Territory;

    /// <summary>Gets the final score and winner in Finished. Null in all other phases.</summary>
    public MainPhaseResult? FinalResult => _mainPhase?.Result;

    /// <summary>Gets the command identifier, which advances once per resolved round and once per restart.</summary>
    public long RoundId { get; }

    /// <summary>Gets the immutable set of legal main-phase selections for the specified player.</summary>
    /// <param name="player">The participant whose selections to read.</param>
    /// <returns>The player's legal targets, including any privately pending choice. Empty outside Main.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value does not identify either player.</exception>
    public IReadOnlySet<BoardPosition> GetMainPhaseLegalSelections(PlayerId player)
    {
        if (player is not (PlayerId.PlayerOne or PlayerId.PlayerTwo))
        {
            throw new ArgumentOutOfRangeException(nameof(player), player, "Unknown player.");
        }

        return Phase == MatchPhase.Main && _mainPhase is { } mainPhase
            ? mainPhase.GetLegalSelections(player)
            : FrozenSet<BoardPosition>.Empty;
    }

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
