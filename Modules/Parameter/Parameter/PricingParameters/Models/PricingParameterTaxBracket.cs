namespace Parameter.PricingParameters.Models;

/// <summary>
/// Tiered property-tax bracket used in Method10.
/// </summary>
public class PricingParameterTaxBracket
{
    /// <summary>Tier number (1-based), used as PK.</summary>
    public int Tier { get; private set; }

    /// <summary>Tax rate as a decimal fraction, e.g. 0.02 = 2%.</summary>
    public decimal TaxRate { get; private set; }

    public decimal MinValue { get; private set; }

    public decimal? MaxValue { get; private set; }

    private PricingParameterTaxBracket()
    {
        // For EF Core
    }
}
