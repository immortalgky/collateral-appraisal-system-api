using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.PricingAnalysis.SaveLeaseholdAnalysis;

public class SaveLeaseholdAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : ICommandHandler<SaveLeaseholdAnalysisCommand, SaveLeaseholdAnalysisResult>
{
    private readonly LeaseholdCalculationService _calcService = new();

    public async Task<SaveLeaseholdAnalysisResult> Handle(
        SaveLeaseholdAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
                                 command.PricingAnalysisId, cancellationToken)
                             ?? throw new InvalidOperationException(
                                 $"PricingAnalysis {command.PricingAnalysisId} not found");

        // Guard before any mutation: Leasehold is only valid for PropertyGroup-subject analyses.
        if (!pricingAnalysis.PropertyGroupId.HasValue)
            throw new BadRequestException(
                "Leasehold analysis is only supported for PropertyGroup-subject pricing analyses.");

        var method = pricingAnalysis.Approaches
                         .SelectMany(a => a.Methods)
                         .FirstOrDefault(m => m.Id == command.MethodId)
                     ?? throw new InvalidOperationException(
                         $"PricingAnalysisMethod {command.MethodId} not found");

        // Upsert leasehold analysis
        var analysis = method.LeaseholdAnalysis;
        if (analysis is null)
        {
            analysis = LeaseholdAnalysis.Create(method.Id);
            method.SetLeaseholdAnalysis(analysis);
        }

        // Update input fields
        analysis.Update(
            command.LandValuePerSqWa,
            command.LandGrowthRateType,
            command.LandGrowthRatePercent,
            command.LandGrowthIntervalYears,
            command.ConstructionCostIndex,
            command.InitialBuildingValue,
            command.DepreciationRate,
            command.DepreciationIntervalYears,
            command.BuildingCalcStartYear,
            command.DiscountRate);

        // Set remark
        method.SetRemark(command.Remark);

        // Replace growth periods
        analysis.ClearLandGrowthPeriods();
        if (command.LandGrowthPeriods is not null)
        {
            foreach (var period in command.LandGrowthPeriods)
            {
                analysis.AddLandGrowthPeriod(period.FromYear, period.ToYear, period.GrowthRatePercent);
            }
        }

        // Fetch rental schedule and property data using shared service.
        var propertyData = await propertyDataService.GetPropertyDataAsync(
            pricingAnalysis.PropertyGroupId.Value, cancellationToken);

        // Build appraisal schedule and map to leasehold record type
        var sharedSchedule = PricingPropertyDataService.BuildAppraisalSchedule(
            propertyData.ContractSchedule, propertyData.AppointmentDate);

        var appraisalSchedule = sharedSchedule
            .Select(s => new LeaseholdCalculationService.AppraisalScheduleRow(s.Year, s.ContractRentalFee))
            .ToList();

        // Recalculate computed values (backend is source of truth)
        var calcResult = _calcService.Calculate(analysis, appraisalSchedule, propertyData.TotalLandAreaInSqWa);

        analysis.SetComputedValues(
            calcResult.TotalIncomeOverLeaseTerm,
            calcResult.ValueAtLeaseExpiry,
            calcResult.FinalValue,
            calcResult.FinalValueRounded);

        // Store full calculation table
        analysis.ClearTableRows();
        for (int i = 0; i < calcResult.Rows.Count; i++)
        {
            var r = calcResult.Rows[i];
            analysis.AddTableRow(LeaseholdCalculationDetail.Create(
                analysis.Id, i, r.Year, r.LandValue, r.LandGrowthPercent,
                r.BuildingValue, r.DepreciationAmount, r.DepreciationPercent,
                r.BuildingAfterDepreciation, r.TotalLandAndBuilding,
                r.RentalIncome, r.PvFactor, r.NetCurrentRentalIncome));
        }

        // Handle partial usage
        decimal? computedEstimatePriceRounded;
        if (command.IsPartialUsage)
        {
            var (partialLandArea, partialLandPrice, estimateNetPrice, estimatePriceRounded) =
                LeaseholdCalculationService.CalculatePartialUsage(
                    calcResult.FinalValueRounded,
                    command.PartialRai, command.PartialNgan, command.PartialWa,
                    command.PricePerSqWa);

            computedEstimatePriceRounded = estimatePriceRounded;

            // Store user override if provided, otherwise use computed
            var finalEstimate = command.EstimatePriceRounded ?? estimatePriceRounded;

            analysis.SetPartialUsage(true,
                command.PartialRai, command.PartialNgan, command.PartialWa,
                partialLandArea, command.PricePerSqWa,
                partialLandPrice, estimateNetPrice, finalEstimate);
        }
        else
        {
            computedEstimatePriceRounded = null;

            // Store user override if provided
            analysis.SetPartialUsage(false, null, null, null, null, null, null, null,
                command.EstimatePriceRounded);
        }

        // Set method value: user override > partial estimate > final value rounded
        var computedEstimate = computedEstimatePriceRounded ?? calcResult.FinalValueRounded;
        var finalPrice = command.EstimatePriceRounded ?? computedEstimate;
        method.SetValue(finalPrice);

        // Propagate value up if method is selected
        if (method.IsSelected && method.MethodValue.HasValue)
        {
            var parentApproach = pricingAnalysis.Approaches
                .First(a => a.Methods.Any(m => m.Id == method.Id));
            parentApproach.SetValue(method.MethodValue.Value);

            if (parentApproach.IsSelected)
                pricingAnalysis.SetFinalValues(parentApproach.ApproachValue!.Value);
        }

        return new SaveLeaseholdAnalysisResult(
            command.PricingAnalysisId,
            command.MethodId,
            calcResult.TotalIncomeOverLeaseTerm,
            calcResult.ValueAtLeaseExpiry,
            calcResult.FinalValue,
            calcResult.FinalValueRounded);
    }
}
