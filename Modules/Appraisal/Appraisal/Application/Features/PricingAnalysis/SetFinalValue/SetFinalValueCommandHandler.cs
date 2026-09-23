using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SetFinalValue;

/// <summary>
/// Handler for setting final value for a pricing method
/// </summary>
public class SetFinalValueCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    PricingPropertyDataService propertyDataService
) : ICommandHandler<SetFinalValueCommand, SetFinalValueResult>
{
    public async Task<SetFinalValueResult> Handle(
        SetFinalValueCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        // Find the method
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
            throw new NotFoundException("PricingAnalysisMethod", command.MethodId);

        // Create or update final value
        PricingFinalValue finalValue;
        if (method.FinalValue is null)
        {
            // Create new final value
            finalValue = PricingFinalValue.Create(
                command.MethodId,
                command.FinalValue);

            method.SetFinalValue(finalValue);
        }
        else
        {
            // Update existing final value
            finalValue = method.FinalValue;
            finalValue.UpdateFinalValue(command.FinalValue);
        }

        // Preserve the existing price unit — this manual override adjusts the value, not the unit.
        method.SetValue(command.FinalValue, method.ValuePerUnit, method.UnitType);

        // The appraiser just typed this method's value — stamp only the method actually written.
        // PricingAnalysis.UseSystemCalc (the group-level toggle) is the user's own explicit control
        // on the summary screen and is deliberately left alone here: see its remarks and
        // PricingAnalysis.ContributingMethodsUseSystemCalc for why a save handler must not
        // force-write it as a side effect.
        method.RecordCalcMode(false);

        // Handle land area. A per-unit RATE (PerSqWa/PerSqm) means the final value prices LAND per
        // unit area, so area and value are derivable and must NOT be gated on the building-cost
        // toggle. Area is authoritative from the property's land titles, never from the request.
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
            finalValue.ExcludeLandArea();
        }
        else
        {
            // One rule, one place: see PricingAnalysisMethod.ApplyLandAreaValue for why the
            // unit is read live-first, why the row's stamp is only consulted while land area
            // is still included, and why the appraiser's rate wins over the computed one.
            method.ApplyLandAreaValue(landAreaFromTitles, command.LandValue);
        }

        // Handle building value (toggle + amount); IndicatedValue persists independently below.
        if (command.HasBuildingValue == true && command.BuildingValue.HasValue)
        {
            finalValue.SetBuildingValue(command.BuildingValue.Value);
        }
        else if (command.HasBuildingValue == false)
        {
            finalValue.ClearBuildingValue();
        }

        if (command.IndicatedValue.HasValue)
        {
            finalValue.SetIndicatedValue(command.IndicatedValue.Value);
        }

        // MethodValue = IndicatedValue ?? FinalValue — last, once both are set for this save — then
        // roll the new method value up through approach → analysis (null-safe, idempotent).
        method.SyncMethodValueWithIndicatedValue();
        pricingAnalysis.RecalculateRollup();

        return new SetFinalValueResult(
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
