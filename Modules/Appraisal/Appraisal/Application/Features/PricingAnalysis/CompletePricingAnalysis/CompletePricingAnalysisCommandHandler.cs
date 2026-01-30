namespace Appraisal.Application.Features.PricingAnalysis.CompletePricingAnalysis;

/// <summary>
/// Handler for completing pricing analysis
/// </summary>
public class CompletePricingAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<CompletePricingAnalysisCommand, CompletePricingAnalysisResult>
{
    public async Task<CompletePricingAnalysisResult> Handle(
        CompletePricingAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdAsync(command.Id, cancellationToken)
                              ?? throw new InvalidOperationException($"Pricing analysis {command.Id} not found");

        pricingAnalysis.Complete(command.AppraisedValue);

        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new CompletePricingAnalysisResult(
            pricingAnalysis.Id,
            pricingAnalysis.Status);
    }
}