namespace Appraisal.Application.Features.PricingAnalysis.SelectMethod;

/// <summary>
/// Handler for selecting a method as primary
/// </summary>
public class SelectMethodCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<SelectMethodCommand, SelectMethodResult>
{
    public async Task<SelectMethodResult> Handle(
        SelectMethodCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdWithAllDataAsync(
            command.PricingAnalysisId,
            cancellationToken);

        if (pricingAnalysis is null)
            throw new NotFoundException("PricingAnalysis", command.PricingAnalysisId);

        // Selection invariants (exactly one selected method per approach) and the
        // FinalAppraisedValue propagation (when this approach is already the analysis's
        // selected/final approach) are now enforced inside the aggregate.
        pricingAnalysis.SelectMethod(command.MethodId);

        var targetMethod = pricingAnalysis.Approaches
            .SelectMany(a => a.Methods)
            .First(m => m.Id == command.MethodId);

        return new SelectMethodResult(
            targetMethod.Id,
            targetMethod.MethodType,
            pricingAnalysis.FinalAppraisedValue);
    }
}
