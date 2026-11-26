using System.Globalization;
using System.Numerics;

namespace CastrumTactics.Development.Solver;

internal readonly struct Rational : IComparable<Rational>, IEquatable<Rational>
{
    private readonly BigInteger _numerator;
    private readonly BigInteger _denominator;
    private BigInteger Denominator => _denominator.IsZero ? BigInteger.One : _denominator;
    internal bool IsZero => _numerator.IsZero;

    private Rational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero) throw new DivideByZeroException();
        var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator) * denominator.Sign;
        _numerator = numerator / divisor;
        _denominator = denominator / divisor;
    }

    public static implicit operator Rational(int value) => new(value, BigInteger.One);
    public static Rational operator +(Rational a, Rational b) => a.IsZero ? b : b.IsZero ? a :
        new(a._numerator * b.Denominator + b._numerator * a.Denominator, a.Denominator * b.Denominator);
    public static Rational operator -(Rational a, Rational b) => a + -b;
    public static Rational operator -(Rational a) => new(-a._numerator, a.Denominator);
    public static Rational operator *(Rational a, Rational b) => a.IsZero || b.IsZero ? default :
        new(a._numerator * b._numerator, a.Denominator * b.Denominator);
    public static Rational operator /(Rational a, Rational b) =>
        new(a._numerator * b.Denominator, a.Denominator * b._numerator);
    public static bool operator <(Rational a, Rational b) => a.CompareTo(b) < 0;
    public static bool operator >(Rational a, Rational b) => a.CompareTo(b) > 0;
    public static bool operator ==(Rational a, Rational b) => a.Equals(b);
    public static bool operator !=(Rational a, Rational b) => !a.Equals(b);

    /// <summary>Compares two fractions by exact cross multiplication.</summary>
    public int CompareTo(Rational other) =>
        (_numerator * other.Denominator).CompareTo(other._numerator * Denominator);

    /// <summary>Tests equality using reduced fractions.</summary>
    public bool Equals(Rational other) => _numerator == other._numerator && Denominator == other.Denominator;

    /// <summary>Tests equality with another rational value.</summary>
    public override bool Equals(object? obj) => obj is Rational other && Equals(other);

    /// <summary>Hashes the reduced numerator and positive denominator.</summary>
    public override int GetHashCode() => HashCode.Combine(_numerator, Denominator);

    /// <summary>Formats the exact number as an integer or a reduced fraction.</summary>
    public override string ToString() => Denominator.IsOne ? _numerator.ToString(CultureInfo.InvariantCulture) :
        FormattableString.Invariant($"{_numerator}/{Denominator}");

    internal double Approximate()
    {
        var denominator = Denominator;
        long shift = Math.Max(_numerator.GetBitLength(), denominator.GetBitLength()) - 1000;
        return shift <= 0 ? (double)_numerator / (double)denominator :
            (double)(_numerator >> (int)shift) / (double)(denominator >> (int)shift);
    }

    internal (BigInteger Numerator, BigInteger Denominator) Parts => (_numerator, Denominator);

    internal Rational RoundOutward(int scale, bool upper)
    {
        // Keep common small exact ratios and values already on the grid. A shared grid prevents
        // unrelated child denominators from multiplying in each exact simplex tableau.
        var denominator = Denominator;
        if (denominator <= Math.Min(16, scale) || (denominator <= scale && scale % denominator == 0))
            return this;
        var scaled = BigInteger.DivRem(_numerator * scale, denominator, out var remainder);
        if (upper && remainder.Sign > 0) scaled++;
        else if (!upper && remainder.Sign < 0) scaled--;
        return new(scaled, scale);
    }

    internal static Rational Parse(string text)
    {
        var parts = text.Split('/');
        if (parts.Length > 2) throw new FormatException("Invalid rational fraction.");
        return new(BigInteger.Parse(parts[0], CultureInfo.InvariantCulture),
            parts.Length == 1 ? BigInteger.One : BigInteger.Parse(parts[1], CultureInfo.InvariantCulture));
    }
}
