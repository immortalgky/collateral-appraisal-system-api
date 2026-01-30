using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.LinkComparable;

public record LinkComparableCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    Guid MarketComparableId,
    int DisplaySequence,
    decimal? Weight = null
) : ICommand<LinkComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
