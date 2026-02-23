namespace Appraisal.Application.Features.PricingAnalysis.StartPricingAnalysis;

/// <summary>
/// Handler for starting pricing analysis
/// </summary>
public class StartPricingAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<StartPricingAnalysisCommand, StartPricingAnalysisResult>
{
    public async Task<StartPricingAnalysisResult> Handle(
        StartPricingAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdAsync(command.Id, cancellationToken)
                              ?? throw new InvalidOperationException($"Pricing analysis {command.Id} not found");

        pricingAnalysis.StartProgress();

        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new StartPricingAnalysisResult(pricingAnalysis.Id, pricingAnalysis.Status);
    }
}
