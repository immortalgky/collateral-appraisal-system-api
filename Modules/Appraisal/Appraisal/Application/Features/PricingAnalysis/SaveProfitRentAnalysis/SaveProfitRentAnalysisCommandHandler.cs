using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using Shared.CQRS;

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

        // Fetch property data using shared service
        var propertyData = await propertyDataService.GetPropertyDataAsync(
            pricingAnalysis.PropertyGroupId, cancellationToken);

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
            calcResult.TotalPresentValue,
            calcResult.FinalValueRounded);

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
        method.SetValue(finalPrice);

        // Handle building cost on PricingFinalValue
        decimal? totalBuildingCost = null;
        decimal? priceWithBuilding = null;
        decimal? priceWithBuildingRounded = null;

        if (command.IncludeBuildingCost && propertyData.TotalBuildingCost > 0)
        {
            totalBuildingCost = propertyData.TotalBuildingCost;
            priceWithBuilding = finalPrice + totalBuildingCost.Value;
            priceWithBuildingRounded = command.AppraisalPriceWithBuildingRounded ?? priceWithBuilding;

            var finalValue = method.FinalValue;
            if (finalValue is null)
            {
                finalValue = PricingFinalValue.Create(method.Id, finalPrice, finalPrice);
                method.SetFinalValue(finalValue);
            }
            else
            {
                finalValue.UpdateFinalValue(finalPrice, finalPrice);
            }

            finalValue.SetBuildingCost(
                totalBuildingCost.Value,
                priceWithBuilding.Value,
                priceWithBuildingRounded.Value);

            // Propagate building-inclusive price upward
            method.SetValue(priceWithBuildingRounded.Value);
        }
        else if (!command.IncludeBuildingCost && method.FinalValue?.HasBuildingCost == true)
        {
            method.FinalValue.ClearBuildingCost();
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
            priceWithBuilding,
            priceWithBuildingRounded);
    }
}
