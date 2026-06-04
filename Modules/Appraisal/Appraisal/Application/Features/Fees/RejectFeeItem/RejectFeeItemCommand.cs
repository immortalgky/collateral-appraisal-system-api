using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Fees.RejectFeeItem;

public record RejectFeeItemCommand(
    Guid AppraisalId,
    Guid FeeId,
    Guid ItemId,
    string RejectedBy,
    string Reason
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
