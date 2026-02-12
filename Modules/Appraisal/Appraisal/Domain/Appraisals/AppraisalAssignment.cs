namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Entity tracking appraisal assignments to internal users or external companies.
/// Supports reassignment chain and progress tracking.
/// </summary>
public class AppraisalAssignment : Entity<Guid>
{
    // Core Properties
    public Guid AppraisalId { get; private set; }
    public AssignmentMode AssignmentMode { get; private set; } = null!;
    public AssignmentStatus AssignmentStatus { get; private set; } = null!;

    // Assignee (one of these will be set based on mode)
    public Guid? AssigneeUserId { get; private set; } // For Internal
    public Guid? AssigneeCompanyId { get; private set; } // For External

    // External Appraiser Details
    public Guid? ExternalAppraiserId { get; private set; }
    public string? ExternalAppraiserName { get; private set; }

    // Internal Appraiser Details
    public Guid? InternalAppraiserId { get; private set; }
    public string? InternalAppraiserName { get; private set; }

    // Assignment Source
    public string AssignmentSource { get; private set; } = null!; // Manual, AutoRule, Quotation
    public Guid? AutoRuleId { get; private set; }
    public Guid? QuotationRequestId { get; private set; }

    // Reassignment Chain
    public Guid? PreviousAssignmentId { get; private set; }
    public int ReassignmentNumber { get; private set; } = 1;

    // Progress Tracking
    public int ProgressPercent { get; private set; }
    public DateTime? LastProgressUpdate { get; private set; }

    // Timestamps
    public DateTime AssignedAt { get; private set; }
    public Guid AssignedBy { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public string? CancellationReason { get; private set; }
    public string? Notes { get; private set; }

    private AppraisalAssignment()
    {
        // For EF Core
    }

    // Private constructor for factory
    private AppraisalAssignment(
        Guid appraisalId,
        AssignmentMode assignmentMode,
        Guid? assigneeUserId,
        Guid? assigneeCompanyId,
        string assignmentSource,
        Guid? autoRuleId,
        Guid? previousAssignmentId,
        int reassignmentNumber,
        Guid assignedBy)
    {
        Id = Guid.CreateVersion7();
        AppraisalId = appraisalId;
        AssignmentMode = assignmentMode;
        AssigneeUserId = assigneeUserId;
        AssigneeCompanyId = assigneeCompanyId;
        AssignmentSource = assignmentSource;
        AutoRuleId = autoRuleId;
        PreviousAssignmentId = previousAssignmentId;
        ReassignmentNumber = reassignmentNumber;
        AssignmentStatus = AssignmentStatus.Assigned;
        AssignedAt = DateTime.UtcNow;
        AssignedBy = assignedBy;
        ProgressPercent = 0;
    }

    /// <summary>
    /// Factory method to create a new assignment
    /// </summary>
    public static AppraisalAssignment Create(
        Guid appraisalId,
        string assignmentMode,
        Guid? assigneeUserId = null,
        Guid? assigneeCompanyId = null,
        string assignmentSource = "Manual",
        Guid? autoRuleId = null,
        Guid? previousAssignmentId = null,
        int reassignmentNumber = 1,
        Guid assignedBy = default)
    {
        var mode = AssignmentMode.FromString(assignmentMode);

        return new AppraisalAssignment(
            appraisalId,
            mode,
            assigneeUserId,
            assigneeCompanyId,
            assignmentSource,
            autoRuleId,
            previousAssignmentId,
            reassignmentNumber,
            assignedBy);
    }

    /// <summary>
    /// Start work on this assignment
    /// </summary>
    public void StartWork()
    {
        ValidateStatus(AssignmentStatus.Assigned, "start work");

        AssignmentStatus = AssignmentStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update progress percentage
    /// </summary>
    public void UpdateProgress(int percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentException("Progress must be between 0 and 100");

        if (AssignmentStatus != AssignmentStatus.InProgress)
            throw new InvalidOperationException("Can only update progress for in-progress assignments");

        ProgressPercent = percent;
        LastProgressUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Complete this assignment
    /// </summary>
    public void Complete()
    {
        ValidateStatus(AssignmentStatus.InProgress, "complete");

        AssignmentStatus = AssignmentStatus.Completed;
        ProgressPercent = 100;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reject this assignment
    /// </summary>
    public void Reject(string reason)
    {
        if (AssignmentStatus == AssignmentStatus.Completed ||
            AssignmentStatus == AssignmentStatus.Cancelled)
            throw new InvalidOperationException($"Cannot reject assignment in status '{AssignmentStatus}'");

        AssignmentStatus = AssignmentStatus.Rejected;
        RejectionReason = reason;
    }

    /// <summary>
    /// Cancel this assignment
    /// </summary>
    public void Cancel(string reason)
    {
        if (AssignmentStatus == AssignmentStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed assignment");

        AssignmentStatus = AssignmentStatus.Cancelled;
        CancellationReason = reason;
    }

    /// <summary>
    /// Set external appraiser details
    /// </summary>
    public void SetExternalAppraiser(Guid appraiserId, string name, string? license)
    {
        if (AssignmentMode != AssignmentMode.External)
            throw new InvalidOperationException("Can only set external appraiser for external assignments");

        ExternalAppraiserId = appraiserId;
        ExternalAppraiserName = name;
    }

    private void ValidateStatus(AssignmentStatus expectedStatus, string action)
    {
        if (AssignmentStatus != expectedStatus)
            throw new InvalidOperationException(
                $"Cannot {action} assignment in status '{AssignmentStatus}'. Expected: '{expectedStatus}'");
    }
}