using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UpdateComparableLink;

public record UpdateComparableLinkCommand(
    Guid PricingAnalysisId,
    Guid LinkId,
    decimal? Weight,
    int? DisplaySequence
) : ICommand<UpdateComparableLinkResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
