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

/// <summary>Immutable configuration for feature weighting and phase-specific sampling.</summary>
public sealed class CpuProfile
{
    /// <summary>Creates a profile for built-in or custom tuning.</summary>
    /// <param name="weights">Finite feature weights in the inclusive range 0 through 10.</param>
    /// <param name="openingCandidateLimit">Positive nominal opening pool size. At least two are retained when available.</param>
    /// <param name="mainCandidateLimit">Positive nominal main-phase pool size.</param>
    /// <param name="mainTemperature">Softmax temperature from 0 through 10. Zero samples uniformly among equivalent best candidates.</param>
    /// <param name="openingTemperature">Softmax temperature greater than 0 and at most 10.</param>
    /// <param name="openingDiversity">Uniform probability share for ordinary opening rounds, from 0 through 1.</param>
    /// <param name="urgentOpeningDiversity">Uniform share from 0 through 1 when two starts remain and neither player has a starting piece.</param>
    /// <param name="openingMaximumScoreDifference">Optional finite, nonnegative maximum gap from the best opening score.
    /// Pass null to disable the restriction. The opening pool minimum takes precedence.</param>
    /// <param name="mainMaximumScoreDifference">Optional finite, nonnegative maximum gap from the best main-phase score.
    /// Pass null to disable the restriction.</param>
    /// <exception cref="ArgumentOutOfRangeException">A supplied weight or selection parameter is outside its range.</exception>
    /// <remarks>Pool limits apply after evaluating all legal candidates. Both pools include equivalent candidates at the cutoff.</remarks>
    public CpuProfile(CpuEvaluationWeights weights, int openingCandidateLimit, int mainCandidateLimit,
        double mainTemperature, double openingTemperature, double openingDiversity = 0.2, double urgentOpeningDiversity = 0.7,
        double? openingMaximumScoreDifference = null, double? mainMaximumScoreDifference = null)
    {
        weights.ValidateWeights();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(openingCandidateLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mainCandidateLimit);
        ValidateTemperature(mainTemperature, nameof(mainTemperature), allowZero: true);
        ValidateTemperature(openingTemperature, nameof(openingTemperature), allowZero: false);
        ValidateDiversity(openingDiversity, nameof(openingDiversity));
        ValidateDiversity(urgentOpeningDiversity, nameof(urgentOpeningDiversity));
        ValidateScoreDifference(openingMaximumScoreDifference, nameof(openingMaximumScoreDifference));
        ValidateScoreDifference(mainMaximumScoreDifference, nameof(mainMaximumScoreDifference));
        Weights = weights;
        OpeningCandidateLimit = openingCandidateLimit;
        MainCandidateLimit = mainCandidateLimit;
        MainTemperature = mainTemperature;
        OpeningTemperature = openingTemperature;
        OpeningDiversity = openingDiversity;
        UrgentOpeningDiversity = urgentOpeningDiversity;
        OpeningMaximumScoreDifference = openingMaximumScoreDifference;
        MainMaximumScoreDifference = mainMaximumScoreDifference;
    }

    /// <summary>Gets the weights for the normalized candidate features.</summary>
    public CpuEvaluationWeights Weights { get; }

    /// <summary>Gets the nominal opening pool limit, subject to the two-choice minimum and cutoff ties.</summary>
    public int OpeningCandidateLimit { get; }

    /// <summary>Gets the nominal main-phase pool limit, subject to cutoff ties.</summary>
    public int MainCandidateLimit { get; }

    /// <summary>Gets main-phase temperature. Zero samples equally among the best-rated candidates.</summary>
    /// <remarks>Higher temperatures flatten the distribution within the retained pool.</remarks>
    public double MainTemperature { get; }

    /// <summary>Gets opening temperature. Higher values flatten the distribution within the pool.</summary>
    public double OpeningTemperature { get; }

    /// <summary>Gets the uniform share of the ordinary opening distribution.</summary>
    public double OpeningDiversity { get; }

    /// <summary>Gets the uniform share when two starts remain and neither player has a starting piece.</summary>
    public double UrgentOpeningDiversity { get; }

    /// <summary>Gets the maximum opening score gap in evaluation-score units, or null if disabled.</summary>
    public double? OpeningMaximumScoreDifference { get; }

    /// <summary>Gets the maximum main-phase score gap in evaluation-score units, or null if disabled.</summary>
    public double? MainMaximumScoreDifference { get; }

    private static void ValidateTemperature(double value, string parameter, bool allowZero)
    {
        if (!double.IsFinite(value) || value < 0 || value > 10 || (!allowZero && value == 0))
        {
            throw new ArgumentOutOfRangeException(parameter, value, "Invalid CPU selection temperature.");
        }
    }

    private static void ValidateDiversity(double value, string parameter)
    {
        if (!double.IsFinite(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(parameter, value, "Opening diversity must be finite and between 0 and 1.");
        }
    }

    private static void ValidateScoreDifference(double? value, string parameter)
    {
        if (value is double difference && (!double.IsFinite(difference) || difference < 0))
        {
            throw new ArgumentOutOfRangeException(parameter, value, "Maximum score difference must be finite and nonnegative.");
        }
    }
}

/// <summary>Provides the built-in tuning for each difficulty.</summary>
public static class CpuDifficultyProfiles
{
    private const double FormationWeight = 1.2;
    private const double SupportRiskWeight = 1.6;

    private static readonly CpuProfile[] Profiles =
    [
        new(WithSupport(new(2.2, 0.7, 1.5, 0.8, 0.3, 0.2, 0.5, 0.35)), 16, 12, 1.0, 0.9),
        new(WithSupport(new(2.2, 1.2, 1.6, 1.1, 0.6, 0.5, 0.8, 0.65)), 12, 8, 0.6, 0.75),
        new(WithSupport(new(2.0, 1.6, 1.8, 1.4, 0.9, 0.8, 1.1, 0.95)), 10, 5, 0.3, 0.65),
        new(WithSupport(new(2.0, 1.9, 1.9, 1.6, 1.1, 1.1, 1.3, 1.15)), 8, 3, 0.12, 0.55),
        new(WithSupport(new(2.0, 2.1, 2.0, 1.7, 1.2, 1.2, 1.4, 1.25)), 16, 1, 0, 0.5),
    ];

    private static CpuEvaluationWeights WithSupport(CpuEvaluationWeights weights) =>
        weights with { Formation = FormationWeight, SupportRisk = SupportRiskWeight };

    /// <summary>Gets the immutable configuration for a named difficulty.</summary>
    /// <param name="difficulty">One of the five supported values.</param>
    /// <returns>The corresponding built-in profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The difficulty is undefined.</exception>
    public static CpuProfile Get(CpuDifficulty difficulty) => (uint)difficulty < (uint)Profiles.Length
        ? Profiles[(int)difficulty]
        : throw new ArgumentOutOfRangeException(nameof(difficulty));
}
