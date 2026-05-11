namespace Appraisal.Application.Features.Invoices.GetEligibleAssignments;

public record EligibleAssignmentDto(
    Guid AssignmentId,
    Guid AppraisalFeeId,
    string? AppraisalNumber,
    string? CustomerName,
    string? ProductType,
    decimal FeeBeforeVAT,
    decimal VATRate,
    decimal VATAmount,
    decimal TotalFeeAfterVAT,
    decimal BankAbsorbAmount,
    DateTime? ReceivedDate
);
