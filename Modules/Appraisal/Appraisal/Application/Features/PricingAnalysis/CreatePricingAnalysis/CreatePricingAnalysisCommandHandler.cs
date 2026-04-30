using Shared.Exceptions;

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
        // Pre-check: throws ConflictException (409) if already exists.
        // The filtered unique index on PropertyGroupId is the DB-level race guard for concurrent requests.
        var exists = await pricingAnalysisRepository.ExistsByPropertyGroupIdAsync(
            command.PropertyGroupId,
            cancellationToken);

        if (exists)
            throw new ConflictException(
                "PricingAnalysis", command.PropertyGroupId);

        var pricingAnalysis = Domain.Appraisals.PricingAnalysis.CreateForPropertyGroup(command.PropertyGroupId);

        await pricingAnalysisRepository.AddAsync(pricingAnalysis, cancellationToken);

        return new CreatePricingAnalysisResult(pricingAnalysis.Id, pricingAnalysis.Status);
    }
}
