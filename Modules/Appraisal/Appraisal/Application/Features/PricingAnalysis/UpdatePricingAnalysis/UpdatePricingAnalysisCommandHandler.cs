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
                              ?? throw new NotFoundException("PricingAnalysis", command.Id);

        // Only write what the caller actually sent. The calculation-mode toggle on the summary
        // screen posts UseSystemCalc alone; writing an absent AppraisedValue would push the
        // deserialised 0 over the value the selection rollup just computed.
        if (command.AppraisedValue.HasValue)
            pricingAnalysis.SetFinalValues(command.AppraisedValue.Value);

        if (command.UseSystemCalc.HasValue)
            pricingAnalysis.SetUseSystemCalc(command.UseSystemCalc.Value);

        await pricingAnalysisRepository.UpdateAsync(pricingAnalysis, cancellationToken);

        return new UpdatePricingAnalysisResult(pricingAnalysis.Id);
    }
}
