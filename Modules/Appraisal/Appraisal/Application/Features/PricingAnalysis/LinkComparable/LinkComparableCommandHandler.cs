using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.LinkComparable;

public class LinkComparableCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<LinkComparableCommand, LinkComparableResult>
{
    public async Task<LinkComparableResult> Handle(
        LinkComparableCommand command,
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

        // Link the comparable
        var link = method.LinkComparable(
            command.MarketComparableId,
            command.DisplaySequence,
            command.Weight);

        // Also create a calculation for this comparable
        var calculation = method.AddCalculation(command.MarketComparableId);

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new LinkComparableResult(link.Id, calculation.Id);
    }
}
