using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Fees.AddFeeItem;

public record AddFeeItemCommand(
    Guid AppraisalId,
    Guid FeeId,
    string FeeCode,
    string FeeDescription,
    decimal FeeAmount
) : ICommand<AddFeeItemResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
