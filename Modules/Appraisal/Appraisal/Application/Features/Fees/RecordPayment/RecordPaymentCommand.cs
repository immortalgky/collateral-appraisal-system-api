using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Fees.RecordPayment;

public record RecordPaymentCommand(
    Guid AppraisalId,
    Guid FeeId,
    decimal PaymentAmount,
    DateTime PaymentDate,
    string? PaymentMethod = null,
    string? PaymentReference = null,
    string? Remarks = null
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
