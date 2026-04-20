namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Individual fee line item within an AppraisalFee.
/// Supports approval workflow for additional fees.
/// </summary>
public class AppraisalFeeItem : Entity<Guid>
{
    public Guid AppraisalFeeId { get; private set; }

    // Fee Details
    public string FeeCode { get; private set; } = null!; // 01=AppraisalFee, 02=Travel, 03=Urgent
    public string FeeDescription { get; private set; } = null!;
    public decimal FeeAmount { get; private set; }

    // Approval (for additional fees)
    public bool RequiresApproval { get; private set; }
    public string? ApprovalStatus { get; private set; } // Pending, Approved, Rejected
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private AppraisalFeeItem()
    {
        // For EF Core
    }

    public static AppraisalFeeItem Create(
        Guid appraisalFeeId,
        string feeCode,
        string feeDescription,
        decimal feeAmount,
        bool requiresApproval = false)
    {
        return new AppraisalFeeItem
        {
            //Id = Guid.CreateVersion7(),
            AppraisalFeeId = appraisalFeeId,
            FeeCode = feeCode,
            FeeDescription = feeDescription,
            FeeAmount = feeAmount,
            RequiresApproval = requiresApproval,
            ApprovalStatus = requiresApproval ? "Pending" : null
        };
    }

    public void Update(string feeCode, string feeDescription, decimal feeAmount)
    {
        FeeCode = feeCode;
        FeeDescription = feeDescription;
        FeeAmount = feeAmount;
    }

    public void Approve(Guid approvedBy)
    {
        if (ApprovalStatus != "Pending")
            throw new InvalidOperationException($"Cannot approve fee item in status '{ApprovalStatus}'");

        ApprovalStatus = "Approved";
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.Now;
    }

    public void Reject(Guid rejectedBy, string reason)
    {
        if (ApprovalStatus != "Pending")
            throw new InvalidOperationException($"Cannot reject fee item in status '{ApprovalStatus}'");

        ApprovalStatus = "Rejected";
        ApprovedBy = rejectedBy;
        ApprovedAt = DateTime.Now;
        RejectionReason = reason;
    }
}