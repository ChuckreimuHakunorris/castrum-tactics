namespace CastrumTactics.AI;

/// <summary>Limits Knight Support search and configures the opponent response model.</summary>
public sealed class CpuSearchOptions
{
    /// <summary>Creates deterministic search limits. Pair budgets are shared by all depths of one decision.</summary>
    /// <param name="depth">Maximum number of simultaneous turns to search, from one through six.</param>
    /// <param name="ownCandidates">Maximum number of own root candidates, from one through 32.</param>
    /// <param name="opponentCandidates">Maximum number of common root replies, from one through 32.</param>
    /// <param name="pairBudget">Maximum number of authoritative resolutions per phase decision, from one through 32768.</param>
    /// <param name="risk">Adverse-outcome weight from zero through one. The remaining weight goes to the expected outcome.</param>
    /// <param name="replyTemperature">Positive opponent softmax temperature. Higher values give plausible alternatives more weight.</param>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is outside its supported range.</exception>
    public CpuSearchOptions(int depth, int ownCandidates, int opponentCandidates, int pairBudget, double risk,
        double replyTemperature)
        : this(depth, ownCandidates, opponentCandidates, pairBudget, risk, replyTemperature, 2, 2, 2) { }

    /// <summary>Creates deterministic limits and sets the breadth of passes beyond the first two turns.</summary>
    /// <param name="depth">Maximum number of simultaneous turns to search, from one through six.</param>
    /// <param name="ownCandidates">Maximum number of root actions, from one through 32.</param>
    /// <param name="opponentCandidates">Maximum common root replies, from one through 32.</param>
    /// <param name="pairBudget">Maximum authoritative resolutions per decision, from one through 32768.</param>
    /// <param name="risk">Adverse-outcome weight, from zero through one.</param>
    /// <param name="replyTemperature">Positive, finite opponent softmax temperature.</param>
    /// <param name="continuationOwnCandidates">Own-action breadth inside deeper passes, from one through eight.</param>
    /// <param name="continuationOpponentCandidates">Common response breadth inside deeper passes, from one through eight.</param>
    /// <param name="deepFinalists">Maximum root finalists to compare at a deeper pass, from two through six.</param>
    /// <param name="verifyTerminalWins">Whether deeper search verifies apparent immediate wins against omitted root replies.</param>
    /// <param name="requireFullRootReplies">Whether passes beyond depth two require a complete common root reply set.</param>
    /// <param name="proofOnlyDeepening">Whether to spend extra effort only on compact root matrices and accept deeper decisions only when they prove a win.</param>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is outside its supported range.</exception>
    public CpuSearchOptions(int depth, int ownCandidates, int opponentCandidates, int pairBudget, double risk,
        double replyTemperature, int continuationOwnCandidates, int continuationOpponentCandidates, int deepFinalists,
        bool verifyTerminalWins = true, bool requireFullRootReplies = false, bool proofOnlyDeepening = false)
    {
        if (depth is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(depth));
        if (ownCandidates is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(ownCandidates));
        if (opponentCandidates is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(opponentCandidates));
        if (pairBudget is < 1 or > 32768) throw new ArgumentOutOfRangeException(nameof(pairBudget));
        if (!(risk >= 0 && risk <= 1)) throw new ArgumentOutOfRangeException(nameof(risk));
        if (!(replyTemperature > 0 && double.IsFinite(replyTemperature)))
            throw new ArgumentOutOfRangeException(nameof(replyTemperature));
        if (continuationOwnCandidates is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(continuationOwnCandidates));
        if (continuationOpponentCandidates is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(continuationOpponentCandidates));
        if (deepFinalists is < 2 or > 6) throw new ArgumentOutOfRangeException(nameof(deepFinalists));
        Depth = depth;
        OwnCandidates = ownCandidates;
        OpponentCandidates = opponentCandidates;
        PairBudget = pairBudget;
        Risk = risk;
        ReplyTemperature = replyTemperature;
        ContinuationOwnCandidates = continuationOwnCandidates;
        ContinuationOpponentCandidates = continuationOpponentCandidates;
        DeepFinalists = deepFinalists;
        VerifyTerminalWins = verifyTerminalWins;
        RequireFullRootReplies = requireFullRootReplies;
        ProofOnlyDeepening = proofOnlyDeepening;
    }

    /// <summary>Gets the maximum simultaneous-turn depth.</summary>
    public int Depth { get; }
    /// <summary>Gets the root own-action limit.</summary>
    public int OwnCandidates { get; }
    /// <summary>Gets the common root response limit. A legal omitted collision may also be checked.</summary>
    public int OpponentCandidates { get; }
    /// <summary>Gets the deterministic limit on authoritative resolutions per decision.</summary>
    public int PairBudget { get; }
    /// <summary>Gets the fraction of a rating assigned to adverse outcomes.</summary>
    public double Risk { get; }
    /// <summary>Gets the opponent model's softmax temperature.</summary>
    public double ReplyTemperature { get; }
    /// <summary>Gets the own-action breadth at continuation nodes in deeper passes.</summary>
    public int ContinuationOwnCandidates { get; }
    /// <summary>Gets the common reply breadth at continuation nodes in deeper passes.</summary>
    public int ContinuationOpponentCandidates { get; }
    /// <summary>Gets the maximum root actions compared at depth three and beyond.</summary>
    public int DeepFinalists { get; }
    /// <summary>Gets whether deeper search verifies apparent immediate wins against omitted root replies.</summary>
    public bool VerifyTerminalWins { get; }
    /// <summary>Gets whether passes beyond depth two require all legal root replies in the common set.</summary>
    public bool RequireFullRootReplies { get; }
    /// <summary>Gets whether deeper refinement is limited to compact, fully covered root matrices and verified wins.</summary>
    public bool ProofOnlyDeepening { get; }
}
