using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Handler for updating final value
/// </summary>
public class UpdateFinalValueCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : ICommandHandler<UpdateFinalValueCommand, UpdateFinalValueResult>
{
    public async Task<UpdateFinalValueResult> Handle(
        UpdateFinalValueCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        // Find the method with matching final value ID
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.FinalValue?.Id == command.FinalValueId);

        if (method?.FinalValue is null)
            throw new NotFoundException("PricingFinalValue", command.FinalValueId);

        var finalValue = method.FinalValue;

        // Update final value. One figure now: the request used to carry a raw and a rounded value,
        // and only the rounded one ever became the method value.
        finalValue.UpdateFinalValue(command.FinalValue);
        // Preserve the existing price unit — this manual override adjusts the value, not the unit.
        method.SetValue(command.FinalValue, method.ValuePerUnit, method.UnitType);

        // The appraiser just typed this method's value — stamp only the method actually written.
        // PricingAnalysis.UseSystemCalc (the group-level toggle) is the user's own explicit control
        // on the summary screen and is deliberately left alone here: see its remarks and
        // PricingAnalysis.ContributingMethodsUseSystemCalc for why a save handler must not
        // force-write it as a side effect.
        method.RecordCalcMode(false);

        // Handle land area. Only PerSqWa is a LAND rate: area x rate is derivable and must NOT be gated
        // on the building-cost toggle. Area is authoritative from the property's land titles, never
        // from the request. A Role=Land lumpsum (PerUnit) or PerSqm row (quoted against the usable
        // area, not a land rate) records the area plus the IndicatedValue as its land value. A
        // non-Land PerSqm row is "explicit or clear" in Cost and "clear" in Market, as is a Cost
        // PerUnit row that is not Land but has a building in it; a Cost PerUnit row that is not Land
        // and has no building is left alone. An explicit command.LandValue (cost approach enters it
        // by hand) applies only on PerSqWa, or on those Cost non-Land rows, and only when > 0.
        // null means ONLY "a reference analysis": its row is left untouched. A property group is a
        // known area — a group with no land properties (condo, building, machinery) is 0 — and so is a
        // project model, which has no titles (0); a known 0 excludes or clears the land figures.
        decimal? totalLandAreaFromTitles = pricingAnalysis.IsReference ? null : 0m;
        if (pricingAnalysis.SubjectType == PricingAnalysisSubjectType.PropertyGroup
            && pricingAnalysis.AnchorId.HasValue)
            totalLandAreaFromTitles = await propertyDataService.GetTotalLandAreaFromTitlesAsync(
                pricingAnalysis.AnchorId.Value, cancellationToken) ?? 0m;

        // Approach type, not method.Role: Role is also null on cost rows that predate it, and
        // treating those as market would clear a land value that is real.
        var isCostApproach = pricingAnalysis.Approaches
            .First(a => a.Id == method.ApproachId).ApproachType == "Cost";

        // Captured before the building block below flips it — see the parameter's remarks.
        var buildingWasPresentBeforeThisSave = finalValue.HasBuildingValue;

        // Handle building value (toggle + amount)
        if (command.HasBuildingValue == true && command.BuildingValue.HasValue)
        {
            finalValue.SetBuildingValue(command.BuildingValue.Value);
        }
        else if (command.HasBuildingValue == false)
        {
            finalValue.ClearBuildingValue();
        }

        // Indicated value (now persisted independently of the building-cost toggle)
        if (command.IndicatedValue.HasValue)
        {
            finalValue.SetIndicatedValue(command.IndicatedValue.Value);
        }

        // After this save's building block and IndicatedValue, not before them: on a Role=Land
        // lumpsum the land value IS the indicated value, so running it first wrote the PREVIOUS
        // save's figure (and, with a building involved, an old land+building total).
        method.ApplyLandAreaValue(
            totalLandAreaFromTitles, command.LandValue, command.IncludeLandArea, isCostApproach,
            buildingWasPresentBeforeThisSave);

        // MethodValue = IndicatedValue ?? FinalValue, then roll up through approach → analysis.
        // RecalculateRollup, not a direct approach.SetValue(this method): a Cost approach sums its
        // selected methods (Land + Building), so one method's value is not the approach's.
        method.SyncMethodValueWithIndicatedValue();
        // A Role=Land method's land IS its indicated value — settle that here, after the typed
        // figure is in, so every reader of LandValue gets one answer.
        method.SyncLandValueWithIndicatedValue(buildingWasPresentBeforeThisSave);
        pricingAnalysis.RecalculateRollup();

        return new UpdateFinalValueResult(
            finalValue.Id,
            finalValue.FinalValue,
            finalValue.IncludeLandArea,
            finalValue.LandArea,
            finalValue.LandValue,
            finalValue.HasBuildingValue,
            finalValue.BuildingValue,
            finalValue.IndicatedValue
        );
    }
}
