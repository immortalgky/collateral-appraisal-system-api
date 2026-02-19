namespace Appraisal.Application.Features.Fees.GetAppraisalFees;

public record GetAppraisalFeesResult(List<AppraisalFeeDto> Fees);

public record AppraisalFeeDto
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid AppraisalId { get; set; }
    public string FeePaymentType { get; set; } = default!;
    public string FeeNotes { get; set; } = default!;
    public decimal TotalFeeBeforeVAT { get; set; }
    public decimal VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalFeeAfterVAT { get; set; }
    public decimal BankAbsorbAmount { get; set; }
    public decimal CustomerPayableAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string PaymentStatus { get; set; } = default!;
    public decimal? InspectionFeeAmount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<AppraisalFeeItemDto> Items { get; set; } = [];
    public List<PaymentHistoryDto> PaymentHistory { get; set; } = [];
}

public record AppraisalFeeItemDto
{
    public Guid Id { get; set; }
    public Guid AppraisalFeeId { get; set; }
    public string FeeCode { get; set; } = default!;
    public string FeeDescription { get; set; } = default!;
    public decimal FeeAmount { get; set; }
    public bool RequiresApproval { get; set; }
    public string? ApprovalStatus { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public record PaymentHistoryDto
{
    public Guid Id { get; set; }
    public Guid AppraisalFeeId { get; set; }
    public decimal PaymentAmount { get; set; }
    public DateTime PaymentDate { get; set; }
}