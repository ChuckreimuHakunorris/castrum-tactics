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
/// <param name="CollisionChecked">Whether resolution with a same-square reply was included.</param>
/// <param name="Depth">The comparable completed depth used for this action.</param>
public readonly record struct CpuActionAnalysis(BoardPosition Position, double Expected, double Adverse,
    bool CollisionChecked, int Depth);

/// <summary>Shows the weighted, signed nonterminal evaluation terms from the acting player's perspective.</summary>
/// <param name="Secured">Authoritative secured score lead times its phase weight.</param>
/// <param name="Influence">Estimated contested or draft influence lead times its weight.</param>
/// <param name="Material">Castle and knight value with a discount for threatened knights.</param>
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
/// <param name="Depth">Deepest fully compared search pass, or zero when using the static fallback.</param>
/// <param name="BudgetExhausted">Whether the next complete comparison could not fit within the pair budget.</param>
/// <param name="TimeLimitReached">Whether the optional elapsed-time safeguard interrupted search.</param>
/// <param name="Elapsed">Total wall time for the decision with diagnostics enabled.</param>
public sealed record CpuSearchReport(IReadOnlyList<CpuActionAnalysis> Actions, IReadOnlyList<CpuReplyAnalysis> Replies,
    CpuStrategicFeatures? Position, int ResolvedPairs, int Depth, bool BudgetExhausted, bool TimeLimitReached,
    TimeSpan Elapsed);
