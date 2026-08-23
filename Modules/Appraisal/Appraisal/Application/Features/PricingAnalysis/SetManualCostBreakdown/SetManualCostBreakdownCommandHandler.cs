using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SetManualCostBreakdown;

/// <summary>
/// Records a hand-entered Cost-approach breakdown so the appraisal-summary report can print
/// ที่ดิน and สิ่งปลูกสร้าง as separate rows.
///
/// Manual pricing used to write only <c>PricingAnalysisMethods.MethodValue</c> (via UpdateMethod),
/// leaving no <c>PricingFinalValues</c> row. The summary report reaches ApproachType through an
/// OUTER APPLY that inner-joins that table, so a missing row makes the report treat a Cost group as
/// a blended Market group and print one combined "ที่ดินพร้อมสิ่งปลูกสร้าง" line. Writing the same
/// columns the calculated Cost+WQS path writes makes the report split with no reporting change.
///
/// The caller supplies only what the appraiser types — the land rate and the rounded price. Land
/// area comes from the group's land titles and the building figure from the depreciation schedule,
/// both resolved server-side, so the two numbers the report prints as separate rows cannot drift
/// from the property data behind them.
/// </summary>
public class SetManualCostBreakdownCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : ICommandHandler<SetManualCostBreakdownCommand, SetManualCostBreakdownResult>
{
    private const string CostApproachType = "Cost";

    public async Task<SetManualCostBreakdownResult> Handle(
        SetManualCostBreakdownCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        var approach = pricingAnalysis.Approaches
            .FirstOrDefault(a => a.Methods.Any(m => m.Id == command.MethodId));

        if (approach is null)
            throw new NotFoundException("PricingAnalysisMethod", command.MethodId);

        var method = approach.Methods.First(m => m.Id == command.MethodId);

        if (!string.Equals(approach.ApproachType, CostApproachType, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException(
                "A manual cost breakdown can only be recorded on a method under the Cost approach.");

        // Clearing the rate removes the breakdown entirely. The PricingFinalValues row itself has to
        // go: leaving it behind keeps ApproachType non-null, so the report would still split and print
        // a land row with an empty money cell — which reads as zero.
        if (command.LandRatePerSqWa is null or <= 0m)
        {
            method.ClearFinalValue();

            // SetValue is the only writer of ValuePerUnit/UnitType, so it has to run even when no
            // price comes with the clear. Skipping it leaves the method advertising a PerSqWa rate
            // whose breakdown was just deleted, and PricingUnit.IsPerUnitRate consumers — the
            // summary report's ราคาต่อหน่วย cell among them — act on that stale rate.
            method.SetValue(
                command.AppraisalPrice ?? method.MethodValue ?? 0m, null, PricingUnit.PerUnit);

            pricingAnalysis.RecalculateRollup();
            pricingAnalysis.SetUseSystemCalc(false);

            return new SetManualCostBreakdownResult(
                method.Id, null, null, null, null, null, 0m,
                command.AppraisalPrice, method.MethodValue,
                approach.ApproachValue, pricingAnalysis.FinalAppraisedValue);
        }

        // Land area and the building schedule are both scoped to a property group, so a reference
        // sub-analysis (machinery/income/leasehold anchors) has nothing to break down.
        if (pricingAnalysis.SubjectType != PricingAnalysisSubjectType.PropertyGroup
            || !pricingAnalysis.AnchorId.HasValue)
            throw new BadRequestException(
                "A manual cost breakdown is only available for a property-group pricing analysis.");

        var propertyGroupId = pricingAnalysis.AnchorId.Value;
        var rate = command.LandRatePerSqWa.Value;

        var landArea = await propertyDataService.GetTotalLandAreaFromTitlesAsync(
            propertyGroupId, cancellationToken);

        if (landArea is null or <= 0m)
            throw new BadRequestException(
                "This property group has no land area on its title deeds, so a land rate cannot be applied.");

        // The report sums its own สิ่งปลูกสร้าง lines from BuildingDepreciationDetails. Reading the
        // same table here keeps the stored BuildingValue — which the collateral master reads — equal
        // to the subtotal the report prints.
        var buildingValue = await propertyDataService.GetTotalBuildingCostAsync(
            propertyGroupId, cancellationToken);

        var landValue = landArea.Value * rate;
        var computedTotal = landValue + buildingValue;

        // The appraiser's rounded figure is the group total; without one the raw sum stands. Rounding
        // is the appraiser's call, not something to invent here.
        var appraisalPrice = command.AppraisalPrice ?? computedTotal;

        var finalValue = method.FinalValue;
        if (finalValue is null)
        {
            finalValue = PricingFinalValue.Create(method.Id, computedTotal, appraisalPrice);
            method.SetFinalValue(finalValue);
        }
        else
        {
            finalValue.UpdateFinalValue(computedTotal, appraisalPrice);
        }

        // FinalValueAdjusted is the column the summary report prints as ราคาต่อหน่วย.
        finalValue.SetFinalValueAdjusted(rate);
        finalValue.SetLandAreaValues(landArea.Value, landValue);

        if (buildingValue > 0m)
            finalValue.SetBuildingValue(buildingValue);
        else
            finalValue.ClearBuildingValue();

        finalValue.SetAppraisalPrice(appraisalPrice);

        // PerSqWa marks this as a land rate, matching the calculated Cost+WQS path.
        method.SetValue(appraisalPrice, rate, PricingUnit.PerSqWa);

        pricingAnalysis.RecalculateRollup();
        pricingAnalysis.SetUseSystemCalc(false);

        return new SetManualCostBreakdownResult(
            method.Id,
            finalValue.Id,
            rate,
            finalValue.LandArea,
            finalValue.LandValue,
            finalValue.BuildingValue,
            computedTotal,
            finalValue.AppraisalPrice,
            method.MethodValue,
            approach.ApproachValue,
            pricingAnalysis.FinalAppraisedValue);
    }
}
