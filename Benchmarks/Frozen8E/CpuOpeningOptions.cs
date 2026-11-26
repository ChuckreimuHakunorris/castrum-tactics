namespace CastrumTactics.AI;

/// <summary>Chooses how a completed simultaneous opening matrix is used.</summary>
public enum CpuOpeningPolicy
{
    /// <summary>Blend the common reply expectation with the adverse reply.</summary>
    ExpectedAdverse,
    /// <summary>Choose among actions with the best modeled worst case.</summary>
    WorstCase,
    /// <summary>Sample from a numerical equilibrium of the modeled matrix.</summary>
    Equilibrium
}

/// <summary>Configures optional Knight Support draft search independently of Main search.</summary>
public sealed class CpuOpeningOptions
{
    /// <summary>Creates an opening-only work and policy configuration.</summary>
    /// <param name="depth">Maximum simultaneous draft decisions to preview, from one to six.</param>
    /// <param name="candidateLimit">Maximum number of shared candidates per matrix, from two to 32.</param>
    /// <param name="pairBudget">Maximum new authoritative draft and Main preview pairs combined.</param>
    /// <param name="policy">Method for selecting from each completed matrix.</param>
    /// <param name="synergy">Weight of marginal, terrain-aware coverage in [0, 1].</param>
    /// <param name="mainPreviewDepth">Maximum Main turns previewed at a completed opening, from zero to two.</param>
    /// <param name="frontierEvaluation">Use the terrain-aware draft evaluator. False uses the 8D.3 positional evaluation.</param>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its supported range.</exception>
    public CpuOpeningOptions(int depth, int candidateLimit, int pairBudget, CpuOpeningPolicy policy,
        double synergy = 1, int mainPreviewDepth = 0, bool frontierEvaluation = true)
    {
        if (depth is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(depth));
        if (candidateLimit is < 2 or > 32) throw new ArgumentOutOfRangeException(nameof(candidateLimit));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pairBudget);
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        if (!(synergy is >= 0 and <= 1)) throw new ArgumentOutOfRangeException(nameof(synergy));
        if (mainPreviewDepth is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(mainPreviewDepth));
        Depth = depth;
        CandidateLimit = candidateLimit;
        PairBudget = pairBudget;
        Policy = policy;
        Synergy = synergy;
        MainPreviewDepth = mainPreviewDepth;
        FrontierEvaluation = frontierEvaluation;
    }

    /// <summary>Gets the maximum recursive draft tier.</summary>
    public int Depth { get; }
    /// <summary>Gets the maximum shared matrix width.</summary>
    public int CandidateLimit { get; }
    /// <summary>Gets the deterministic cap on new authoritative pairs.</summary>
    public int PairBudget { get; }
    /// <summary>Gets the selection and continuation policy.</summary>
    public CpuOpeningPolicy Policy { get; }
    /// <summary>Gets the blend weight for combined castle coverage.</summary>
    public double Synergy { get; }
    /// <summary>Gets the maximum Main-phase preview horizon.</summary>
    public int MainPreviewDepth { get; }
    /// <summary>Gets whether to use terrain-aware draft features.</summary>
    public bool FrontierEvaluation { get; }
}
