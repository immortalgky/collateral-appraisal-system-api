using Appraisal.Application.Services;
using Appraisal.Domain.MarketComparables;
using Appraisal.Domain.Services;

namespace Appraisal.Application.Features.PricingAnalysis.SaveComparativeAnalysis;

/// <summary>
/// Handler for saving the entire comparative analysis in a single transaction.
/// Uses ID-based upsert: existing items are updated, new items are created, missing items are deleted.
/// After upsert, backend recalculates all derived fields as the "gate of truth".
/// </summary>
public class SaveComparativeAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IMarketComparableRepository marketComparableRepository,
    PricingCalculationServiceResolver calculationServiceResolver,
    PricingPropertyDataService propertyDataService
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
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        // Find the target method
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
            throw new NotFoundException("PricingAnalysisMethod", command.MethodId);

        // Persist template selection
        method.SetComparativeAnalysisTemplate(command.ComparativeAnalysisTemplateId);

        // Persist remark (parity with Hypothesis/ProfitRent/MachineCost/Leasehold saves)
        if (command.Remark is not null)
            method.SetRemark(command.Remark);

        // STEP 1: Upsert comparative factors
        UpsertComparativeFactors(method, command.ComparativeFactors);

        // STEP 2: Upsert factor scores
        UpsertFactorScores(method, command.FactorScores);

        // STEP 3: Update calculations
        UpdateCalculations(method, command.Calculations);

        // STEP 3.5: Auto-compute BuySellYear/Month from MarketComparable.SaleDate
        await ApplyTimeAdjustmentsFromSaleDate(method, cancellationToken);

        // STEP 4: Recalculate derived fields (backend as gate of truth)
        var calculationService = calculationServiceResolver.Resolve(method.MethodType);
        calculationService?.Recalculate(method);

        // Override method value with appraisal value if provided.
        // Preserve the price unit the calc service just resolved (single-arg SetValue would
        // null UnitType/ValuePerUnit); fall back to re-resolving when no calc ran.
        if (command.AppraisalValue.HasValue)
        {
            var unitType = method.UnitType ?? PricingCalculationHelper.ResolvePriceUnit(method.Calculations);
            var valuePerUnit = PricingUnit.IsPerUnitRate(unitType) ? method.ValuePerUnit : null;
            method.SetValue(command.AppraisalValue.Value, valuePerUnit, unitType);
        }

        // Ensure a FinalValue row exists so user overrides persist even when
        // the calc service couldn't auto-create one (e.g. WQS with < 2 data points).
        if (method.FinalValue is null)
        {
            method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m, 0m));
        }

        // Persist user-overridden final value adjusted (not recalculated by backend)
        method.FinalValue!.SetFinalValueAdjusted(command.FinalValueAdjusted);

        // Appraisal price (always persist — independent of building-cost toggle).
        // Applies to land cost (01/02), machinery cost (03), market, and with-building-cost.
        method.FinalValue.SetAppraisalPrice(command.AppraisalPrice);

        // Land area + land value. A per-unit RATE (PerSqWa/PerSqm) means the final value prices
        // LAND per unit area, so both are derivable and must NOT be gated on the building-cost
        // toggle (the old guard also required command.LandValue, which the WQS screen never sends
        // unless building cost is on — so land area was silently never persisted).
        // The area is authoritative from the property's land titles, never from the request.
        // PerUnit is a whole-unit lumpsum carrying no land rate → leave the row alone.
        // An explicit command.LandValue still wins (cost approach enters it by hand).
        decimal? totalLandAreaFromTitles = null;
        if (pricingAnalysis.SubjectType == PricingAnalysisSubjectType.PropertyGroup
            && pricingAnalysis.AnchorId.HasValue)
            totalLandAreaFromTitles = await propertyDataService.GetTotalLandAreaFromTitlesAsync(
                pricingAnalysis.AnchorId.Value, cancellationToken);

        var landAreaFromTitles = totalLandAreaFromTitles ?? 0m;

        if (command.IncludeLandArea == false)
        {
            method.FinalValue.ExcludeLandArea();
        }
        else if (PricingUnit.IsPerUnitRate(method.UnitType) && landAreaFromTitles > 0m)
        {
            var rate = method.ValuePerUnit ?? method.FinalValue.FinalValueAdjusted;
            var landValue = command.LandValue
                ?? (rate.HasValue ? landAreaFromTitles * rate.Value : (decimal?)null);

            if (landValue.HasValue)
                method.FinalValue.SetLandAreaValues(landAreaFromTitles, landValue.Value);
        }

        // Building value toggle (separate from AppraisalPrice now).
        if (command.HasBuildingValue == true && command.BuildingValue.HasValue)
            method.FinalValue.SetBuildingValue(command.BuildingValue.Value);
        else if (command.HasBuildingValue == false)
            method.FinalValue.ClearBuildingValue();

        // Propagate: if method is selected and has a value, push it up
        if (method.IsSelected && method.MethodValue.HasValue)
        {
            var parentApproach = pricingAnalysis.Approaches
                .FirstOrDefault(a => a.Id == method.ApproachId)
                ?? throw new InvalidOperationException(
                    $"Approach {method.ApproachId} not found in pricing analysis {command.PricingAnalysisId}");

            parentApproach.SetValue(method.MethodValue.Value);

            // If approach is also selected, propagate to FinalAppraisedValue
            if (parentApproach.IsSelected)
            {
                pricingAnalysis.SetFinalValues(parentApproach.ApproachValue!.Value);
            }
        }

        // TODO: Temporary — mark as system calc since this is a backend-calculated save
        pricingAnalysis.SetUseSystemCalc(true);

        // Changes are saved by TransactionalBehavior's SaveChangesAsync.
        // Do NOT call DbSet.Update() here — the aggregate is already tracked,
        // and Update() would override new child entities' Added state to Modified.

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
                    existing.Update(input.DisplaySequence, input.IsSelectedForScoring, input.Remarks, input.CollateralValue);
            }
            else
            {
                // Create new
                method.AddComparativeFactor(
                    input.FactorId,
                    input.DisplaySequence,
                    input.IsSelectedForScoring,
                    input.Remarks,
                    input.CollateralValue
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
                        input.Remarks,
                        input.Intensity,
                        input.AdjustmentAmt,
                        input.ComparisonResult
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

                if (input.Value is not null || input.Score.HasValue || input.Intensity.HasValue)
                    factorScore.SetValues(input.Value, input.Score, input.Intensity);

                if (input.AdjustmentPct.HasValue || input.AdjustmentAmt.HasValue || input.ComparisonResult is not null || input.Remarks is not null)
                    factorScore.SetAdjustment(input.AdjustmentPct, input.AdjustmentAmt, input.ComparisonResult, input.Remarks);
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

            // Update calculation values — clear mutually exclusive price path
            if (calcInput.OfferingPrice.HasValue && calcInput.OfferingPriceUnit is not null)
            {
                calculation.SetOfferingPrice(
                    calcInput.OfferingPrice.Value,
                    calcInput.OfferingPriceUnit,
                    calcInput.AdjustOfferPricePct,
                    calcInput.AdjustOfferPriceAmt
                );
                if (!calcInput.SellingPrice.HasValue)
                    calculation.ClearSellingPrice();
            }

            if (calcInput.SellingPrice.HasValue)
            {
                calculation.SetSellingPrice(calcInput.SellingPrice.Value, calcInput.SellingPriceUnit);
                if (!calcInput.OfferingPrice.HasValue)
                    calculation.ClearOfferingPrice();
            }

            if (calcInput.BuySellYear.HasValue || calcInput.AdjustedPeriodPct.HasValue)
                calculation.SetTimeAdjustment(
                    calcInput.BuySellYear,
                    calcInput.BuySellMonth,
                    calcInput.AdjustedPeriodPct,
                    calcInput.CumulativeAdjPeriod
                );

            if (calcInput.LandAreaDeficient.HasValue || calcInput.LandPrice.HasValue)
                calculation.SetLandAdjustment(
                    calcInput.LandAreaDeficient,
                    calcInput.LandAreaDeficientUnit,
                    calcInput.LandPrice,
                    calcInput.LandValueAdjustment
                );

            if (calcInput.UsableAreaDeficient.HasValue || calcInput.UsableAreaPrice.HasValue)
                calculation.SetBuildingAdjustment(
                    calcInput.UsableAreaDeficient,
                    calcInput.UsableAreaDeficientUnit,
                    calcInput.UsableAreaPrice,
                    calcInput.BuildingValueAdjustment
                );

            if (calcInput.TotalFactorDiffPct.HasValue || calcInput.TotalFactorDiffAmt.HasValue)
                calculation.SetFactorAdjustment(calcInput.TotalFactorDiffPct, calcInput.TotalFactorDiffAmt);

            if (calcInput.TotalAdjustedValue.HasValue)
                calculation.SetResult(calcInput.TotalAdjustedValue.Value);

            if (calcInput.Weight.HasValue || calcInput.WeightedAdjustedValue.HasValue)
                calculation.SetWeight(calcInput.Weight, calcInput.WeightedAdjustedValue);
        }
    }

    private async Task ApplyTimeAdjustmentsFromSaleDate(
        PricingAnalysisMethod method,
        CancellationToken cancellationToken)
    {
        var comparableIds = method.Calculations
            .Select(c => c.MarketComparableId)
            .Distinct()
            .ToList();

        // Batch-load all comparables to avoid N+1
        var comparables = new Dictionary<Guid, MarketComparable>();
        foreach (var id in comparableIds)
        {
            var comparable = await marketComparableRepository.GetByIdAsync(id, cancellationToken);
            if (comparable is not null)
                comparables[id] = comparable;
        }

        foreach (var calc in method.Calculations)
        {
            if (!comparables.TryGetValue(calc.MarketComparableId, out var comparable))
                continue;

            if (!comparable.SaleDate.HasValue)
                continue;

            var (years, months) = PricingCalculationHelper.ComputeTimeFromSaleDate(comparable.SaleDate.Value);
            var cumulative = PricingCalculationHelper.ComputeCumulativeAdjPeriod(years, calc.AdjustedPeriodPct);
            calc.SetTimeAdjustment(years, months, calc.AdjustedPeriodPct, cumulative);
        }
    }
}