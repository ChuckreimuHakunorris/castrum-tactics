namespace CastrumTactics.AI;

/// <summary>Describes normalized tactical feature values or the corresponding profile weights.</summary>
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
    internal double Score(CpuFeatures weights) =>
        Expansion * weights.Expansion + Territory * weights.Territory + Capture * weights.Capture +
        Fortification * weights.Fortification + Mobility * weights.Mobility + Bottleneck * weights.Bottleneck +
        Spread * weights.Spread - Risk * weights.Risk;

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
