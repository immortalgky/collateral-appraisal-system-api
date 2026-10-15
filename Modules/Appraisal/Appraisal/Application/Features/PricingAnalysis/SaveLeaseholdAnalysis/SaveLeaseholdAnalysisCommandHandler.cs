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
        if (pricingAnalysis.SubjectType != PricingAnalysisSubjectType.PropertyGroup
            || !pricingAnalysis.AnchorId.HasValue)
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
        
        // Update partial usage (lease land area) to calculate leashold table first
        if (command.IsPartialUsage)
        {
            analysis.SetPartialUsage(true,
                  command.PartialRai, command.PartialNgan, command.PartialWa,
                  (command.PartialRai ?? 0) * 400m + (command.PartialNgan ?? 0) * 100m + (command.PartialWa ?? 0), command.PricePerSqWa,
                  null, null, null
                  );
        } else {
            analysis.SetPartialUsage(false, null, null, null, null, null, null, null, null );
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
            pricingAnalysis.AnchorId!.Value, cancellationToken);

        // Require at least one rental-bearing property (a lease-agreement property,
        // or plain land rented out to others) rather than silently producing an
        // all-zero result.
        if (propertyData.ContractSchedule.Count == 0)
            throw new BadRequestException(
                "Leasehold analysis requires at least one property with a rental schedule "
                + "(a lease-agreement property, or land rented out to others) in the group.");

        // Build appraisal schedule and map to leasehold record type
        var sharedSchedule = PricingPropertyDataService.BuildAppraisalSchedule(
            propertyData.ContractSchedule, propertyData.AppointmentDate);

        var appraisalSchedule = sharedSchedule
            .Select(s => new LeaseholdCalculationService.AppraisalScheduleRow(s.Year, s.ContractRentalFee))
            .ToList();

        // Recalculate computed values (backend is source of truth).
        // TotalLandAreaInSqWa carries the NET appraisable area here (registered area less the
        // appraiser's deductions) under a historical name — pricing wants net, so this is correct.
        var calcResult = _calcService.Calculate(analysis, appraisalSchedule, propertyData.TotalLandAreaInSqWa);

        analysis.SetComputedValues(
            calcResult.TotalIncomeOverLeaseTerm,
            calcResult.ValueAtLeaseExpiry);

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

        // Handle partial usage. EstimatePriceRounded stores the SYSTEM-COMPUTED partial estimate
        // only — never the appraiser's override. Mixing the two into this one column meant a later
        // recalc without a resent override silently erased whatever the appraiser had typed. The
        // override lives in FinalValue.IndicatedValue (below), same as every other method.
        decimal? computedEstimatePriceRounded;
        if (command.IsPartialUsage)
        {
            // PricePerSqWa prices the REMAINING (non-leased) land for this partial-usage estimate —
            // a different figure than LandValuePerSqWa, which feeds the land-growth model over the
            // lease term. Passing LandValuePerSqWa here priced the remainder at the wrong rate.
            // The total it is subtracted from — propertyData.TotalLandAreaInSqWa — is the NET
            // appraisable area despite its name, which is what the remainder should be taken from.
            var (partialLandArea, partialLandPrice, estimateNetPrice, estimatePriceRounded) =
                LeaseholdCalculationService.CalculatePartialUsage(
                    calcResult.FinalValueRounded,
                    command.PartialRai, command.PartialNgan, command.PartialWa,
                    command.PricePerSqWa, propertyData.TotalLandAreaInSqWa);

            computedEstimatePriceRounded = estimatePriceRounded;

            analysis.SetPartialUsage(true,
                command.PartialRai, command.PartialNgan, command.PartialWa,
                partialLandArea, command.PricePerSqWa,
                partialLandPrice, estimateNetPrice, estimatePriceRounded);
        }
        else
        {
            computedEstimatePriceRounded = null;
            analysis.SetPartialUsage(false, null, null, null, null, null, null, null, null);
        }

        // Set method value: user override > partial estimate > final value rounded.
        // command.EstimatePriceRounded is the client's continuously-refreshed computed figure, not
        // an override input — falling back to it here would stamp IndicatedValue as "overridden" on
        // every save, even when the appraiser never touched anything. IndicatedValue is the only
        // override field; null means no override, same as every other method.
        var computedEstimate = computedEstimatePriceRounded ?? calcResult.FinalValueRounded;
        method.SetValue(computedEstimate, null, PricingUnit.PerUnit);

        // Mirror the committed final value into the shared PricingFinalValue (single source of truth).
        // Land area and building value are not applicable for Leasehold.
        if (method.FinalValue is null)
            method.SetFinalValue(PricingFinalValue.Create(method.Id, calcResult.FinalValueRounded));
        else
            method.FinalValue.UpdateFinalValue(calcResult.FinalValueRounded);
        method.FinalValue!.SetFinalValueOverride(command.FinalValueOverride);
        method.FinalValue.SetIndicatedValue(command.IndicatedValue);
        method.SyncMethodValueWithIndicatedValue();

        // Roll the new method value up through approach → analysis (null-safe, idempotent).
        pricingAnalysis.RecalculateRollup();

        return new SaveLeaseholdAnalysisResult(
            command.PricingAnalysisId,
            command.MethodId,
            calcResult.TotalIncomeOverLeaseTerm,
            calcResult.ValueAtLeaseExpiry,
            calcResult.FinalValue,
            calcResult.FinalValueRounded);
    }
}
