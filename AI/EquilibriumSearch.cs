using System.Diagnostics;

namespace CastrumTactics.AI;

internal sealed partial class SimultaneousSearch
{
    private int _matrixRootRows;
    private int _matrixLegalReplies;
    private int _matrixSolves;
    private long _matrixTicks;

    private CpuCandidate[] MatrixReplies(CpuCandidate[] choices, CpuCandidate[] replies, CpuCandidate[] legal)
    {
        _matrixRootRows = choices.Length;
        _matrixLegalReplies = legal.Length;
        if (_options.Depth == 1 && legal.Length <= 32 && (long)choices.Length * legal.Length <= _options.PairBudget)
            return legal;
        // Promote affordable collisions to common columns so every candidate faces the same opponent actions.
        var paired = IncludeCollisions(choices, replies, legal);
        return (long)choices.Length * paired.Length <= _options.PairBudget ? paired : replies;
    }

    private static CpuCandidate[] IncludeCollisions(CpuCandidate[] choices, CpuCandidate[] replies,
        CpuCandidate[] legal)
    {
        if (replies.Length == legal.Length) return replies;
        var included = choices.Select(choice => choice.Position).ToHashSet();
        included.ExceptWith(replies.Select(reply => reply.Position));
        var omitted = legal.Where(reply => included.Contains(reply.Position)).ToArray();
        return omitted.Length == 0 ? replies : [.. replies, .. omitted];
    }

    private bool CanSolveRoot(IReadOnlyList<Rating> rows, int columns) =>
        _options.Equilibrium is MatrixPolicy.Root or MatrixPolicy.Continuations &&
        rows.Count == _matrixRootRows && rows[0].Safety != 3 &&
        rows.All(rating => rating.Payoffs?.Length == columns);

    private NumericalMatrixGame.Solution? SolveMatrix(IReadOnlyList<Rating> rows, int columns)
    {
        long start = Stopwatch.GetTimestamp();
        var payoff = new double[rows.Count, columns];
        for (int i = 0; i < rows.Count; i++)
        for (int j = 0; j < columns; j++)
            payoff[i, j] = Math.Clamp(rows[i].Payoffs![j] / StrategicPosition.TerminalWin, -1, 1);
        var solution = NumericalMatrixGame.Solve(payoff, _token, ShouldStop);
        _matrixSolves++;
        _matrixTicks += Stopwatch.GetTimestamp() - start;
        return solution;
    }
}
