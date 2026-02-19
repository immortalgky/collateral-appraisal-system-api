namespace Appraisal.Application.Features.Fees.UpdatePayment;

public record UpdatePaymentResult(
    Guid PaymentId,
    decimal PaymentAmount,
    DateTime PaymentDate
);