using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.PricingAnalysis.SaveProfitRentAnalysis;

public class SaveProfitRentAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : ICommandHandler<SaveProfitRentAnalysisCommand, SaveProfitRentAnalysisResult>
{
    private readonly ProfitRentCalculationService _calcService = new();

    public async Task<SaveProfitRentAnalysisResult> Handle(
        SaveProfitRentAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
                                 command.PricingAnalysisId, cancellationToken)
                             ?? throw new InvalidOperationException(
                                 $"PricingAnalysis {command.PricingAnalysisId} not found");

        // Guard before any mutation: ProfitRent is only valid for PropertyGroup-subject analyses.
        if (pricingAnalysis.SubjectType != PricingAnalysisSubjectType.PropertyGroup
            || !pricingAnalysis.AnchorId.HasValue)
            throw new BadRequestException(
                "ProfitRent analysis is only supported for PropertyGroup-subject pricing analyses.");

        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == command.MethodId)
                     ?? throw new InvalidOperationException(
                         $"PricingAnalysisMethod {command.MethodId} not found");

        // Upsert profit rent analysis
        var analysis = method.ProfitRentAnalysis;
        if (analysis is null)
        {
            analysis = ProfitRentAnalysis.Create(method.Id);
            method.SetProfitRentAnalysis(analysis);
        }

        // Update input fields
        analysis.Update(
            command.MarketRentalFeePerSqWa,
            command.GrowthRateType,
            command.GrowthRatePercent,
            command.GrowthIntervalYears,
            command.DiscountRate,
            command.IncludeBuildingCost,
            command.EstimatePriceRounded);

        // Replace growth periods
        analysis.ClearGrowthPeriods();
        if (command.GrowthPeriods is not null)
        {
            foreach (var period in command.GrowthPeriods)
                analysis.AddGrowthPeriod(period.FromYear, period.ToYear, period.GrowthRatePercent);
        }

        // Set remark
        method.SetRemark(command.Remark);

        var propertyData = await propertyDataService.GetPropertyDataAsync(
            pricingAnalysis.AnchorId!.Value, cancellationToken);

        // Require at least one rental-bearing property (a lease-agreement property,
        // or plain land rented out to others) rather than silently producing an
        // all-zero result.
        if (propertyData.ContractSchedule.Count == 0)
            throw new BadRequestException(
                "ProfitRent analysis requires at least one property with a rental schedule "
                + "(a lease-agreement property, or land rented out to others) in the group.");

        // Build appraisal schedule
        var schedule = PricingPropertyDataService.BuildAppraisalSchedule(
            propertyData.ContractSchedule, propertyData.AppointmentDate);

        // Map to calculation service's record type
        var calcSchedule = schedule
            .Select(s => new ProfitRentCalculationService.AppraisalScheduleRow(s.Year, s.NumberOfMonths, s.ContractRentalFee))
            .ToList();

        // Recalculate (backend is source of truth).
        // TotalLandAreaInSqWa carries the NET appraisable area here (registered area less the
        // appraiser's deductions) under a historical name — pricing wants net, so this is correct.
        var calcResult = _calcService.Calculate(analysis, calcSchedule, propertyData.TotalLandAreaInSqWa);

        analysis.SetComputedValues(
            calcResult.TotalMarketRentalFee,
            calcResult.TotalContractRentalFee,
            calcResult.TotalReturnsFromLease,
            calcResult.TotalPresentValue);

        // Store full calculation table
        analysis.ClearTableRows();
        for (int i = 0; i < calcResult.Rows.Count; i++)
        {
            var r = calcResult.Rows[i];
            analysis.AddTableRow(ProfitRentCalculationDetail.Create(
                analysis.Id, i, r.Year, r.NumberOfMonths,
                r.MarketRentalFeePerSqWa, r.MarketRentalFeeGrowthPercent,
                r.MarketRentalFeePerMonth, r.MarketRentalFeePerYear,
                r.ContractRentalFeePerYear, r.ReturnsFromLease,
                r.PvFactor, r.PresentValue));
        }

        // Set method value (allow user override via EstimatePriceRounded)
        var finalPrice = command.EstimatePriceRounded ?? calcResult.FinalValueRounded;
        method.SetValue(finalPrice, null, PricingUnit.PerUnit);

        // Ensure the shared PricingFinalValue row always carries the calc-derived value
        // as its base (FinalValue ← calcResult).
        // User overrides (EstimatePriceRounded, IndicatedValue) are persisted separately
        // via SetFinalValueOverride / SetIndicatedValue below.
        var finalValue = method.FinalValue;
        if (finalValue is null)
        {
            finalValue = PricingFinalValue.Create(method.Id, calcResult.FinalValueRounded);
            method.SetFinalValue(finalValue);
        }
        else
        {
            finalValue.UpdateFinalValue(calcResult.FinalValueRounded);
        }

        // Building cost (optional). The group's building value is the Building Cost method's
        // SAVED value when there is one — the appraiser may have keyed it over the roll-up — else
        // the per-building roll-up (BuildingCostSql). Same rule as the screen and as the
        // WQS/SAG/DC building row (BuildingCostLink.tsx). Found by type across ALL approaches and
        // without an IsSelected filter: the BC method sits under Cost, not necessarily PR's own
        // approach, and is deselected on purpose once linked to a market method. 0 means "never
        // saved" (CostBuildingPanel re-seeds on it), so it falls back rather than pricing at zero.
        var bcMethodValue = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.MethodType == "BuildingCost")
            ?.MethodValue;
        var groupBuildingValue = bcMethodValue is > 0 ? bcMethodValue.Value : propertyData.TotalBuildingCost;

        decimal? totalBuildingCost = null;

        if (command.IncludeBuildingCost && groupBuildingValue > 0)
        {
            totalBuildingCost = groupBuildingValue;
            var priceWithBuilding = finalPrice + totalBuildingCost.Value;

            finalValue.SetBuildingValue(totalBuildingCost.Value);
            finalValue.SetFinalValueOverride(command.FinalValueOverride);
            finalValue.SetIndicatedValue(command.IndicatedValue);

            // Propagate building-inclusive price upward, then let the appraiser's typed-over total
            // (if any) win.
            method.SetValue(priceWithBuilding, null, PricingUnit.PerUnit);
        }
        else
        {
            if (finalValue.HasBuildingValue)
                finalValue.ClearBuildingValue();

            // Land area and building value are not applicable for the non-building ProfitRent path.
            finalValue.SetFinalValueOverride(command.FinalValueOverride);
            finalValue.SetIndicatedValue(command.IndicatedValue);

            // finalPrice already carries MethodValue here (set above); nothing further to propagate.
        }

        method.SyncMethodValueWithIndicatedValue();
        var indicatedValue = method.FinalValue!.IndicatedValue;

        // Roll the new method value up through approach → analysis (null-safe, idempotent).
        pricingAnalysis.RecalculateRollup();

        return new SaveProfitRentAnalysisResult(
            command.PricingAnalysisId,
            command.MethodId,
            calcResult.TotalMarketRentalFee,
            calcResult.TotalContractRentalFee,
            calcResult.TotalReturnsFromLease,
            calcResult.TotalPresentValue,
            calcResult.FinalValueRounded,
            totalBuildingCost,
            indicatedValue);
    }
}
