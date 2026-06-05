using Appraisal.Domain.Appraisals;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.PricingAnalysis.DeletePricingAnalysisReference;

public class DeletePricingAnalysisReferenceCommandHandler(
    IPricingAnalysisRepository repository
) : ICommandHandler<DeletePricingAnalysisReferenceCommand, DeletePricingAnalysisReferenceResult>
{
    public async Task<DeletePricingAnalysisReferenceResult> Handle(
        DeletePricingAnalysisReferenceCommand command,
        CancellationToken cancellationToken)
    {
        var pa = await repository.GetByIdAsync(command.PricingAnalysisId, cancellationToken);

        // Idempotent: already gone.
        if (pa is null)
            return new DeletePricingAnalysisReferenceResult(true);

        // Guard: only reference analyses may be deleted here. PropertyGroup / ProjectModel
        // valuation analyses are owned by their anchors and cleaned up via those paths.
        if (pa.SubjectType is PricingAnalysisSubjectType.PropertyGroup
            or PricingAnalysisSubjectType.ProjectModel)
        {
            throw new BadRequestException(
                "Only market-reference analyses can be deleted via this endpoint.");
        }

        // Remove the reference PA — children (approaches/methods/comparables/scores/…) cascade at the DB.
        await repository.DeleteAsync(pa, cancellationToken);

        return new DeletePricingAnalysisReferenceResult(true);
    }
}
