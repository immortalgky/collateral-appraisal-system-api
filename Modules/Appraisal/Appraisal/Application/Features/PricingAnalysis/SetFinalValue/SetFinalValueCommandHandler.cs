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
