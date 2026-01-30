using Appraisal.Domain.Appraisals;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateMethod;

/// <summary>
/// Handler for updating a method
/// </summary>
public class UpdateMethodCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<UpdateMethodCommand, UpdateMethodResult>
{
    public async Task<UpdateMethodResult> Handle(
        UpdateMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis == null)
            throw new InvalidOperationException($"Pricing analysis with ID '{command.PricingAnalysisId}' not found");

        PricingAnalysisMethod? method = null;

        foreach (var approach in pricingAnalysis.Approaches)
        {
            method = approach.Methods.FirstOrDefault(m => m.Id == command.MethodId);
            if (method != null)
                break;
        }

        if (method == null)
            throw new InvalidOperationException($"Method with ID '{command.MethodId}' not found");

        // Only call SetValue if at least one parameter is provided
        if (command.MethodValue.HasValue || command.ValuePerUnit.HasValue || command.UnitType != null)
        {
            method.SetValue(
                command.MethodValue ?? method.MethodValue ?? 0,
                command.ValuePerUnit ?? method.ValuePerUnit,
                command.UnitType ?? method.UnitType);
        }

        return new UpdateMethodResult(
            method.Id,
            method.MethodType,
            method.MethodValue,
            method.ValuePerUnit,
            method.UnitType);
    }
}
