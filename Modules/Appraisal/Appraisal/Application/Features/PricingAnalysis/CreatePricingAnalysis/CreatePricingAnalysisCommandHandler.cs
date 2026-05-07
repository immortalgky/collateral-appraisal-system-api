using Appraisal.Application.Services;

namespace Appraisal.Application.Features.PricingAnalysis.CreatePricingAnalysis;

/// <summary>
/// Handler for creating a new PricingAnalysis.
/// Enforces the four readiness preconditions (market survey, building detail,
/// rental info, property status = Saved) before the analysis can be created.
/// </summary>
public class CreatePricingAnalysisCommandHandler(
    IPricingAnalysisRepository pricingAnalysisRepository,
    IPricingAnalysisReadinessService readinessService
) : ICommandHandler<CreatePricingAnalysisCommand, CreatePricingAnalysisResult>
{
    public async Task<CreatePricingAnalysisResult> Handle(
        CreatePricingAnalysisCommand command,
        CancellationToken cancellationToken)
    {
        // Idempotency check
        var exists = await pricingAnalysisRepository.ExistsByPropertyGroupIdAsync(
            command.PropertyGroupId,
            cancellationToken);

        if (exists)
            throw new ConflictException(
                "PricingAnalysis", command.PropertyGroupId);

        // Readiness gate — same rules used by the GET projection so the React
        // client and the API always agree on whether the AP button can fire.
        var readiness = await readinessService.EvaluateByGroupIdAsync(
            command.PropertyGroupId, cancellationToken);

        if (readiness is null)
            throw new NotFoundException(
                $"Property group {command.PropertyGroupId} was not found");

        if (!readiness.IsReady)
            throw new PricingAnalysisNotReadyException(readiness.Violations);

        var pricingAnalysis = Domain.Appraisals.PricingAnalysis.CreateForPropertyGroup(command.PropertyGroupId);
        await pricingAnalysisRepository.AddAsync(pricingAnalysis, cancellationToken);

        return new CreatePricingAnalysisResult(pricingAnalysis.Id, pricingAnalysis.Status);
    }
}
