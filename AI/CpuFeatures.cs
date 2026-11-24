namespace CastrumTactics.AI;

/// <summary>Describes tactical feature values that the evaluator normalizes to the range 0 through 1.</summary>
/// <param name="Expansion">Nearby free space or new coverage beyond existing friendly reach.</param>
/// <param name="Territory">Reachable opening region or potential influence over nearby contested squares.</param>
/// <param name="Capture">Value of removing an opposing knight, including whether a knight survives the capture.</param>
/// <param name="Fortification">Value of protecting a friendly knight by upgrading it.</param>
/// <param name="Mobility">Estimated loss of enemy options when removing their only nearby source.</param>
/// <param name="Bottleneck">Local importance of a connecting square.</param>
/// <param name="Spread">Draft access to regions that are poorly covered by friendly knights.</param>
/// <param name="Risk">Potential collision or exposure cost. Its weighted value is subtracted.</param>
public readonly record struct CpuFeatures(double Expansion, double Territory, double Capture, double Fortification,
    double Mobility, double Bottleneck, double Spread, double Risk)
{
    internal double Score(CpuEvaluationWeights weights) =>
        Expansion * weights.Expansion + Territory * weights.Territory + Capture * weights.Capture +
        Fortification * weights.Fortification + Mobility * weights.Mobility + Bottleneck * weights.Bottleneck +
        Spread * weights.Spread - Risk * weights.Risk;
}

/// <summary>Defines the relative importance of each feature. A CPU profile requires finite weights from 0 through 10.</summary>
/// <param name="Expansion">Weight for free space and new reach.</param>
/// <param name="Territory">Weight for regional access and contested influence.</param>
/// <param name="Capture">Weight for removing opposing knights.</param>
/// <param name="Fortification">Weight for upgrading friendly knights.</param>
/// <param name="Mobility">Weight for denying opposing options.</param>
/// <param name="Bottleneck">Weight for locally important connecting squares.</param>
/// <param name="Spread">Weight for access beyond existing friendly coverage.</param>
/// <param name="Risk">Weight for collision and exposure costs, subtracted from the score.</param>
public readonly record struct CpuEvaluationWeights(double Expansion, double Territory, double Capture, double Fortification,
    double Mobility, double Bottleneck, double Spread, double Risk)
{
    internal void ValidateWeights()
    {
        Validate(Expansion, nameof(Expansion));
        Validate(Territory, nameof(Territory));
        Validate(Capture, nameof(Capture));
        Validate(Fortification, nameof(Fortification));
        Validate(Mobility, nameof(Mobility));
        Validate(Bottleneck, nameof(Bottleneck));
        Validate(Spread, nameof(Spread));
        Validate(Risk, nameof(Risk));

        static void Validate(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0 || value > 10)
            {
                throw new ArgumentOutOfRangeException(name, value, "Weight must be finite and between 0 and 10.");
            }
        }
    }
}
