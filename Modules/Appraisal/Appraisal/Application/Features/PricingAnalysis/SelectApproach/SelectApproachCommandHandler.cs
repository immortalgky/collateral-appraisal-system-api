namespace Appraisal.Application.Features.PricingAnalysis.SelectApproach;

/// <summary>
/// Handler for selecting an approach as the final approach for the analysis.
/// Split out of SelectMethod so an approach can be chosen independently of picking
/// a method within it — see SelectMethodCommandHandler for the method-only half.
/// </summary>
public class SelectApproachCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<SelectApproachCommand, SelectApproachResult>
{
    public async Task<SelectApproachResult> Handle(
        SelectApproachCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        // Invariants (exactly one selected approach, must have a selected method) and the
        // FinalAppraisedValue propagation are now enforced inside the aggregate.
        pricingAnalysis.SelectApproach(command.ApproachId);

        var targetApproach = pricingAnalysis.Approaches.First(a => a.Id == command.ApproachId);

        return new SelectApproachResult(
            targetApproach.Id,
            targetApproach.ApproachType,
            pricingAnalysis.FinalAppraisedValue);
    }
}
