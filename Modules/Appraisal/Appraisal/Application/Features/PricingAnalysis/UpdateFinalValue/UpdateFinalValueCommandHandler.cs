using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateFinalValue;

/// <summary>
/// Handler for updating final value
/// </summary>
public class UpdateFinalValueCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
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
        method.SetValue(command.FinalValueRounded);

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

        // Handle land area
        if (command.IncludeLandArea == true && command.LandArea.HasValue && command.LandValue.HasValue)
        {
            finalValue.SetLandAreaValues(command.LandArea.Value, command.LandValue.Value);
        }
        else if (command.IncludeLandArea == false)
        {
            finalValue.ExcludeLandArea();
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
