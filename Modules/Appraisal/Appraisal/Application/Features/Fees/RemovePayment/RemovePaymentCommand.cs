namespace Appraisal.Application.Features.Fees.RemovePayment;

public record RemovePaymentCommand(Guid FeeId, Guid PaymentId) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;