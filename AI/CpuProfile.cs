namespace CastrumTactics.AI;

/// <summary>Names the five provisional built-in CPU configurations.</summary>
public enum CpuDifficulty
{
    /// <summary>Samples broadly from plausible tactical selections.</summary>
    Easy,
    /// <summary>Balances tactical awareness with some variation.</summary>
    Normal,
    /// <summary>Favors stronger territory and mobility choices.</summary>
    Hard,
    /// <summary>Uses detailed static assessment with little variation.</summary>
    VeryHard,
    /// <summary>Always takes a best-rated main-phase selection and varies draft choices.</summary>
    Monster
}

/// <summary>Controls relative feature importance and how ranked legal candidates are sampled.</summary>
public sealed class CpuProfile
{
    /// <summary>Creates a profile for built-in or custom tuning.</summary>
    /// <param name="weights">Finite feature weights in the inclusive range 0 through 10.</param>
    /// <param name="candidateLimit">Positive ranked pool size. Opening retains at least two candidates when available.</param>
    /// <param name="mainTemperature">Softmax temperature from 0 through 10. Zero selects the first best candidate.</param>
    /// <param name="openingTemperature">Softmax temperature greater than 0 and at most 10.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied weight or selection parameter is outside its range.</exception>
    public CpuProfile(CpuFeatures weights, int candidateLimit, double mainTemperature, double openingTemperature)
    {
        weights.ValidateWeights();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(candidateLimit);
        ValidateTemperature(mainTemperature, nameof(mainTemperature), allowZero: true);
        ValidateTemperature(openingTemperature, nameof(openingTemperature), allowZero: false);
        Weights = weights;
        CandidateLimit = candidateLimit;
        MainTemperature = mainTemperature;
        OpeningTemperature = openingTemperature;
    }

    /// <summary>Gets the weights for the normalized candidate features.</summary>
    public CpuFeatures Weights { get; }

    /// <summary>Gets the number of top-ranked candidates considered before sampling.</summary>
    public int CandidateLimit { get; }

    /// <summary>Gets main-phase temperature. Higher values flatten the distribution within the pool.</summary>
    public double MainTemperature { get; }

    /// <summary>Gets opening temperature. Higher values flatten the distribution within the pool.</summary>
    public double OpeningTemperature { get; }

    private static void ValidateTemperature(double value, string parameter, bool allowZero)
    {
        if (!double.IsFinite(value) || value < 0 || value > 10 || (!allowZero && value == 0))
        {
            throw new ArgumentOutOfRangeException(parameter, value, "Invalid CPU selection temperature.");
        }
    }
}

/// <summary>Provides the built-in tuning for each difficulty.</summary>
public static class CpuDifficultyProfiles
{
    private static readonly CpuProfile[] Profiles =
    [
        new(new(2.2, 0.7, 1.5, 0.8, 0.3, 0.2, 0.5, 0.35), 12, 1.0, 0.9),
        new(new(2.2, 1.2, 1.6, 1.1, 0.6, 0.5, 0.8, 0.65), 8, 0.6, 0.75),
        new(new(2.0, 1.6, 1.8, 1.4, 0.9, 0.8, 1.1, 0.95), 5, 0.3, 0.65),
        new(new(2.0, 1.9, 1.9, 1.6, 1.1, 1.1, 1.3, 1.15), 3, 0.12, 0.55),
        new(new(2.0, 2.1, 2.0, 1.7, 1.2, 1.2, 1.4, 1.25), 1, 0, 0.5),
    ];

    /// <summary>Gets the immutable configuration for a named difficulty.</summary>
    /// <param name="difficulty">One of the five supported values.</param>
    /// <returns>The corresponding built-in profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The difficulty is undefined.</exception>
    public static CpuProfile Get(CpuDifficulty difficulty) => (uint)difficulty < (uint)Profiles.Length
        ? Profiles[(int)difficulty]
        : throw new ArgumentOutOfRangeException(nameof(difficulty));
}
