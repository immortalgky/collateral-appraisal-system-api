namespace Parameter.PricingParameters.Models;

/// <summary>
/// Assumption type descriptor — maps a code like "I01" to a display name and category.
/// </summary>
public class PricingParameterAssumptionType
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    /// <summary>"income" | "expenses" | "gop" | "fixedExps" | "other"</summary>
    public string Category { get; private set; } = null!;

    public int DisplaySeq { get; private set; }

    private PricingParameterAssumptionType()
    {
        // For EF Core
    }
}
