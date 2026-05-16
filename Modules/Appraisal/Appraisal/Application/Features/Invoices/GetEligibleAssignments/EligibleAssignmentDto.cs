namespace Appraisal.Application.Features.Invoices.GetEligibleAssignments;

public record EligibleAssignmentDto(
    Guid AssignmentId,
    Guid AppraisalFeeId,
    string? AppraisalNumber,
    string? CustomerName,
    string? ProductType,
    string? FeePaymentType,
    decimal FeeBeforeVAT,
    decimal VATRate,
    decimal VATAmount,
    decimal TotalFeeAfterVAT,
    decimal BankAbsorbAmount,
    decimal PayPartialAmount,
    decimal RemainingFee,
    DateTime? SubmittedDate,
    DateTime? LastPaymentDate
);
