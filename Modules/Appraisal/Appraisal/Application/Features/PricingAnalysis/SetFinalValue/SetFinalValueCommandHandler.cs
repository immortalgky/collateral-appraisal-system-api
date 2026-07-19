using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
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
                command.FinalValue,
                command.FinalValueRounded);

            method.SetFinalValue(finalValue);
        }
        else
        {
            // Update existing final value
            finalValue = method.FinalValue;
            finalValue.UpdateFinalValue(command.FinalValue, command.FinalValueRounded);
        }

        // Preserve the existing price unit — this manual override adjusts the value, not the unit.
        method.SetValue(command.FinalValueRounded, method.ValuePerUnit, method.UnitType);

        // TODO: Temporary — propagate method value upward for manual frontend updates
        if (method.IsSelected && method.MethodValue.HasValue)
        {
            var parentApproach = pricingAnalysis.Approaches
                .FirstOrDefault(a => a.Id == method.ApproachId)
                ?? throw new InvalidOperationException(
                    $"Approach {method.ApproachId} not found in pricing analysis {command.PricingAnalysisId}");

            parentApproach.SetValue(method.MethodValue.Value);

            if (parentApproach.IsSelected)
            {
                pricingAnalysis.SetFinalValues(parentApproach.ApproachValue!.Value);
            }
        }

        // TODO: Temporary — mark as manual calc since user is overriding values from frontend
        pricingAnalysis.SetUseSystemCalc(false);

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
        else if (PricingUnit.IsPerUnitRate(method.UnitType) && landAreaFromTitles > 0m)
        {
            var rate = method.ValuePerUnit ?? finalValue.FinalValueAdjusted;
            var landValue = command.LandValue
                ?? (rate.HasValue ? landAreaFromTitles * rate.Value : (decimal?)null);

            if (landValue.HasValue)
                finalValue.SetLandAreaValues(landAreaFromTitles, landValue.Value);
        }

        // Handle building value (toggle + amount); AppraisalPrice persists independently below.
        if (command.HasBuildingValue == true && command.BuildingValue.HasValue)
        {
            finalValue.SetBuildingValue(command.BuildingValue.Value);
        }
        else if (command.HasBuildingValue == false)
        {
            finalValue.ClearBuildingValue();
        }

        if (command.AppraisalPrice.HasValue)
        {
            finalValue.SetAppraisalPrice(command.AppraisalPrice.Value);
        }

        return new SetFinalValueResult(
            finalValue.Id,
            finalValue.FinalValue,
            finalValue.FinalValueRounded,
            finalValue.IncludeLandArea,
            finalValue.LandArea,
            finalValue.LandValue,
            finalValue.HasBuildingValue,
            finalValue.BuildingValue,
            finalValue.AppraisalPrice
        );
    }
}
