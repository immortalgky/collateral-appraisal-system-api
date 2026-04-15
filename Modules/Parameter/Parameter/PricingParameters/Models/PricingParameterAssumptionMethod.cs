namespace Parameter.PricingParameters.Models;

/// <summary>
/// Junction table: which method codes are allowed for each assumption type.
/// e.g. I01 → ["01","02","03","04","05"].
/// Composite PK: (AssumptionType, MethodTypeCode).
/// </summary>
public class PricingParameterAssumptionMethod
{
    public string AssumptionType { get; private set; } = null!;
    public string MethodTypeCode { get; private set; } = null!;

    private PricingParameterAssumptionMethod()
    {
        // For EF Core
    }
}
