namespace Appraisal.Application.Features.PricingAnalysis.UpdatePricingAnalysis;

/// <summary>
/// Handler for updating pricing analysis
/// </summary>
public class UpdatePricingAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<UpdatePricingAnalysisCommand, UpdatePricingAnalysisResult>
{
    public async Task<UpdatePricingAnalysisResult> Handle(
        UpdatePricingAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        var pricingAnalysis = await pricingAnalysisRepository.GetByIdAsync(command.Id, cancellationToken)
                              ?? throw new InvalidOperationException($"Pricing analysis {command.Id} not found");

        pricingAnalysis.SetFinalValues(command.AppraisedValue);

        if (command.UseSystemCalc.HasValue)
            pricingAnalysis.SetUseSystemCalc(command.UseSystemCalc.Value);

        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new UpdatePricingAnalysisResult(pricingAnalysis.Id);
    }
}