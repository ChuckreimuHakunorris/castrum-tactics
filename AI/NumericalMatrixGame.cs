namespace CastrumTactics.AI;

internal static class NumericalMatrixGame
{
    private const double PivotTolerance = 1e-10;
    internal const double MaximumGap = 1e-7;

    internal static Solution? Solve(double[,] payoff, CancellationToken token, Func<bool>? shouldStop = null)
    {
        token.ThrowIfCancellationRequested();
        if (shouldStop?.Invoke() == true) return null;
        int rows = payoff.GetLength(0), columns = payoff.GetLength(1);
        if (rows == 0 || columns == 0) throw new ArgumentException("Both players need an action.", nameof(payoff));
        var minima = new double[rows];
        var maxima = new double[columns];
        Array.Fill(maxima, double.NegativeInfinity);
        for (int i = 0; i < rows; i++)
        {
            minima[i] = double.PositiveInfinity;
            for (int j = 0; j < columns; j++)
            {
                double value = payoff[i, j];
                if (!(value >= -1 && value <= 1))
                    throw new ArgumentOutOfRangeException(nameof(payoff), "Payoffs must be in [-1, 1].");
                minima[i] = Math.Min(minima[i], value);
                maxima[j] = Math.Max(maxima[j], value);
            }
        }

        double lower = minima.Max(), upper = maxima.Min();
        // The best pure row and column certify a saddle within tolerance without building a tableau.
        if (upper - lower <= PivotTolerance)
            return new(Uniform(minima, lower), Uniform(maxima, upper), lower, upper, 0);

        // Shift to positive coefficients. The feasible slack basis avoids a separate feasibility phase.
        // The objective's slack prices recover the row player's dual policy.
        int rhs = rows + columns;
        var tableau = new double[rows + 1, rhs + 1];
        var basis = new int[rows];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++) tableau[i, j] = payoff[i, j] + 2;
            basis[i] = columns + i;
            tableau[i, basis[i]] = tableau[i, rhs] = 1;
        }
        for (int j = 0; j < columns; j++) tableau[rows, j] = -1;
        int pivots = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (shouldStop?.Invoke() == true) return null;
            int enter = 0;
            while (enter < rhs && tableau[rows, enter] >= -PivotTolerance) enter++;
            if (enter == rhs) break;
            if (++pivots > 256) return null;
            int leave = -1;
            double smallest = double.PositiveInfinity;
            for (int i = 0; i < rows; i++)
            {
                double coefficient = tableau[i, enter];
                if (coefficient <= PivotTolerance) continue;
                double ratio = tableau[i, rhs] / coefficient;
                if (leave >= 0 && (ratio > smallest + PivotTolerance ||
                    Math.Abs(ratio - smallest) <= PivotTolerance && basis[i] >= basis[leave])) continue;
                leave = i;
                smallest = ratio;
            }
            if (leave < 0) return null;
            basis[leave] = enter;
            double pivot = tableau[leave, enter];
            for (int j = 0; j <= rhs; j++) tableau[leave, j] /= pivot;
            for (int i = 0; i <= rows; i++)
            {
                if (i == leave) continue;
                double factor = tableau[i, enter];
                for (int j = 0; j <= rhs; j++) tableau[i, j] -= factor * tableau[leave, j];
                tableau[i, enter] = 0;
            }
        }

        var row = new double[rows];
        var column = new double[columns];
        for (int i = 0; i < rows; i++)
        {
            row[i] = tableau[rows, columns + i];
            if (basis[i] < columns) column[basis[i]] = tableau[i, rhs];
        }
        if (!Normalize(row) || !Normalize(column)) return null;

        // Measure both guarantees directly on the unshifted payoffs after normalization.
        Array.Clear(minima);
        Array.Clear(maxima);
        for (int i = 0; i < rows; i++)
        for (int j = 0; j < columns; j++)
        {
            minima[i] += column[j] * payoff[i, j];
            maxima[j] += row[i] * payoff[i, j];
        }
        lower = maxima.Min();
        upper = minima.Max();
        return Math.Abs(upper - lower) <= MaximumGap ? new(row, column, lower, upper, pivots) : null;
    }

    private static double[] Uniform(double[] values, double target)
    {
        double weight = 1.0 / values.Count(value => value == target);
        return values.Select(value => value == target ? weight : 0).ToArray();
    }

    private static bool Normalize(double[] probabilities)
    {
        double sum = 0;
        for (int i = 0; i < probabilities.Length; i++)
        {
            if (!double.IsFinite(probabilities[i]) || probabilities[i] < -PivotTolerance) return false;
            probabilities[i] = Math.Max(0, probabilities[i]);
            sum += probabilities[i];
        }
        if (sum <= PivotTolerance || !double.IsFinite(sum)) return false;
        for (int i = 0; i < probabilities.Length; i++) probabilities[i] /= sum;
        return true;
    }

    internal sealed record Solution(double[] Row, double[] Column, double Lower, double Upper, int Pivots)
    {
        internal double Value => (Lower + Upper) / 2;
    }
}
