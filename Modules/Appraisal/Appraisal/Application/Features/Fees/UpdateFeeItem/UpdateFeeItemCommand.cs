namespace Appraisal.Application.Features.Fees.UpdateFeeItem;

public record UpdateFeeItemCommand(
    Guid AppraisalId,
    Guid FeeId,
    Guid FeeItemId,
    string FeeCode,
    string FeeDescription,
    decimal FeeAmount
) : ICommand<UpdateFeeItemResult>, ITransactionalCommand<IAppraisalUnitOfWork>;