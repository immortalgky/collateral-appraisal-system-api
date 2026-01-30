using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.SetFinalValue;

/// <summary>
/// Handler for setting final value for a pricing method
/// </summary>
public class SetFinalValueCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
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
        {
            throw new InvalidOperationException(
                $"Pricing analysis with ID {command.PricingAnalysisId} not found");
        }

        // Find the method
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
        {
            throw new InvalidOperationException(
                $"Pricing method with ID {command.MethodId} not found in pricing analysis {command.PricingAnalysisId}");
        }

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

        return new SetFinalValueResult(
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
