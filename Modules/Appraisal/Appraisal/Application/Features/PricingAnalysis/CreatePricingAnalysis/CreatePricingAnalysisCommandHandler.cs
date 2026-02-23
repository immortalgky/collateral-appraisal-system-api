namespace Appraisal.Application.Features.PricingAnalysis.CreatePricingAnalysis;

/// <summary>
/// Handler for creating a new PricingAnalysis
/// </summary>
public class CreatePricingAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository
) : ICommandHandler<CreatePricingAnalysisCommand, CreatePricingAnalysisResult>
{
    public async Task<CreatePricingAnalysisResult> Handle(
        CreatePricingAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        // Check if pricing analysis already exists for this group
        var exists = await pricingAnalysisRepository.ExistsByPropertyGroupIdAsync(
            command.PropertyGroupId,
            cancellationToken);

        if (exists)
            throw new InvalidOperationException(
                $"Pricing analysis already exists for property group {command.PropertyGroupId}");

        var pricingAnalysis = Domain.Appraisals.PricingAnalysis.Create(command.PropertyGroupId);

        await pricingAnalysisRepository.AddAsync(pricingAnalysis, cancellationToken);

        return new CreatePricingAnalysisResult(pricingAnalysis.Id, pricingAnalysis.Status);
    }
}
