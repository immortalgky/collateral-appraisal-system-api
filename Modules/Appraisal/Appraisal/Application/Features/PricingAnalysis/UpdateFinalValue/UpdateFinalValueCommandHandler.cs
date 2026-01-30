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
        {
            throw new InvalidOperationException(
                $"Pricing analysis with ID {command.PricingAnalysisId} not found");
        }

        // Find the method with matching final value ID
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.FinalValue?.Id == command.FinalValueId);

        if (method?.FinalValue is null)
        {
            throw new InvalidOperationException(
                $"Final value with ID {command.FinalValueId} not found in pricing analysis {command.PricingAnalysisId}");
        }

        var finalValue = method.FinalValue;

        // Update final value
        finalValue.UpdateFinalValue(command.FinalValue, command.FinalValueRounded);

        // Handle land area
        if (command.IncludeLandArea == true && command.LandArea.HasValue &&
            command.AppraisalPrice.HasValue && command.AppraisalPriceRounded.HasValue)
        {
            finalValue.SetLandAreaValues(
                command.LandArea.Value,
                command.AppraisalPrice.Value,
                command.AppraisalPriceRounded.Value);
        }
        else if (command.IncludeLandArea == false)
        {
            finalValue.ExcludeLandArea();
        }

        // Handle building cost
        if (command.HasBuildingCost == true && command.BuildingCost.HasValue &&
            command.AppraisalPriceWithBuilding.HasValue && command.AppraisalPriceWithBuildingRounded.HasValue)
        {
            finalValue.SetBuildingCost(
                command.BuildingCost.Value,
                command.AppraisalPriceWithBuilding.Value,
                command.AppraisalPriceWithBuildingRounded.Value);
        }

        return new UpdateFinalValueResult(
            finalValue.Id,
            finalValue.FinalValue,
            finalValue.FinalValueRounded,
            finalValue.IncludeLandArea,
            finalValue.LandArea,
            finalValue.AppraisalPrice,
            finalValue.AppraisalPriceRounded,
            finalValue.HasBuildingCost,
            finalValue.BuildingCost,
            finalValue.AppraisalPriceWithBuilding,
            finalValue.AppraisalPriceWithBuildingRounded
        );
    }
}
