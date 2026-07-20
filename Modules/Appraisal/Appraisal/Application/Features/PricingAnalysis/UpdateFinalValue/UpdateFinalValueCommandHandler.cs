using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Services;
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

        // Update final value
        finalValue.UpdateFinalValue(command.FinalValue, command.FinalValueRounded);
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

        // Handle building value (toggle + amount)
        if (command.HasBuildingValue == true && command.BuildingValue.HasValue)
        {
            finalValue.SetBuildingValue(command.BuildingValue.Value);
        }
        else if (command.HasBuildingValue == false)
        {
            finalValue.ClearBuildingValue();
        }

        // Appraisal price (now persisted independently of the building-cost toggle)
        if (command.AppraisalPrice.HasValue)
        {
            finalValue.SetAppraisalPrice(command.AppraisalPrice.Value);
        }

        return new UpdateFinalValueResult(
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
