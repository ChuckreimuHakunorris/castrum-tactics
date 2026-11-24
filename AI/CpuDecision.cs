using CastrumTactics.Domain;

namespace CastrumTactics.AI;

/// <summary>A proposed selection tied to the snapshot that supplied its legality.</summary>
/// <param name="Player">The player making the selection.</param>
/// <param name="Position">A legal position on the supplied snapshot.</param>
/// <param name="Phase">The phase of the supplied snapshot.</param>
/// <param name="RoundId">The round identifier within the originating session, used for command validation.</param>
public readonly record struct CpuDecision(PlayerId Player, BoardPosition Position, MatchPhase Phase, long RoundId);

/// <summary>Records the static evaluation and selection probability for a legal candidate.</summary>
/// <param name="Position">The evaluated legal position.</param>
/// <param name="Features">The normalized feature values before profile weighting.</param>
/// <param name="Score">The weighted tactical score.</param>
/// <param name="Probability">The sampling probability, including zero outside the candidate pool.</param>
public readonly record struct CpuCandidate(BoardPosition Position, CpuFeatures Features, double Score, double Probability);

/// <summary>Provides a decision and its optional ranking diagnostics.</summary>
public sealed class CpuDecisionReport
{
    internal CpuDecisionReport(CpuDecision? decision, CpuCandidate[] candidates)
    {
        Decision = decision;
        Candidates = Array.AsReadOnly(candidates);
    }

    /// <summary>Gets the selected candidate, or null when no decision is available.</summary>
    public CpuDecision? Decision { get; }

    /// <summary>Gets all evaluated candidates in score order, breaking ties in row-major order.</summary>
    public IReadOnlyList<CpuCandidate> Candidates { get; }
}
