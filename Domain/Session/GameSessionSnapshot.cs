using System.Collections.Frozen;

namespace CastrumTactics.Domain.Session;

/// <summary>Public match state shared by both players, with submission status and resolved board results.</summary>
public sealed class GameSessionSnapshot
{
    private readonly bool _playerOneSubmitted;
    private readonly bool _playerTwoSubmitted;
    private readonly OpeningDraft _draft;

    internal GameSessionSnapshot(OpeningDraft draft, MainPhaseState? mainPhase, int resolvedMainTurns, long roundId,
        bool playerOneSubmitted, bool playerTwoSubmitted)
    {
        _draft = draft;
        MainState = mainPhase;
        RuleSet = draft.RuleSet;
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

    /// <summary>Gets the gameplay rules fixed for this match and its restarts.</summary>
    public RuleSet RuleSet { get; }

    /// <summary>Gets the current match phase, including whether a restart is required.</summary>
    public MatchPhase Phase { get; }

    /// <summary>Gets the shared eligible opening positions, including any privately pending choice. Empty outside Opening.</summary>
    public IReadOnlyList<BoardPosition> EligiblePositions { get; }

    /// <summary>Gets the number of rounds resolved in this draft, including an automatic final collision. Resets on restart.</summary>
    public int ResolvedRounds { get; }

    /// <summary>Gets the number of main-phase turns resolved in this match. Resets on restart.</summary>
    public int ResolvedMainTurns { get; }

    /// <summary>Gets strategic control on the current board in Main or Finished. Null during the draft.</summary>
    public TerritoryAnalysis? Territory => MainState?.Territory;

    /// <summary>Gets the immutable evaluated Main board, including on a finished match. Null during a draft or a failed draft.</summary>
    public MainPhaseState? MainState { get; }

    /// <summary>Previews a pair of simultaneous draft choices using the session's rules, including any forced final collision.</summary>
    /// <param name="playerOne">Player One's eligible choice on this snapshot.</param>
    /// <param name="playerTwo">Player Two's eligible choice on this snapshot.</param>
    /// <returns>A hypothetical snapshot with no pending submissions and the projected next phase and round.</returns>
    /// <exception cref="InvalidOperationException">The phase is not Opening or either choice is ineligible.</exception>
    /// <remarks>Uses the same resolution as live play, including immediate completion when the final draft board has no Main actions. Ignores submission status and does not modify the originating session.</remarks>
    public GameSessionSnapshot PreviewDraft(BoardPosition playerOne, BoardPosition playerTwo)
    {
        if (Phase != MatchPhase.Opening) throw new InvalidOperationException("The draft is not active.");
        var (draft, main, rounds) = _draft.ResolveTransition(playerOne, playerTwo);
        return new GameSessionSnapshot(draft, main, 0, RoundId + rounds, false, false);
    }

    /// <summary>Gets Player One's secured territory total, or zero during the opening or a failed draft.</summary>
    public int PlayerOneScore => Territory?.PlayerOneScore ?? 0;

    /// <summary>Gets Player Two's secured territory total, or zero during the opening or a failed draft.</summary>
    public int PlayerTwoScore => Territory?.PlayerTwoScore ?? 0;

    /// <summary>Gets the final score and winner in Finished. Null in all other phases.</summary>
    public MainPhaseResult? FinalResult => MainState?.Result;

    /// <summary>Gets the session-scoped command identifier, which advances once per resolved round and once per restart.</summary>
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

        return Phase == MatchPhase.Main && MainState is { } mainPhase
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
