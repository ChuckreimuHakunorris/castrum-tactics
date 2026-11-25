namespace CastrumTactics.Development.Solver;

internal sealed record Equilibrium(Rational Value, Rational[] Red, Rational[] Blue)
{
    internal Rational Lower { get; init; } = Value;
    internal Rational Upper { get; init; } = Value;
    internal bool IsExact => Lower == Upper;
}

internal static class MatrixGame
{
    internal static Equilibrium Solve(Rational[,] payoffs, Action? checkLimits = null)
    {
        int rows = payoffs.GetLength(0), columns = payoffs.GetLength(1);
        if (rows == 0 || columns == 0) throw new ArgumentException("Both players need an action.", nameof(payoffs));

        Rational[] redWorst = RowGuarantees(payoffs);
        Rational[] blueWorst = ColumnCeilings(payoffs);
        var lower = redWorst.Max();
        var upper = blueWorst.Min();
        if (lower == upper)
        {
            // Uniform mixing over the pure equilibrium actions respects ties.
            return Verify(payoffs, new(lower, UniformAtValue(redWorst, lower), UniformAtValue(blueWorst, upper)));
        }

        // Shift all payoffs strictly positive. Maximize 1^T y with A y <= 1 and y >= 0.
        // The objective-row slack prices give the corresponding dual weights for Red.
        Rational shift = 1 - redWorst.Min();
        int rhs = columns + rows;
        var tableau = new Rational[rows + 1, rhs + 1];
        int[] basic = new int[rows];
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++) tableau[i, j] = payoffs[i, j] + shift;
            basic[i] = columns + i;
            tableau[i, basic[i]] = 1;
            tableau[i, rhs] = 1;
        }
        for (int j = 0; j < columns; j++) tableau[rows, j] = -1;
        while (true)
        {
            checkLimits?.Invoke();
            // Bland's rule makes even degenerate exact pivots terminate.
            int enter = 0;
            while (enter < rhs && !(tableau[rows, enter] < 0)) enter++;
            if (enter == rhs) break;
            int leave = -1;
            Rational best = default;
            for (int i = 0; i < rows; i++)
            {
                if (!(tableau[i, enter] > 0)) continue;
                Rational ratio = tableau[i, rhs] / tableau[i, enter];
                if (leave >= 0 && (ratio > best || (ratio == best && basic[i] > basic[leave]))) continue;
                leave = i;
                best = ratio;
            }
            if (leave < 0) throw new InvalidOperationException("Positive matrix LP was unexpectedly unbounded.");
            basic[leave] = enter;
            Rational pivot = tableau[leave, enter];
            for (int j = 0; j <= rhs; j++) tableau[leave, j] /= pivot;
            for (int i = 0; i <= rows; i++)
            {
                if (i == leave || tableau[i, enter].IsZero) continue;
                Rational multiplier = tableau[i, enter];
                for (int j = 0; j <= rhs; j++) tableau[i, j] -= multiplier * tableau[leave, j];
            }
        }

        Rational objective = tableau[rows, rhs];
        var red = new Rational[rows];
        var blue = new Rational[columns];
        for (int i = 0; i < rows; i++)
        {
            red[i] = tableau[rows, columns + i] / objective;
            if (basic[i] < columns) blue[basic[i]] = tableau[i, rhs] / objective;
        }
        return Verify(payoffs, new(1 / objective - shift, red, blue));
    }

    internal static Equilibrium SolveBounds(Rational[,] lowerMatrix, Rational[,] upperMatrix, Action? checkLimits = null)
    {
        var lower = Solve(lowerMatrix, checkLimits);
        var upper = Solve(upperMatrix, checkLimits);
        if (lower.Value > upper.Value) throw new InvalidDataException("Matrix bounds are inverted.");
        // Each exact solve has already verified the policy and its relevant endpoint.
        return new((lower.Value + upper.Value) / 2, lower.Red, upper.Blue)
        {
            Lower = lower.Value, Upper = upper.Value
        };
    }

    internal static Equilibrium VerifyBounds(Rational[,] lowerMatrix, Rational[,] upperMatrix, Equilibrium equilibrium)
    {
        if (!Distribution(equilibrium.Red, lowerMatrix.GetLength(0)) ||
            !Distribution(equilibrium.Blue, upperMatrix.GetLength(1)))
            throw new InvalidDataException("Invalid interval policy distribution.");
        var lower = ColumnExpectations(lowerMatrix, equilibrium.Red).Min();
        var upper = RowExpectations(upperMatrix, equilibrium.Blue).Max();
        if (lower < equilibrium.Lower || upper > equilibrium.Upper ||
            equilibrium.Value < equilibrium.Lower || equilibrium.Value > equilibrium.Upper)
            throw new InvalidDataException("Stored equilibrium interval does not bound the policy guarantees.");
        return equilibrium;
    }

    internal static Equilibrium Verify(Rational[,] matrix, Equilibrium equilibrium)
    {
        if (!Distribution(equilibrium.Red, matrix.GetLength(0)) ||
            !Distribution(equilibrium.Blue, matrix.GetLength(1)))
            throw new InvalidDataException("Invalid equilibrium probability distribution.");
        var lower = ColumnExpectations(matrix, equilibrium.Red).Min();
        var upper = RowExpectations(matrix, equilibrium.Blue).Max();
        if (lower != equilibrium.Value || upper != equilibrium.Value)
            throw new InvalidDataException($"Equilibrium certification failed. Value {equilibrium.Value}, bounds [{lower}, {upper}].");
        return equilibrium;
    }

    internal static bool Distribution(Rational[] probabilities, int size) =>
        probabilities.Length == size && probabilities.All(p => !(p < 0)) &&
        probabilities.Aggregate(default(Rational), (sum, p) => sum + p) == 1;

    internal static Rational[] RowGuarantees(Rational[,] matrix)
    {
        int rows = matrix.GetLength(0), columns = matrix.GetLength(1);
        var guarantees = new Rational[rows];
        for (int i = 0; i < rows; i++)
        {
            guarantees[i] = matrix[i,0];
            for (int j = 1; j < columns; j++)
                if (matrix[i,j] < guarantees[i]) guarantees[i] = matrix[i,j];
        }
        return guarantees;
    }

    internal static Rational[] ColumnCeilings(Rational[,] matrix)
    {
        int rows = matrix.GetLength(0), columns = matrix.GetLength(1);
        var ceilings = new Rational[columns];
        for (int j = 0; j < columns; j++)
        {
            ceilings[j] = matrix[0,j];
            for (int i = 1; i < rows; i++)
                if (matrix[i,j] > ceilings[j]) ceilings[j] = matrix[i,j];
        }
        return ceilings;
    }

    internal static Rational[] RowExpectations(Rational[,] matrix, Rational[] blue)
    {
        var expectations = new Rational[matrix.GetLength(0)];
        for (int j = 0; j < blue.Length; j++)
        {
            if (blue[j].IsZero) continue;
            for (int i = 0; i < expectations.Length; i++) expectations[i] += blue[j] * matrix[i,j];
        }
        return expectations;
    }

    internal static Rational[] ColumnExpectations(Rational[,] matrix, Rational[] red)
    {
        var expectations = new Rational[matrix.GetLength(1)];
        for (int i = 0; i < red.Length; i++)
        {
            if (red[i].IsZero) continue;
            for (int j = 0; j < expectations.Length; j++) expectations[j] += red[i] * matrix[i,j];
        }
        return expectations;
    }

    internal static bool[] Dominated(Rational[,] matrix, bool red) => Dominated(matrix, matrix, red);

    internal static bool[] Dominated(Rational[,] lower, Rational[,] upper, bool red)
    {
        int count = lower.GetLength(red ? 0 : 1), other = lower.GetLength(red ? 1 : 0);
        Rational Minimum(int i, int j) => red ? lower[i,j] : -upper[j,i];
        Rational Maximum(int i, int j) => red ? upper[i,j] : -lower[j,i];
        return Enumerable.Range(0, count).Select(i => Enumerable.Range(0, count).Any(k => k != i &&
            Enumerable.Range(0, other).All(j => Minimum(k,j).CompareTo(Maximum(i,j)) >= 0) &&
            Enumerable.Range(0, other).Any(j => Minimum(k,j) > Maximum(i,j)))).ToArray();
    }

    internal static Rational[] UniformAtValue(Rational[] guarantees, Rational value)
    {
        Rational weight = 1 / (Rational)guarantees.Count(v => v == value);
        return guarantees.Select(v => v == value ? weight : default).ToArray();
    }
}
