using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UnlinkComparable;

public record UnlinkComparableCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    Guid LinkId
) : ICommand<UnlinkComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
