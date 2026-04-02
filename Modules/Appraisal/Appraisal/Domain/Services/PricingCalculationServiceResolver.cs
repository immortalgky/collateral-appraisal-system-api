namespace Appraisal.Domain.Services;

/// <summary>
/// Resolves the correct IPricingCalculationService for a given method type.
/// </summary>
public class PricingCalculationServiceResolver
{
    private readonly WqsCalculationService _wqs = new();
    private readonly SaleGridCalculationService _saleGrid = new();
    private readonly DirectComparisonCalculationService _directComparison = new();
    private readonly MachineryCostCalculationService _machineryCost = new();

    public IPricingCalculationService? Resolve(string methodType)
    {
        return methodType switch
        {
            "WQS" => _wqs,
            "SaleGrid" => _saleGrid,
            "DirectComparison" => _directComparison,
            "MachineryCost" => _machineryCost,
            _ => null
        };
    }
}
