namespace Appraisal.Application.Features.Fees.RemoveFeeItem;

public record RemoveFeeItemCommand(Guid FeeId, Guid FeeItemId) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;