using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;

/// <summary>
/// Idempotent handler: returns the existing reference PricingAnalysis if one already exists
/// for the (SubjectType, AnchorId, AnchorRefKey) triple; otherwise creates a new one with a
/// "Market" approach pre-added.
/// </summary>
public class CreateOrGetReferenceCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<CreateOrGetReferenceCommand, CreateOrGetReferenceResult>
{
    public async Task<CreateOrGetReferenceResult> Handle(
        CreateOrGetReferenceCommand command,
        CancellationToken cancellationToken)
    {
        // Reference types only — guard against misuse
        if (command.SubjectType is PricingAnalysisSubjectType.PropertyGroup
            or PricingAnalysisSubjectType.ProjectModel)
            throw new InvalidOperationException(
                $"SubjectType {command.SubjectType} is not a reference type. " +
                "Use CreatePricingAnalysis or CreateProjectModelPricingAnalysis for these types.");

        // Idempotent: find existing
        var existing = await repository.FindReferenceAsync(
            command.SubjectType,
            command.AnchorId,
            command.AnchorRefKey,
            cancellationToken);

        if (existing is not null)
        {
            // Return the first Market approach (or create one if missing — defensive)
            var marketApproach = existing.Approaches.FirstOrDefault(a => a.ApproachType == "Market");
            if (marketApproach is null)
            {
                marketApproach = existing.AddApproach("Market");
                await repository.UpdateAsync(existing, cancellationToken);
            }

            return new CreateOrGetReferenceResult(existing.Id, marketApproach.Id, WasCreated: false);
        }

        // Create new reference PA
        var pa = Domain.Appraisals.PricingAnalysis.CreateForReference(
            command.SubjectType,
            command.AnchorId,
            command.AnchorRefKey,
            command.HostMethodId);

        var approach = pa.AddApproach("Market");

        await repository.AddAsync(pa, cancellationToken);

        return new CreateOrGetReferenceResult(pa.Id, approach.Id, WasCreated: true);
    }
}
