using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.RemoveMethod;

public class RemoveMethodCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<RemoveMethodCommand, RemoveMethodResult>
{
    public async Task<RemoveMethodResult> Handle(
        RemoveMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId, cancellationToken);

        if (pricingAnalysis is null)
            throw new InvalidOperationException(
                $"Pricing analysis with ID {command.PricingAnalysisId} not found.");

        var approach = pricingAnalysis.Approaches
            .FirstOrDefault(a => a.Id == command.ApproachId);

        if (approach is null)
            throw new InvalidOperationException(
                $"Approach with ID {command.ApproachId} not found.");

        var method = approach.Methods
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
            throw new InvalidOperationException(
                $"Pricing method with ID {command.MethodId} not found.");

        // If the removed method was selected, clear approach and final value selections
        if (method.IsSelected)
        {
            approach.Unselect();
            pricingAnalysis.ClearFinalValues();
        }

        approach.RemoveMethod(command.MethodId);

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new RemoveMethodResult(command.MethodId, true);
    }
}
