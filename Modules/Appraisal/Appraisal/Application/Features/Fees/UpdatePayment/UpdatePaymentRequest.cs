namespace Appraisal.Application.Features.Fees.UpdatePayment;

public record UpdatePaymentRequest(
    decimal PaymentAmount,
    DateTime PaymentDate
);