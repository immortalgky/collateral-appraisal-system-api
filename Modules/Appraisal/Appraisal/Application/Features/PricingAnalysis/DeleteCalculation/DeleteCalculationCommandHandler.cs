using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteCalculation;

public class DeleteCalculationCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<DeleteCalculationCommand, DeleteCalculationResult>
{
    public async Task<DeleteCalculationResult> Handle(
        DeleteCalculationCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(command.PricingAnalysisId, cancellationToken);

        if (pricingAnalysis is null)
        {
            throw new InvalidOperationException($"Pricing analysis with ID {command.PricingAnalysisId} not found.");
        }

        // Find the method containing the calculation
        var method = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Calculations.Any(c => c.Id == command.CalculationId));

        if (method is null)
        {
            throw new InvalidOperationException($"Calculation with ID {command.CalculationId} not found.");
        }

        // Find the calculation
        var calculation = method.Calculations.FirstOrDefault(c => c.Id == command.CalculationId);

        if (calculation is null)
        {
            throw new InvalidOperationException($"Calculation with ID {command.CalculationId} not found.");
        }

        // Use reflection to access the private list and remove
        var calculationsField = typeof(Domain.Appraisals.PricingAnalysisMethod)
            .GetField("_calculations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var calculationsList = calculationsField?.GetValue(method) as List<PricingCalculation>;
        calculationsList?.Remove(calculation);

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new DeleteCalculationResult(true);
    }
}
