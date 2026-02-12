using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Fees.ApproveFeeItem;

public record ApproveFeeItemCommand(
    Guid AppraisalId,
    Guid FeeId,
    Guid ItemId,
    Guid ApprovedBy
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
