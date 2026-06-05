using Appraisal.Application.Features.PricingAnalysis.CreateOrGetReference;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.PricingAnalysis.CreateReferenceFromMethod;

/// <summary>
/// Clones an existing Cost-approach method (WQS / SaleGrid / DirectComparison) into a
/// new reference PricingAnalysis (IncomeLandRef), optionally overriding the land area.
/// Idempotent: if a reference already exists for (SubjectType, AnchorId, null), returns it.
/// </summary>
public class CreateReferenceFromMethodCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<CreateReferenceFromMethodCommand, CreateOrGetReferenceResult>
{
    private static readonly HashSet<string> AllowedSourceMethodTypes =
        new(StringComparer.OrdinalIgnoreCase) { "WQS", "SaleGrid", "DirectComparison" };

    public async Task<CreateOrGetReferenceResult> Handle(
        CreateReferenceFromMethodCommand command,
        CancellationToken cancellationToken)
    {
        // Guard: this endpoint is land-copy-specific
        if (command.SubjectType != PricingAnalysisSubjectType.IncomeLandRef)
            throw new BadRequestException(
                $"CreateReferenceFromMethod only supports SubjectType=IncomeLandRef; got {command.SubjectType}.");

        // Idempotent: return existing reference without re-cloning
        var existing = await repository.FindReferenceAsync(
            command.SubjectType,
            command.AnchorId,
            anchorRefKey: null,
            cancellationToken);

        if (existing is not null)
        {
            // Defensive: ensure a Market approach exists rather than leaking Guid.Empty
            // (mirrors CreateOrGetReferenceCommandHandler).
            var existingMarket = existing.Approaches.FirstOrDefault(a => a.ApproachType == "Market")
                                 ?? existing.AddApproach("Market");
            return new CreateOrGetReferenceResult(
                existing.Id,
                existingMarket.Id,
                WasCreated: false);
        }

        // Load the source PA (must include all data to deep-clone)
        var sourcePa = await repository.GetByIdWithAllDataAsync(
            command.SourcePricingAnalysisId, cancellationToken)
            ?? throw new NotFoundException(
                $"Source PricingAnalysis {command.SourcePricingAnalysisId} not found.");

        // Find the source method by id across all its approaches
        var sourceMethod = sourcePa.Approaches
            .SelectMany(a => a.Methods)
            .FirstOrDefault(m => m.Id == command.SourceMethodId)
            ?? throw new NotFoundException(
                $"Source method {command.SourceMethodId} not found in PricingAnalysis {command.SourcePricingAnalysisId}.");

        // Guard: source must be a market-comparison method type
        if (!AllowedSourceMethodTypes.Contains(sourceMethod.MethodType))
            throw new BadRequestException(
                $"Source method type '{sourceMethod.MethodType}' is not allowed for cloning. " +
                $"Expected one of: {string.Join(", ", AllowedSourceMethodTypes)}.");

        // Create the reference PA with the cloned method
        var pa = Domain.Appraisals.PricingAnalysis.CreateReferenceFromMethod(
            command.SubjectType,
            command.AnchorId,
            command.HostMethodId,
            sourceMethod,
            command.LandAreaOverride);

        await repository.AddAsync(pa, cancellationToken);

        var marketApproach = pa.Approaches.First(a => a.ApproachType == "Market");

        return new CreateOrGetReferenceResult(pa.Id, marketApproach.Id, WasCreated: true);
    }
}
