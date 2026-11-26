namespace CastrumTactics.Domain.Session;

/// <summary>Describes a simultaneously resolved pair and the authoritative board states around it.</summary>
/// <remarks>Use the originating session together with <see cref="RoundId"/> as its identity. Hypothetical
/// previews never publish these records. Only the resolved pair can reveal submitted coordinates.</remarks>
public sealed class ResolvedTurn
{
    internal ResolvedTurn(GameSessionSnapshot before, GameSessionSnapshot after, BoardPosition playerOne,
        BoardPosition playerTwo, BoardState? mainBeforeSupport, BoardPosition? automaticDraftCollision)
    {
        Before = before;
        After = after;
        PlayerOne = playerOne;
        PlayerTwo = playerTwo;
        MainBeforeSupport = mainBeforeSupport;
        AutomaticDraftCollision = automaticDraftCollision;
    }

    /// <summary>Gets the public pre-resolution state and its legal sets and scores.</summary>
    public GameSessionSnapshot Before { get; }

    /// <summary>Gets the published result, including any forced collision or support eliminations.</summary>
    public GameSessionSnapshot After { get; }

    /// <summary>Gets the phase in which the players submitted the pair.</summary>
    public MatchPhase Phase => Before.Phase;

    /// <summary>Gets the session-scoped identifier of the submitted pair.</summary>
    public long RoundId => Before.RoundId;

    /// <summary>Gets Player One's resolved selection.</summary>
    public BoardPosition PlayerOne { get; }

    /// <summary>Gets Player Two's resolved selection.</summary>
    public BoardPosition PlayerTwo { get; }

    /// <summary>Gets the automatically collided draft square, if the pair left a single start.</summary>
    /// <remarks>The automatic collision uses <see cref="RoundId"/> plus one and is part of the same publication.</remarks>
    public BoardPosition? AutomaticDraftCollision { get; }

    /// <summary>Gets both ordinary Main outcomes before Knight Support, or null for a draft pair.</summary>
    /// <remarks>Compare this immutable board with the final board to identify support removals, including
    /// a knight that was placed or captured and then removed during this turn.</remarks>
    public BoardState? MainBeforeSupport { get; }
}
