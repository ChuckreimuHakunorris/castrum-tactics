namespace CastrumTactics.AI;

/// <summary>Limits Knight Support search and configures the opponent response model.</summary>
public sealed class CpuSearchOptions
{
    /// <summary>Creates deterministic search limits. Pair budgets are shared by all depths of one decision.</summary>
    /// <param name="depth">Maximum number of simultaneous turns to search, from one through two.</param>
    /// <param name="ownCandidates">Maximum number of own root candidates, from one through 32.</param>
    /// <param name="opponentCandidates">Maximum number of common root replies, from one through 32.</param>
    /// <param name="pairBudget">Maximum number of authoritative resolutions per phase decision, from one through 4096.</param>
    /// <param name="risk">Adverse-outcome weight from zero through one. The remaining weight goes to the expected outcome.</param>
    /// <param name="replyTemperature">Positive opponent softmax temperature. Higher values give plausible alternatives more weight.</param>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is outside its supported range.</exception>
    public CpuSearchOptions(int depth, int ownCandidates, int opponentCandidates, int pairBudget, double risk,
        double replyTemperature)
    {
        if (depth is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(depth));
        if (ownCandidates is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(ownCandidates));
        if (opponentCandidates is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(opponentCandidates));
        if (pairBudget is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(pairBudget));
        if (!(risk >= 0 && risk <= 1)) throw new ArgumentOutOfRangeException(nameof(risk));
        if (!(replyTemperature > 0 && double.IsFinite(replyTemperature)))
            throw new ArgumentOutOfRangeException(nameof(replyTemperature));
        Depth = depth;
        OwnCandidates = ownCandidates;
        OpponentCandidates = opponentCandidates;
        PairBudget = pairBudget;
        Risk = risk;
        ReplyTemperature = replyTemperature;
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
}
