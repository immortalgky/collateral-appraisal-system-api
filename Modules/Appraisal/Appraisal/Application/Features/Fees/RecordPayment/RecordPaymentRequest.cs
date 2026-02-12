namespace Appraisal.Application.Features.Fees.RecordPayment;

public record RecordPaymentRequest(
    decimal PaymentAmount,
    DateTime PaymentDate,
    string? PaymentMethod = null,
    string? PaymentReference = null,
    string? Remarks = null);
