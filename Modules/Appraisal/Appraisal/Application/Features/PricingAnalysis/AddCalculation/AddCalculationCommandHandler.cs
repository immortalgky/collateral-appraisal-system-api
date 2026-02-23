using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.AddCalculation;

public class AddCalculationCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<AddCalculationCommand, AddCalculationResult>
{
    public async Task<AddCalculationResult> Handle(
        AddCalculationCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(command.PricingAnalysisId, cancellationToken);

        if (pricingAnalysis is null)
        {
            throw new InvalidOperationException($"Pricing analysis with ID {command.PricingAnalysisId} not found.");
        }

        // Find the method
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.MethodId);

        if (method is null)
        {
            throw new InvalidOperationException($"Pricing method with ID {command.MethodId} not found.");
        }

        // Add the calculation
        var calculation = method.AddCalculation(command.MarketComparableId);

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new AddCalculationResult(calculation.Id);
    }
}
