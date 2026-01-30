namespace Appraisal.Application.Features.PricingAnalysis.GetComparativeFactors;

/// <summary>
/// Handler for getting comparative factors with their current selections and scores.
/// Fetches display names for factors and comparables.
/// </summary>
public class GetComparativeFactorsQueryHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IMarketComparableFactorRepository factorRepository,
    IMarketComparableRepository comparableRepository
) : IQueryHandler<GetComparativeFactorsQuery, GetComparativeFactorsResult>
{
    public async Task<GetComparativeFactorsResult> Handle(
        GetComparativeFactorsQuery query,
        CancellationToken cancellationToken)
    {
        // Load pricing analysis with all data
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            query.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new InvalidOperationException($"PricingAnalysis {query.PricingAnalysisId} not found");

        // Find the target method
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == query.MethodId);

        if (method is null)
            throw new InvalidOperationException($"PricingAnalysisMethod {query.MethodId} not found");

        // Collect all factor IDs to fetch names
        var factorIds = method.ComparativeFactors.Select(f => f.FactorId)
            .Union(method.FactorScores.Select(f => f.FactorId))
            .Distinct()
            .ToList();

        // Collect all market comparable IDs to fetch names
        var comparableIds = method.ComparableLinks.Select(l => l.MarketComparableId)
            .Union(method.FactorScores.Where(f => f.MarketComparableId.HasValue).Select(f => f.MarketComparableId!.Value))
            .Union(method.Calculations.Select(c => c.MarketComparableId))
            .Distinct()
            .ToList();

        // Fetch factor details
        var factors = factorIds.Count > 0
            ? (await factorRepository.GetByIdsAsync(factorIds, cancellationToken)).ToDictionary(f => f.Id)
            : new Dictionary<Guid, MarketComparableFactor>();

        // Fetch comparable details
        var comparables = new Dictionary<Guid, MarketComparable>();
        foreach (var id in comparableIds)
        {
            var comparable = await comparableRepository.GetByIdAsync(id, cancellationToken);
            if (comparable is not null)
                comparables[id] = comparable;
        }

        // Map linked comparables with names
        var linkedComparables = method.ComparableLinks
            .OrderBy(l => l.DisplaySequence)
            .Select(l =>
            {
                comparables.TryGetValue(l.MarketComparableId, out var comparable);
                return new LinkedComparableDto(
                    l.Id,
                    l.MarketComparableId,
                    l.DisplaySequence,
                    comparable?.ComparableNumber,
                    comparable?.ComparableNumber // Using ComparableNumber as code too
                );
            })
            .ToList();

        // Map comparative factors (Step 1) with names
        var comparativeFactors = method.ComparativeFactors
            .OrderBy(f => f.DisplaySequence)
            .Select(f =>
            {
                factors.TryGetValue(f.FactorId, out var factor);
                return new ComparativeFactorDto(
                    f.Id,
                    f.FactorId,
                    factor?.FactorName,
                    factor?.FactorCode,
                    f.DisplaySequence,
                    f.IsSelectedForScoring,
                    f.Remarks
                );
            })
            .ToList();

        // Map factor scores (Step 2) with names
        var factorScores = method.FactorScores
            .OrderBy(f => f.DisplaySequence)
            .ThenBy(f => f.MarketComparableId)
            .Select(f =>
            {
                factors.TryGetValue(f.FactorId, out var factor);
                string? comparableName = null;
                if (f.MarketComparableId.HasValue && comparables.TryGetValue(f.MarketComparableId.Value, out var comparable))
                {
                    comparableName = comparable.ComparableNumber;
                }

                return new FactorScoreDto(
                    f.Id,
                    f.FactorId,
                    factor?.FactorName,
                    f.MarketComparableId,
                    comparableName,
                    f.FactorWeight,
                    f.DisplaySequence,
                    f.Value,
                    f.Score,
                    f.WeightedScore,
                    f.AdjustmentPct,
                    f.Remarks
                );
            })
            .ToList();

        // Map calculations with names
        var calculations = method.Calculations
            .Select(c =>
            {
                comparables.TryGetValue(c.MarketComparableId, out var comparable);
                return new CalculationDto(
                    c.Id,
                    c.MarketComparableId,
                    comparable?.ComparableNumber,
                    c.OfferingPrice,
                    c.OfferingPriceUnit,
                    c.AdjustOfferPricePct,
                    c.SellingPrice,
                    c.BuySellYear,
                    c.BuySellMonth,
                    c.AdjustedPeriodPct,
                    c.CumulativeAdjPeriod,
                    c.TotalFactorDiffPct,
                    c.TotalAdjustedValue
                );
            })
            .ToList();

        return new GetComparativeFactorsResult(
            query.PricingAnalysisId,
            query.MethodId,
            method.MethodType,
            linkedComparables,
            comparativeFactors,
            factorScores,
            calculations
        );
    }
}
