using Workflow.FeeAppointmentApprovals.Domain.Events;

namespace Workflow.FeeAppointmentApprovals.Domain;

/// <summary>
/// Aggregate representing an approval request for a bundle of external-company changes
/// (appointment postpone + additional fee items). Mirrors the DocumentFollowup aggregate.
/// Status flow: Open → Resolved | Cancelled.
/// A child approval workflow (FollowupWorkflowInstanceId) drives a single approver task.
/// </summary>
public class FeeAppointmentApproval : Aggregate<Guid>
{
    public List<FeeAppointmentApprovalLine> Lines { get; private set; } = new();

    public Guid AppraisalId { get; private set; }

    /// <summary>
    /// Who raised the request, derived from the acting user — "Ext" (external company) or "Int"
    /// (bank-internal). Drives which AppointmentApprovalRule / FeeApprovalTier rows apply
    /// (AppliesTo matches this or "Both"). Internal requests auto-apply unless Int/Both config is seeded.
    /// </summary>
    public string RequestSource { get; private set; } = default!;

    public FeeAppointmentApprovalStatus Status { get; private set; }

    /// <summary>The tier code of the strictest approver, e.g. "IntAdmin" or "Checker".</summary>
    public string? ResolvedTier { get; private set; }

    /// <summary>
    /// Assignee code of the resolved approver (user or group code, per AssignedType).
    /// Set when the child workflow is spawned.
    /// </summary>
    public string? ApproverAssignee { get; private set; }

    /// <summary>"1" = user, "2" = group</summary>
    public string? AssignedType { get; private set; }

    public Guid? FollowupWorkflowInstanceId { get; private set; }

    public string? CancellationReason { get; private set; }
    public DateTime RaisedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private FeeAppointmentApproval() { }

    /// <summary>
    /// Factory: creates a new Open FeeAppointmentApproval with the provided lines.
    /// Raises FeeAppointmentApprovalRaisedDomainEvent.
    /// </summary>
    public static FeeAppointmentApproval Raise(
        Guid appraisalId,
        string requestSource,
        string approverAssignee,
        string assignedType,
        string resolvedTier,
        IEnumerable<FeeAppointmentApprovalLine> lines)
    {
        if (string.IsNullOrWhiteSpace(approverAssignee))
            throw new ArgumentException("ApproverAssignee is required", nameof(approverAssignee));

        var lineList = lines?.ToList() ?? new();
        if (lineList.Count == 0)
            throw new ArgumentException("At least one line is required", nameof(lines));

        var approval = new FeeAppointmentApproval
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            RequestSource = requestSource,
            ApproverAssignee = approverAssignee,
            AssignedType = assignedType,
            ResolvedTier = resolvedTier,
            Status = FeeAppointmentApprovalStatus.Open,
            Lines = lineList,
            RaisedAt = DateTime.Now
        };

        approval.AddDomainEvent(new FeeAppointmentApprovalRaisedDomainEvent(
            approval.Id,
            approval.AppraisalId,
            approverAssignee,
            assignedType));

        return approval;
    }

    public void AttachFollowupWorkflowInstance(Guid workflowInstanceId)
    {
        FollowupWorkflowInstanceId = workflowInstanceId;
    }

    /// <summary>
    /// Resolves the approval with per-component decisions:
    /// - appointmentDecision: "approve" or "reject"
    /// - feeDecision: "approve" or "reject" (applied to ALL fee lines as a group)
    /// Raises FeeAppointmentApprovalResolvedDomainEvent.
    /// </summary>
    public void Resolve(
        string appointmentDecision,
        string? appointmentReason,
        string feeDecision,
        string? feeReason,
        string? resolvedByCode = null)
    {
        if (Status != FeeAppointmentApprovalStatus.Open)
            throw new InvalidOperationException("Approval is not open");

        var outcomes = new List<(string LineType, Guid TargetId, string Decision, string? Reason)>();

        foreach (var line in Lines)
        {
            if (line.LineType == FeeApprovalLineType.Appointment)
            {
                var decision = NormalizeDecision(appointmentDecision);
                if (decision == "approve") line.MarkApproved();
                else line.MarkRejected(appointmentReason);
                outcomes.Add(("Appointment", line.TargetId, decision == "approve" ? "Approved" : "Rejected", appointmentReason));
            }
            else // Fee
            {
                var decision = NormalizeDecision(feeDecision);
                if (decision == "approve") line.MarkApproved();
                else line.MarkRejected(feeReason);
                outcomes.Add(("Fee", line.TargetId, decision == "approve" ? "Approved" : "Rejected", feeReason));
            }
        }

        Status = FeeAppointmentApprovalStatus.Resolved;
        ResolvedAt = DateTime.Now;

        AddDomainEvent(new FeeAppointmentApprovalResolvedDomainEvent(Id, AppraisalId, outcomes, resolvedByCode));
    }

    /// <summary>
    /// Cancels the approval (e.g. when the parent workflow is cancelled).
    /// </summary>
    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required", nameof(reason));
        if (Status != FeeAppointmentApprovalStatus.Open) return;

        foreach (var line in Lines.Where(l => l.LineStatus == FeeApprovalLineStatus.Pending))
            line.MarkCancelled(reason);

        Status = FeeAppointmentApprovalStatus.Cancelled;
        CancellationReason = reason;
        ResolvedAt = DateTime.Now;

        AddDomainEvent(new FeeAppointmentApprovalCancelledDomainEvent(Id, AppraisalId, reason));
    }

    private static string NormalizeDecision(string decision) =>
        decision?.Trim().ToLowerInvariant() ?? "reject";
}

public enum FeeAppointmentApprovalStatus
{
    Open = 0,
    Resolved = 1,
    Cancelled = 2
}
