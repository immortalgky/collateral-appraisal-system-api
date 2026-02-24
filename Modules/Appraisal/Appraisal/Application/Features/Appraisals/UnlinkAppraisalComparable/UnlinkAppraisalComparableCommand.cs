using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.UnlinkAppraisalComparable;

public record UnlinkAppraisalComparableCommand(
    Guid AppraisalId,
    Guid AppraisalComparableId
) : ICommand<UnlinkAppraisalComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
