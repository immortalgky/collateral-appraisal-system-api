namespace Appraisal.Application.Features.Fees.UpdateFee;

public record UpdateFeeCommand(Guid FeeId, string FeePaymentType, decimal BankAbsorbAmount)
    : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;