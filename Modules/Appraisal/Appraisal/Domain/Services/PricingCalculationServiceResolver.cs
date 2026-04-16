namespace Appraisal.Domain.Services;

/// <summary>
/// Resolves the correct IPricingCalculationService for a given method type.
///
/// NOTE: <see cref="IncomeCalculationService"/> is injected via DI (scoped) so it can carry
/// a logger.  All other calc services remain as inline <c>new()</c> instances — broader
/// DI migration for those is deferred to a separate refactor.
/// </summary>
public class PricingCalculationServiceResolver(IncomeCalculationService incomeCalculationService)
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
            "Income" => incomeCalculationService,
            _ => null
        };
    }
}
