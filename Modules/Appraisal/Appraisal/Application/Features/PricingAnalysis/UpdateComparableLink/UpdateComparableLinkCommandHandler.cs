namespace Appraisal.Application.Features.PricingAnalysis.UpdateComparableLink;

public class UpdateComparableLinkCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<UpdateComparableLinkCommand, UpdateComparableLinkResult>
{
    public async Task<UpdateComparableLinkResult> Handle(
        UpdateComparableLinkCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await repository.GetByIdWithAllDataAsync(command.PricingAnalysisId, cancellationToken);

        if (pricingAnalysis is null)
        {
            throw new InvalidOperationException($"Pricing analysis with ID {command.PricingAnalysisId} not found.");
        }

        // Find the link across all methods
        var link = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .SelectMany(m => m.ComparableLinks)
            .FirstOrDefault(l => l.Id == command.LinkId);

        if (link is null)
        {
            throw new InvalidOperationException($"Comparable link with ID {command.LinkId} not found.");
        }
        
        // Update display sequence if provided
        if (command.DisplaySequence.HasValue)
        {
            link.SetDisplaySequence(command.DisplaySequence.Value);
        }

        await repository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new UpdateComparableLinkResult(link.Id, link.DisplaySequence);
    }
}
