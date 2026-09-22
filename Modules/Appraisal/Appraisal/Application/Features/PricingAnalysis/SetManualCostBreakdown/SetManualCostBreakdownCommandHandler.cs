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
/// The caller supplies only what the appraiser types — the land rate and the rounded land price.
/// Land area comes from the group's land titles, resolved server-side. The method prices LAND only:
/// the building is the Building Cost method's own line in the Cost total, not folded in here.
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

        // The breakdown is a LAND rate plus the building: it belongs on a land method. On the
        // BuildingCost method it would link the method to itself, and on MachineryCost it would
        // re-tag the machinery component LandAndBuilding and drop it from the rollup.
        if (method.MethodType is "BuildingCost" or "MachineryCost")
            throw new BadRequestException(
                $"A manual cost breakdown cannot be recorded on a {method.MethodType} method.");

        // Clearing the rate removes the breakdown entirely. The PricingFinalValues row itself has to
        // go: leaving it behind keeps ApproachType non-null, so the report would still split and print
        // a land row with an empty money cell — which reads as zero.
        //
        // Only NULL clears. A keyed 0 is a real land value of zero (a group priced on its structures
        // alone) and keeps its breakdown row, so the summary still splits ที่ดิน / สิ่งปลูกสร้าง and
        // prints 0.00 on the land line. The two gestures stay distinct on screen: emptying the input
        // sends null, typing 0 sends 0.
        // A price or rate below zero is never a valuation — it would drive the Cost total and
        // FinalAppraisedValue negative on their way to LOS/AS400.
        if (command.LandRatePerSqWa < 0m || command.IndicatedValue < 0m)
            throw new BadRequestException("The land rate and price cannot be negative.");

        if (command.LandRatePerSqWa is null)
        {
            // The whole breakdown (including any linked BuildingCost method) goes with it — a
            // cleared rate means there is no longer a land component to attach a building to.
            // RevertToLand covers a linked AND an unlinked LandAndBuilding method.
            approach.RevertToLand(method.Id);

            method.ClearFinalValue();

            // SetValue is the only writer of ValuePerUnit/UnitType, so it has to run even when no
            // price comes with the clear. Skipping it leaves the method advertising a PerSqWa rate
            // whose breakdown was just deleted, and PricingUnit.IsPerUnitRate consumers — the
            // summary report's ราคาต่อหน่วย cell among them — act on that stale rate.
            method.SetValue(
                command.IndicatedValue ?? method.MethodValue ?? 0m, null, PricingUnit.PerUnit);

            pricingAnalysis.RecalculateRollup();
            // Stamp only the method actually written — see PricingAnalysis.UseSystemCalc's remarks
            // for why the group-level toggle is deliberately left alone here.
            method.RecordCalcMode(false);

            return new SetManualCostBreakdownResult(
                method.Id, null, null, null, null, null, 0m,
                command.IndicatedValue, method.MethodValue,
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

        // LAND ONLY (user decision 2026-09-22: "เลิกผูก — ให้เป็นวิธี 'ที่ดิน' ล้วน"). The panel prices
        // the land; the building reaches the Cost total through the Building Cost method as its own
        // selected line, so this method stays Role=Land and folds nothing in.
        var landValue = landArea.Value * rate;
        var computedTotal = landValue;

        // The appraiser's rounded figure is the land total; without one the raw land value stands.
        // Rounding is the appraiser's call, not something to invent here.
        var indicatedValue = command.IndicatedValue ?? computedTotal;

        // FinalValueRounded means what its name says on every other method: the computed result with
        // the system's own rounding applied. This path used to store the appraiser's typed figure in
        // it instead, which made the column mean two different things depending on which screen wrote
        // the row, and left the typed number duplicated across FinalValueRounded and IndicatedValue.
        // The typed figure now lives in IndicatedValue alone — the column the report reads — while
        // MethodValue still carries it onward, so the appraised price is unchanged.
        // Nearest 1,000, halves away from zero: the same rule the cost card applies client-side when
        // it seeds the total field, so the stored figure equals the one the screen offered.
        var roundedTotal = Math.Round(computedTotal / 1000m, MidpointRounding.AwayFromZero) * 1000m;

        var finalValue = method.FinalValue;
        if (finalValue is null)
        {
            finalValue = PricingFinalValue.Create(method.Id, roundedTotal);
            method.SetFinalValue(finalValue);
        }
        else
        {
            finalValue.UpdateFinalValue(roundedTotal);
        }

        // FinalValueOverride is the column the summary report prints as ราคาต่อหน่วย.
        finalValue.SetFinalValueOverride(rate);
        finalValue.SetLandAreaValues(landArea.Value, landValue);

        // A method saved before the land-only decision may still be linked or tagged LandAndBuilding
        // (BuildingCost deselected, a BuildingValue snapshot on this row). Undo that: RevertToLand
        // re-tags it Land and brings the BuildingCost method back so the building is counted exactly
        // once, and the snapshot goes — a Land-role row carrying a BuildingValue would be read by the
        // book / LOS as this method's building component.
        approach.RevertToLand(method.Id);
        finalValue.ClearBuildingValue();

        finalValue.SetIndicatedValue(indicatedValue);

        // PerSqWa marks this as a land rate, matching the calculated Cost+WQS path.
        method.SetValue(indicatedValue, rate, PricingUnit.PerSqWa);

        pricingAnalysis.RecalculateRollup();
        // Stamp only the method actually written — see PricingAnalysis.UseSystemCalc's remarks for
        // why the group-level toggle is deliberately left alone here.
        method.RecordCalcMode(false);

        return new SetManualCostBreakdownResult(
            method.Id,
            finalValue.Id,
            rate,
            finalValue.LandArea,
            finalValue.LandValue,
            finalValue.BuildingValue,
            computedTotal,
            finalValue.IndicatedValue,
            method.MethodValue,
            approach.ApproachValue,
            pricingAnalysis.FinalAppraisedValue);
    }
}
