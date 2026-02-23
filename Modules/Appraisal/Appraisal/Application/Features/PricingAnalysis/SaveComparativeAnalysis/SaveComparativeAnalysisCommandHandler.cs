namespace Appraisal.Application.Features.PricingAnalysis.SaveComparativeAnalysis;

/// <summary>
/// Handler for saving the entire comparative analysis in a single transaction.
/// Uses ID-based upsert: existing items are updated, new items are created, missing items are deleted.
/// </summary>
public class SaveComparativeAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<SaveComparativeAnalysisCommand, SaveComparativeAnalysisResult>
{
    public async Task<SaveComparativeAnalysisResult> Handle(
        SaveComparativeAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        // Load pricing analysis aggregate with all data
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new InvalidOperationException($"PricingAnalysis {command.PricingAnalysisId} not found");

        // Find the target method
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
            throw new InvalidOperationException($"PricingAnalysisMethod {command.MethodId} not found");

        // STEP 1: Upsert comparative factors
        UpsertComparativeFactors(method, command.ComparativeFactors);

        // STEP 2: Upsert factor scores
        UpsertFactorScores(method, command.FactorScores);

        // STEP 3: Update calculations
        UpdateCalculations(method, command.Calculations);

        // Save via repository - all changes committed in single transaction
        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new SaveComparativeAnalysisResult(
            command.PricingAnalysisId,
            command.MethodId,
            command.ComparativeFactors.Count,
            command.FactorScores.Count,
            command.Calculations.Count,
            true
        );
    }

    private static void UpsertComparativeFactors(
        PricingAnalysisMethod method,
        IReadOnlyList<ComparativeFactorInput> inputs)
    {
        // Get IDs from request (only existing items have IDs)
        var inputIds = inputs
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        // Get existing IDs
        var existingIds = method.ComparativeFactors
            .Select(f => f.Id)
            .ToHashSet();

        // Delete items not in request
        var idsToDelete = existingIds.Except(inputIds).ToList();
        foreach (var id in idsToDelete) method.RemoveComparativeFactor(id);

        // Update or create items
        foreach (var input in inputs)
            if (input.Id.HasValue)
            {
                // Update existing
                var existing = method.GetComparativeFactor(input.Id.Value);
                if (existing is not null)
                    existing.Update(input.DisplaySequence, input.IsSelectedForScoring, input.Remarks);
            }
            else
            {
                // Create new
                method.AddComparativeFactor(
                    input.FactorId,
                    input.DisplaySequence,
                    input.IsSelectedForScoring,
                    input.Remarks
                );
            }
    }

    private static void UpsertFactorScores(
        PricingAnalysisMethod method,
        IReadOnlyList<FactorScoreInput> inputs)
    {
        // Get IDs from request
        var inputIds = inputs
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        // Get existing IDs
        var existingIds = method.FactorScores
            .Select(f => f.Id)
            .ToHashSet();

        // Delete items not in request
        var idsToDelete = existingIds.Except(inputIds).ToList();
        foreach (var id in idsToDelete) method.RemoveFactorScore(id);

        // Update or create items
        foreach (var input in inputs)
            if (input.Id.HasValue)
            {
                // Update existing
                var existing = method.GetFactorScore(input.Id.Value);
                if (existing is not null)
                    existing.Update(
                        input.FactorWeight,
                        input.DisplaySequence,
                        input.Value,
                        input.Score,
                        input.AdjustmentPct,
                        input.Remarks
                    );
            }
            else
            {
                // Create new
                var factorScore = method.AddFactorScore(
                    input.FactorId,
                    input.FactorWeight,
                    input.DisplaySequence,
                    input.MarketComparableId
                );

                if (input.Value is not null || input.Score.HasValue)
                    factorScore.SetValues(input.Value, input.Score);

                if (input.AdjustmentPct.HasValue || input.Remarks is not null)
                    factorScore.SetAdjustment(input.AdjustmentPct, input.Remarks);
            }
    }

    private static void UpdateCalculations(
        PricingAnalysisMethod method,
        IReadOnlyList<CalculationInput> inputs)
    {
        foreach (var calcInput in inputs)
        {
            // Find or create calculation
            var calculation = method.Calculations
                .FirstOrDefault(c => c.MarketComparableId == calcInput.MarketComparableId);

            if (calculation is null) calculation = method.AddCalculation(calcInput.MarketComparableId);

            // Update calculation values
            if (calcInput.OfferingPrice.HasValue && calcInput.OfferingPriceUnit is not null)
                calculation.SetOfferingPrice(
                    calcInput.OfferingPrice.Value,
                    calcInput.OfferingPriceUnit,
                    calcInput.AdjustOfferPricePct
                );

            if (calcInput.SellingPrice.HasValue) calculation.SetSellingPrice(calcInput.SellingPrice.Value);

            if (calcInput.BuySellYear.HasValue || calcInput.AdjustedPeriodPct.HasValue)
                calculation.SetTimeAdjustment(
                    calcInput.BuySellYear,
                    calcInput.BuySellMonth,
                    calcInput.AdjustedPeriodPct,
                    calcInput.CumulativeAdjPeriod
                );

            if (calcInput.TotalAdjustedValue.HasValue)
                calculation.SetResult(calcInput.TotalAdjustedValue.Value);
        }
    }
}