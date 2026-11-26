using CastrumTactics.Domain;

namespace CastrumTactics.AI;

/// <summary>A proposed selection tied to the snapshot that supplied its legality.</summary>
/// <param name="Player">The player making the selection.</param>
/// <param name="Position">A legal position on the supplied snapshot.</param>
/// <param name="Phase">The phase of the supplied snapshot.</param>
/// <param name="RoundId">The round identifier within the originating session, used for command validation.</param>
public readonly record struct CpuDecision(PlayerId Player, BoardPosition Position, MatchPhase Phase, long RoundId);

/// <summary>Records the evaluation and selection probability for a legal candidate.</summary>
/// <param name="Position">The evaluated legal position.</param>
/// <param name="Features">The normalized feature values before profile weighting.</param>
/// <param name="Score">The weighted tactical score, replaced by the search rating for analyzed actions.</param>
/// <param name="Probability">The sampling probability, including zero outside the candidate pool.</param>
public readonly record struct CpuCandidate(BoardPosition Position, CpuFeatures Features, double Score, double Probability);

/// <summary>Provides a decision and its optional ranking diagnostics.</summary>
public sealed class CpuDecisionReport
{
    internal CpuDecisionReport(CpuDecision? decision, CpuCandidate[] candidates, CpuSearchReport? search)
    {
        Decision = decision;
        Candidates = Array.AsReadOnly(candidates);
        Search = search;
    }

    /// <summary>Gets the selected candidate, or null when no decision is available.</summary>
    public CpuDecision? Decision { get; }

    /// <summary>Gets ranked candidates, with searched finalists before static or shallower candidates.</summary>
    public IReadOnlyList<CpuCandidate> Candidates { get; }

    /// <summary>Gets optional strategic search statistics, or null for a static decision.</summary>
    public CpuSearchReport? Search { get; }
}

/// <summary>Summarizes an analyzed root action under the common opponent model.</summary>
/// <param name="Position">The action analyzed.</param>
/// <param name="Expected">The probability-weighted value across common replies.</param>
/// <param name="Adverse">The lowest value including an extra collision reply when applicable.</param>
/// <param name="CollisionChecked">Whether this action was evaluated against a legal same-square reply, either in the common set or as an additional adverse-only check.</param>
/// <param name="Depth">The comparable completed depth used for this action.</param>
public readonly record struct CpuActionAnalysis(BoardPosition Position, double Expected, double Adverse,
    bool CollisionChecked, int Depth)
{
    /// <summary>Gets whether all legal opposing replies were covered in a verified winning line.</summary>
    public bool ProvenWin { get; init; }
}

/// <summary>Shows the weighted, signed nonterminal evaluation terms from the acting player's perspective.</summary>
/// <param name="Secured">Authoritative secured score lead times its phase weight.</param>
/// <param name="Influence">Estimated contested or draft influence lead times its weight.</param>
/// <param name="Material">Piece value with a discount for threatened knights. The resilient evaluator accounts for castles through secured score and influence.</param>
/// <param name="Formation">Resilience adjustment from the available survival supporters.</param>
/// <param name="Ground">Ground degradation weighted by the relative influence at each square.</param>
/// <param name="Mobility">Meaningful expansion and disruption opportunities times their weight.</param>
public readonly record struct CpuStrategicFeatures(double Secured, double Influence, double Material,
    double Formation, double Ground, double Mobility);

/// <summary>Identifies a common modeled reply and its probability in the expected value calculation.</summary>
/// <param name="Position">A legal opponent selection.</param>
/// <param name="Probability">Its normalized weight in the common response model.</param>
public readonly record struct CpuReplyAnalysis(BoardPosition Position, double Probability);

/// <summary>Reports the work and opponent model used for a strategic decision.</summary>
/// <param name="Actions">Analyzed own actions and their outcome estimates.</param>
/// <param name="Replies">The common root opponent response set.</param>
/// <param name="Position">Strategic feature contributions of the input board in Main, or null during the draft.</param>
/// <param name="ResolvedPairs">Total authoritative pair resolutions performed.</param>
/// <param name="Depth">Comparable search depth used to select the action, or zero for the static fallback.</param>
/// <param name="BudgetExhausted">Whether the next complete comparison could not fit within the pair budget.</param>
/// <param name="TimeLimitReached">Whether the optional elapsed-time safeguard interrupted search.</param>
/// <param name="Elapsed">Total wall time for the decision with diagnostics enabled.</param>
public sealed record CpuSearchReport(IReadOnlyList<CpuActionAnalysis> Actions, IReadOnlyList<CpuReplyAnalysis> Replies,
    CpuStrategicFeatures? Position, int ResolvedPairs, int Depth, bool BudgetExhausted, bool TimeLimitReached,
    TimeSpan Elapsed)
{
    /// <summary>Gets how many requests reused an already resolved and evaluated pair in this decision.</summary>
    public int ReusedPairs { get; init; }
    /// <summary>Gets the deepest complete comparison, including a deeper pass that the selection policy declined.</summary>
    public int CompletedDepth { get; init; }
    /// <summary>Gets numerical diagnostics for a sampled matrix policy, or null when another policy was used.</summary>
    public CpuMatrixAnalysis? Matrix { get; init; }
}

/// <summary>Reports the security interval of the modeled payoff matrix in outcome utility units.</summary>
/// <param name="Security">Minimum modeled payoff of the selected row policy against a common reply.</param>
/// <param name="Ceiling">Maximum modeled payoff of any included own action against the dual reply policy.</param>
/// <param name="Pivots">Simplex pivots, with zero for a pure saddle.</param>
/// <param name="OwnActions">Number of included own actions.</param>
/// <param name="Replies">Number of included opposing actions.</param>
/// <param name="FullReplies">Whether every legal opposing root action was included. Continuations may still be approximate.</param>
/// <param name="Solves">Number of attempted numerical solves across all tiers.</param>
/// <param name="Elapsed">Wall time spent constructing and solving matrices for this decision.</param>
public readonly record struct CpuMatrixAnalysis(double Security, double Ceiling, int Pivots, int OwnActions,
    int Replies, bool FullReplies, int Solves, TimeSpan Elapsed);
