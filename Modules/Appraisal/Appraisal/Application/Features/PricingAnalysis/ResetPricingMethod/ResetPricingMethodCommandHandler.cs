using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.ResetPricingMethod;

public class ResetPricingMethodCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<ResetPricingMethodCommand, ResetPricingMethodResult>
{
    public async Task<ResetPricingMethodResult> Handle(
        ResetPricingMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId, cancellationToken);

        if (pricingAnalysis is null)
            throw new InvalidOperationException(
                $"Pricing analysis with ID {command.PricingAnalysisId} not found.");

        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
            throw new InvalidOperationException(
                $"Pricing method with ID {command.MethodId} not found.");

        method.Reset();

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new ResetPricingMethodResult(command.MethodId, true);
    }
}
