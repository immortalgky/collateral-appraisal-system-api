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

        // Recalculate (backend is source of truth)
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
        // as its base (FinalValue/FinalValueRounded ← calcResult).
        // User overrides (EstimatePriceRounded, AppraisalPrice) are persisted separately
        // via SetFinalValueAdjusted / SetAppraisalPrice below.
        var finalValue = method.FinalValue;
        if (finalValue is null)
        {
            finalValue = PricingFinalValue.Create(method.Id, calcResult.FinalValueRounded, calcResult.FinalValueRounded);
            method.SetFinalValue(finalValue);
        }
        else
        {
            finalValue.UpdateFinalValue(calcResult.FinalValueRounded, calcResult.FinalValueRounded);
        }

        // Building cost (optional)
        decimal? totalBuildingCost = null;
        decimal? appraisalPrice = null;

        if (command.IncludeBuildingCost && propertyData.TotalBuildingCost > 0)
        {
            totalBuildingCost = propertyData.TotalBuildingCost;
            var priceWithBuilding = finalPrice + totalBuildingCost.Value;
            appraisalPrice = command.AppraisalPrice ?? priceWithBuilding;

            finalValue.SetBuildingValue(totalBuildingCost.Value);
            finalValue.SetFinalValueAdjusted(command.FinalValueAdjusted);
            finalValue.SetAppraisalPrice(appraisalPrice.Value);

            // Propagate building-inclusive price upward
            method.SetValue(appraisalPrice.Value, null, PricingUnit.PerUnit);
        }
        else
        {
            if (finalValue.HasBuildingValue)
                finalValue.ClearBuildingValue();

            // Land area and building value are not applicable for the non-building ProfitRent path.
            finalValue.SetFinalValueAdjusted(command.FinalValueAdjusted);
            finalValue.SetAppraisalPrice(command.AppraisalPrice);
        }

        // Propagate value up if method is selected
        if (method.IsSelected && method.MethodValue.HasValue)
        {
            var parentApproach = pricingAnalysis.Approaches
                .First(a => a.Methods.Any(m => m.Id == method.Id));
            parentApproach.SetValue(method.MethodValue.Value);

            if (parentApproach.IsSelected)
                pricingAnalysis.SetFinalValues(parentApproach.ApproachValue!.Value);
        }

        return new SaveProfitRentAnalysisResult(
            command.PricingAnalysisId,
            command.MethodId,
            calcResult.TotalMarketRentalFee,
            calcResult.TotalContractRentalFee,
            calcResult.TotalReturnsFromLease,
            calcResult.TotalPresentValue,
            calcResult.FinalValueRounded,
            totalBuildingCost,
            appraisalPrice);
    }
}
