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
            method.SetFinalValue(PricingFinalValue.Create(method.Id, 0m));
        }

        // Persist user-overridden final value adjusted (not recalculated by backend)
        method.FinalValue!.SetFinalValueOverride(command.FinalValueOverride);

        // Appraisal price (always persist — independent of building-cost toggle).
        // Applies to land cost (01/02), machinery cost (03), market, and with-building-cost.
        method.FinalValue.SetIndicatedValue(command.IndicatedValue);

        // The appraiser's typed-over total wins over whatever AppraisalValue/the calc service just
        // wrote to MethodValue above.
        method.SyncMethodValueWithIndicatedValue();

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
            var rate = method.ValuePerUnit ?? method.FinalValue.FinalValueOverride;
            var landValue = command.LandValue
                ?? (rate.HasValue ? landAreaFromTitles * rate.Value : (decimal?)null);

            if (landValue.HasValue)
                method.FinalValue.SetLandAreaValues(landAreaFromTitles, landValue.Value);
        }

        // Building value toggle (separate from IndicatedValue now).
        //
        // Cost approach: "include building" links to (or auto-creates) a real BuildingCost method
        // under this approach instead of trusting a client-supplied figure — see plan §2.2 / decision
        // 15 (WQS/SAG/DC "include building" -> Building Cost). Market approach is unchanged: it has
        // no Cost-approach role/rollup concept, so the client-supplied BuildingValue is still stored
        // as-is, exactly as before.
        var approach = pricingAnalysis.Approaches.First(a => a.Id == method.ApproachId);

        if (approach.ApproachType == "Cost")
        {
            if (command.HasBuildingValue == true)
            {
                if (pricingAnalysis.SubjectType != PricingAnalysisSubjectType.PropertyGroup
                    || !pricingAnalysis.AnchorId.HasValue)
                    throw new BadRequestException(
                        "Building cost can only be linked for a property-group pricing analysis.");

                var buildingCostValue = await propertyDataService.GetTotalBuildingCostAsync(
                    pricingAnalysis.AnchorId.Value, cancellationToken);

                // PROVISIONAL, pending a UX decision from the user: fails loudly instead of silently
                // creating a zero-value BuildingCost method or leaving the toggle at Role=Land. A
                // building value that goes missing from LOS/AS400 without anyone noticing is exactly
                // the failure mode this item exists to prevent, so the default is "block, don't guess".
                // Flip to a warning-and-proceed if the user decides differently — this is the only
                // place that decision needs to change.
                //
                // Scoped to the case it was written for: CREATING a BuildingCost method with nothing
                // to put in it. An approach that already has one carries a figure of record —
                // possibly one the appraiser typed — and blocking the save then would refuse work
                // over data that is present.
                var hasExistingBuildingCostValue = approach.Methods
                    .Any(m => m.MethodType == "BuildingCost" && m.MethodValue > 0m);

                if (buildingCostValue == 0m && !hasExistingBuildingCostValue)
                    throw new BadRequestException(
                        "This group has no building cost data yet. Add the building's construction " +
                        "cost and depreciation detail before including it here.");

                // Snapshot what the BuildingCost method actually holds, not the roll-up just
                // computed: when the method already existed, LinkOrCreateBuildingCostMethod leaves
                // the appraiser's own figure in place, and this source method's building component
                // has to agree with it or the two screens would print different totals.
                var buildingCostMethod = approach.LinkOrCreateBuildingCostMethod(method.Id, buildingCostValue);
                method.FinalValue.SetBuildingValue(buildingCostMethod.MethodValue ?? buildingCostValue);
            }
            else if (command.HasBuildingValue == false)
            {
                // Only undo what "include building" set: Unlink re-tags Role=Land, so calling it on
                // every toggle-off save would reset a role the appraiser picked on the board. A
                // LandAndBuilding method with NO link (the data fix tags one when the group had no
                // building total to link to) still claims the building, so it goes back to Land.
                approach.RevertToLand(method.Id);
                method.FinalValue.ClearBuildingValue();
            }
        }
        else
        {
            if (command.HasBuildingValue == true && command.BuildingValue.HasValue)
                method.FinalValue.SetBuildingValue(command.BuildingValue.Value);
            else if (command.HasBuildingValue == false)
                method.FinalValue.ClearBuildingValue();
        }

        // Roll the recalculated method value up through approach → analysis (null-safe, idempotent).
        pricingAnalysis.RecalculateRollup();

        // This save ran the backend calculation service — stamp only the method actually
        // recalculated. PricingAnalysis.UseSystemCalc (the group-level toggle) is the user's own
        // explicit control on the summary screen: force-writing it true here used to silently
        // overwrite a manual override an appraiser had just typed on a DIFFERENT method in this
        // same analysis via SetFinalValue — see PricingAnalysis.ContributingMethodsUseSystemCalc's
        // remarks for the decision to stop.
        method.RecordCalcMode(true);

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