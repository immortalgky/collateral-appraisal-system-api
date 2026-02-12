namespace Appraisal.Application.Features.Fees.GetAppraisalFees;

public record GetAppraisalFeesResult(List<AppraisalFeeDto> Fees);

public record AppraisalFeeDto(
    Guid Id,
    Guid AssignmentId,
    Guid AppraisalId,
    decimal TotalFeeBeforeVAT,
    decimal VATRate,
    decimal VATAmount,
    decimal TotalFeeAfterVAT,
    decimal BankAbsorbAmount,
    decimal CustomerPayableAmount,
    decimal TotalPaidAmount,
    decimal OutstandingAmount,
    string PaymentStatus,
    decimal? InspectionFeeAmount,
    DateTime? CreatedOn)
{
    public List<AppraisalFeeItemDto> Items { get; init; } = [];
}

public record AppraisalFeeItemDto(
    Guid Id,
    Guid AppraisalFeeId,
    string FeeCode,
    string FeeDescription,
    decimal FeeAmount,
    bool RequiresApproval,
    string? ApprovalStatus,
    Guid? ApprovedBy,
    DateTime? ApprovedAt,
    string? RejectionReason);
