namespace CastrumTactics.Development.Solver;

// Fast candidate generation only. Every accepted policy is bounded using exact rational dot products.
internal static class Binary64Policy
{
    private const double Tolerance = 1e-12;
    private const int TicketCount = 1 << 30;

    internal static Equilibrium? TryCertify(Rational[,] lowerMatrix, Rational[,] upperMatrix, int scale)
    {
        var lowerGame = TryCandidate(lowerMatrix);
        var upperGame = ReferenceEquals(lowerMatrix, upperMatrix) ? lowerGame : TryCandidate(upperMatrix);
        if (lowerGame is not { } lower || upperGame is not { } upper) return null;
        var red = lower.Red;
        var blue = upper.Blue;
        int rows = lowerMatrix.GetLength(0), columns = lowerMatrix.GetLength(1);
        var l = MatrixGame.ColumnExpectations(lowerMatrix, red).Min();
        var u = MatrixGame.RowExpectations(upperMatrix, blue).Max();
        Rational inheritedWidth = 0;
        if (!ReferenceEquals(lowerMatrix, upperMatrix))
        {
            for (int i = 0; i < rows; i++)
            for (int j = 0; j < columns; j++)
            {
                var width = upperMatrix[i,j] - lowerMatrix[i,j];
                if (width > inheritedWidth) inheritedWidth = width;
            }
        }

        if (l > u || u - l > inheritedWidth + ((Rational)1 / scale)) return null;
        var result = new Equilibrium((l + u) / 2, red, blue) { Lower = l, Upper = u };
        return MatrixGame.VerifyBounds(lowerMatrix, upperMatrix, result);
    }

    private static (Rational[] Red, Rational[] Blue)? TryCandidate(Rational[,] payoffs)
    {
        int rows = payoffs.GetLength(0), columns = payoffs.GetLength(1);
        var row = MatrixGame.RowGuarantees(payoffs);
        var column = MatrixGame.ColumnCeilings(payoffs);
        var lower = row.Max();
        if (lower == column.Min())
            return (MatrixGame.UniformAtValue(row, lower), MatrixGame.UniformAtValue(column, lower));

        int rhs = columns + rows;
        var tableau = new double[rows + 1, rhs + 1];
        var basic = new int[rows];
        double shift = 1 - row.Min().Approximate();
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++) tableau[i,j] = payoffs[i,j].Approximate() + shift;
            basic[i] = columns + i;
            tableau[i,basic[i]] = 1;
            tableau[i,rhs] = 1;
        }
        for (int j = 0; j < columns; j++) tableau[rows,j] = -1;

        for (int iteration = 0; iteration < 256; iteration++)
        {
            int enter = 0;
            while (enter < rhs && tableau[rows,enter] >= -Tolerance) enter++;
            if (enter == rhs)
            {
                var red = new double[rows];
                var blue = new double[columns];
                for (int i = 0; i < rows; i++)
                {
                    red[i] = tableau[rows,columns+i];
                    if (basic[i] < columns) blue[basic[i]] = tableau[i,rhs];
                }
                var r = Rationalize(red);
                var b = Rationalize(blue);
                return r is null || b is null ? null : (r, b);
            }

            int leave = -1;
            double best = double.PositiveInfinity;
            for (int i = 0; i < rows; i++)
            {
                double coefficient = tableau[i,enter];
                if (coefficient <= Tolerance) continue;
                double ratio = tableau[i,rhs] / coefficient;
                if (ratio > best + Tolerance || (leave >= 0 && Math.Abs(ratio-best) <= Tolerance && basic[i] > basic[leave]))
                    continue;
                leave = i;
                best = ratio;
            }
            if (leave < 0) return null;
            basic[leave] = enter;
            double pivot = tableau[leave,enter];
            for (int j = 0; j <= rhs; j++) tableau[leave,j] /= pivot;
            for (int i = 0; i <= rows; i++)
            {
                if (i == leave || tableau[i,enter] == 0) continue;
                double multiple = tableau[i,enter];
                for (int j = 0; j <= rhs; j++) tableau[i,j] -= multiple * tableau[leave,j];
            }
        }

        return null;
    }

    private static Rational[]? Rationalize(double[] weights)
    {
        if (weights.Any(w => !double.IsFinite(w) || w < -Tolerance)) return null;
        double sum = weights.Sum(w => Math.Max(0, w));
        if (!(sum > 0) || !double.IsFinite(sum)) return null;
        int largest = Array.IndexOf(weights, weights.Max());
        int[] tickets = weights.Select(w => (int)Math.Floor(Math.Max(0, w) / sum * TicketCount)).ToArray();
        tickets[largest] += TicketCount - tickets.Sum();
        return tickets.Select(t => (Rational)t / TicketCount).ToArray();
    }
}
