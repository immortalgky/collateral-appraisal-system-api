namespace Parameter.PricingParameters.Models;

/// <summary>
/// Hotel job position lookup — used in Method09 (position-based salary calculation).
/// </summary>
public class PricingParameterJobPosition
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int DisplaySeq { get; private set; }

    private PricingParameterJobPosition()
    {
        // For EF Core
    }
}
