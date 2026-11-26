namespace CastrumTactics.AI;

internal static class CandidateSampling
{
    // Scores combine bounded weighted features. The tolerance covers arithmetic roundoff.
    private const double ScoreEquivalenceTolerance = 1e-12;

    internal static int SampleMatrix(ReadOnlySpan<CpuCandidate> policy, Random random)
    {
        int last = 0;
        int support = 0;
        for (int i = 0; i < policy.Length; i++)
        {
            if (policy[i].Probability <= 0) continue;
            last = i;
            support++;
        }
        if (support == 1) return last;
        double draw = random.NextDouble();
        for (int i = 0; i <= last; i++)
        {
            draw -= policy[i].Probability;
            if (draw < 0) return i;
        }
        return last;
    }

    internal static int GetPoolSize(ReadOnlySpan<CpuCandidate> candidates, int nominalLimit, int minimum, double? maximumGap)
    {
        minimum = Math.Min(candidates.Length, minimum);
        int poolSize = Math.Min(candidates.Length, Math.Max(nominalLimit, minimum));
        if (maximumGap is double gap)
        {
            double best = candidates[0].Score;
            while (poolSize > minimum && best - candidates[poolSize - 1].Score > gap) poolSize--;
        }

        return IncludeCutoffTies(candidates, poolSize);
    }

    private static int IncludeCutoffTies(ReadOnlySpan<CpuCandidate> candidates, int poolSize)
    {
        // Compare to the original cutoff so a chain of near-ties cannot extend the margin.
        double cutoff = candidates[poolSize - 1].Score;
        while (poolSize < candidates.Length && cutoff - candidates[poolSize].Score <= ScoreEquivalenceTolerance)
        {
            poolSize++;
        }

        return poolSize;
    }

    internal static int Sample(Span<CpuCandidate> candidates, int poolSize, double temperature, double diversity, Random random)
    {
        if (temperature == 0) poolSize = IncludeCutoffTies(candidates, 1);
        for (int i = poolSize; i < candidates.Length; i++)
        {
            candidates[i] = candidates[i] with { Probability = 0 };
        }

        if (poolSize == 1)
        {
            candidates[0] = candidates[0] with { Probability = 1 };
            return 0;
        }

        double best = candidates[0].Score;
        double total = 0;
        for (int i = 0; i < poolSize; i++)
        {
            double weight = temperature == 0 ? 1 : Math.Exp((candidates[i].Score - best) / temperature);
            candidates[i] = candidates[i] with { Probability = weight };
            total += weight;
        }

        double draw = random.NextDouble();
        double cumulative = 0;
        int selected = -1;
        for (int i = 0; i < poolSize; i++)
        {
            double probability = (1 - diversity) * candidates[i].Probability / total + diversity / poolSize;
            candidates[i] = candidates[i] with { Probability = probability };
            cumulative += probability;
            if (selected < 0 && draw < cumulative) selected = i;
        }

        // Absorb rounding at the upper boundary without selecting an underflowed zero-weight tail.
        if (selected < 0)
        {
            selected = poolSize - 1;
            while (candidates[selected].Probability == 0) selected--;
        }

        return selected;
    }
}
