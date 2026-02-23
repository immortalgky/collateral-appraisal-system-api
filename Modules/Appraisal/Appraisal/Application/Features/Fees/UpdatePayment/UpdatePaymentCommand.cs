namespace Appraisal.Application.Features.Fees.UpdatePayment;

public record UpdatePaymentCommand(Guid FeeId, Guid PaymentId, decimal PaymentAmount, DateTime PaymentDate)
    : ICommand<UpdatePaymentResult>, ITransactionalCommand<IAppraisalUnitOfWork>;