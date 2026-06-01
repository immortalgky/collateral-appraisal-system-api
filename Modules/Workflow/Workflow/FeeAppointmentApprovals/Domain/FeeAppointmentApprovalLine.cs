namespace Workflow.FeeAppointmentApprovals.Domain;

/// <summary>
/// A single component within a FeeAppointmentApproval bundle.
/// LineType = Appointment carries the NewDate snapshot; LineType = Fee carries FeeCode/Description/Amount.
/// </summary>
public class FeeAppointmentApprovalLine
{
    public Guid Id { get; private set; }
    public FeeApprovalLineType LineType { get; private set; }
    public Guid TargetId { get; private set; }

    // Appointment snapshot
    public DateTime? NewDate { get; private set; }
    public int? RescheduleCount { get; private set; }

    // Fee snapshot
    public string? FeeCode { get; private set; }
    public string? FeeDescription { get; private set; }
    public decimal? FeeAmount { get; private set; }

    public FeeApprovalLineStatus LineStatus { get; private set; }
    public string? DecisionReason { get; private set; }

    // Public for EF JSON deserialization
    public FeeAppointmentApprovalLine() { }

    internal static FeeAppointmentApprovalLine CreateAppointment(
        Guid targetId, DateTime newDate, int rescheduleCount)
    {
        return new FeeAppointmentApprovalLine
        {
            Id = Guid.CreateVersion7(),
            LineType = FeeApprovalLineType.Appointment,
            TargetId = targetId,
            NewDate = newDate,
            RescheduleCount = rescheduleCount,
            LineStatus = FeeApprovalLineStatus.Pending
        };
    }

    internal static FeeAppointmentApprovalLine CreateFee(
        Guid targetId, string feeCode, string feeDescription, decimal feeAmount)
    {
        return new FeeAppointmentApprovalLine
        {
            Id = Guid.CreateVersion7(),
            LineType = FeeApprovalLineType.Fee,
            TargetId = targetId,
            FeeCode = feeCode,
            FeeDescription = feeDescription,
            FeeAmount = feeAmount,
            LineStatus = FeeApprovalLineStatus.Pending
        };
    }

    internal void MarkApproved()
    {
        LineStatus = FeeApprovalLineStatus.Approved;
    }

    internal void MarkRejected(string? reason)
    {
        LineStatus = FeeApprovalLineStatus.Rejected;
        DecisionReason = reason;
    }

    internal void MarkCancelled(string reason)
    {
        LineStatus = FeeApprovalLineStatus.Cancelled;
        DecisionReason = reason;
    }
}

public enum FeeApprovalLineType
{
    Appointment = 0,
    Fee = 1
}

public enum FeeApprovalLineStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}
