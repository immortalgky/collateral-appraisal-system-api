using System.Text.Json.Serialization;

namespace Workflow.FeeAppointmentApprovals.Domain;

/// <summary>
/// A single component within a FeeAppointmentApproval bundle.
/// LineType = Appointment carries the NewDate snapshot; LineType = Fee carries FeeCode/Description/Amount.
///
/// Persisted as a JSON column (see FeeAppointmentApprovalConfiguration). The properties have
/// private setters, so each carries [JsonInclude] — otherwise System.Text.Json silently skips
/// them on deserialization and every line reads back as a default Appointment line with null
/// values.
/// </summary>
public class FeeAppointmentApprovalLine
{
    [JsonInclude] public Guid Id { get; private set; }
    [JsonInclude] public FeeApprovalLineType LineType { get; private set; }
    [JsonInclude] public Guid TargetId { get; private set; }

    // Appointment snapshot
    [JsonInclude] public DateTime? NewDate { get; private set; }
    [JsonInclude] public int? RescheduleCount { get; private set; }

    // Fee snapshot
    [JsonInclude] public string? FeeCode { get; private set; }
    [JsonInclude] public string? FeeDescription { get; private set; }
    [JsonInclude] public decimal? FeeAmount { get; private set; }

    [JsonInclude] public FeeApprovalLineStatus LineStatus { get; private set; }
    [JsonInclude] public string? DecisionReason { get; private set; }

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
