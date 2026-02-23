using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UnlinkComparable;

public class UnlinkComparableCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<UnlinkComparableCommand, UnlinkComparableResult>
{
    public async Task<UnlinkComparableResult> Handle(
        UnlinkComparableCommand command,
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

        // Find the link to get the market comparable ID before removal
        var link = method.ComparableLinks.FirstOrDefault(l => l.Id == command.LinkId);
        if (link is null)
        {
            throw new InvalidOperationException($"Comparable link with ID {command.LinkId} not found.");
        }

        var marketComparableId = link.MarketComparableId;

        // Remove the link using domain method
        method.RemoveComparableLink(command.LinkId);

        // Also remove associated calculation using domain method
        method.RemoveCalculationByComparableId(marketComparableId);

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new UnlinkComparableResult(true);
    }
}
