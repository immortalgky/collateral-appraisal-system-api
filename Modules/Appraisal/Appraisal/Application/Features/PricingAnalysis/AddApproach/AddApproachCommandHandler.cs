namespace Appraisal.Application.Features.PricingAnalysis.AddApproach;

/// <summary>
/// Handler for adding a new approach to a pricing analysis
/// </summary>
public class AddApproachCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<AddApproachCommand, AddApproachResult>
{
    public async Task<AddApproachResult> Handle(
        AddApproachCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis == null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        var approach = pricingAnalysis.AddApproach(command.ApproachType, command.Weight);

        return new AddApproachResult(approach.Id, approach.ApproachType);
    }
}