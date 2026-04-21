namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Entity tracking appraisal assignments to internal users or external companies.
/// Supports reassignment chain and progress tracking.
/// </summary>
public class AppraisalAssignment : Entity<Guid>
{
    // Core Properties
    public Guid AppraisalId { get; private set; }
    public AssignmentType AssignmentType { get; private set; } = null!;
    public AssignmentStatus AssignmentStatus { get; private set; } = null!;

    // Assignee (one of these will be set based on type)
    public string? AssigneeUserId { get; private set; } // For Internal
    public string? AssigneeCompanyId { get; private set; } // For External

    // External Appraiser Details
    public string? ExternalAppraiserId { get; private set; }
    public string? ExternalAppraiserName { get; private set; }

    // Internal Appraiser Details
    public string? InternalAppraiserId { get; private set; }
    public string? InternalAppraiserName { get; private set; }

    // Assignment Method
    public string AssignmentMethod { get; private set; } = null!; // Manual, AutoRule, Quotation
    public string? InternalFollowupAssignmentMethod { get; private set; } // Manual, RoundRobin
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
    public string AssignedBy { get; private set; } = default!;
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
        AssignmentType assignmentType,
        string? assigneeUserId,
        string? assigneeCompanyId,
        string assignmentMethod,
        string? internalAppraiserId,
        string? internalFollowupMethod,
        Guid? autoRuleId,
        Guid? previousAssignmentId,
        int reassignmentNumber,
        string assignedBy)
    {
        //Id = Guid.CreateVersion7();
        AppraisalId = appraisalId;
        AssignmentType = assignmentType;
        AssigneeUserId = assigneeUserId;
        AssigneeCompanyId = assigneeCompanyId;
        AssignmentMethod = assignmentMethod;
        InternalAppraiserId = internalAppraiserId;
        InternalFollowupAssignmentMethod = internalFollowupMethod;
        AutoRuleId = autoRuleId;
        PreviousAssignmentId = previousAssignmentId;
        ReassignmentNumber = reassignmentNumber;
        AssignmentStatus = AssignmentStatus.Pending;
        AssignedAt = DateTime.Now;
        AssignedBy = assignedBy;
        ProgressPercent = 0;
    }

    /// <summary>
    /// Factory method to create a new assignment
    /// </summary>
    public static AppraisalAssignment Create(
        Guid appraisalId,
        string assignmentType,
        string? assigneeUserId = null,
        string? assigneeCompanyId = null,
        string assignmentMethod = "Manual",
        string? internalAppraiserId = null,
        string? internalFollowupMethod = null,
        Guid? autoRuleId = null,
        Guid? previousAssignmentId = null,
        int reassignmentNumber = 1,
        string assignedBy = default)
    {
        var type = AssignmentType.FromString(assignmentType);

        return new AppraisalAssignment(
            appraisalId,
            type,
            assigneeUserId,
            assigneeCompanyId,
            assignmentMethod,
            internalAppraiserId,
            internalFollowupMethod,
            autoRuleId,
            previousAssignmentId,
            reassignmentNumber,
            assignedBy);
    }

    /// <summary>
    /// Assign the appraisal to an internal user or external company
    /// </summary>
    public void Assign(
        string assignmentType,
        string? assigneeUserId = null,
        string? assigneeCompanyId = null,
        string assignmentMethod = "Manual",
        string? internalAppraiserId = null,
        string? internalFollowupMethod = null,
        Guid? autoRuleId = null,
        Guid? previousAssignmentId = null,
        int reassignmentNumber = 1,
        string assignedBy = default
    )
    {
        AssignmentType = AssignmentType.FromString(assignmentType);
        AssigneeUserId = assigneeUserId;
        AssigneeCompanyId = assigneeCompanyId;
        AssignmentMethod = assignmentMethod;
        InternalAppraiserId = internalAppraiserId;
        InternalFollowupAssignmentMethod = internalFollowupMethod;
        AutoRuleId = autoRuleId;
        PreviousAssignmentId = previousAssignmentId;
        ReassignmentNumber = reassignmentNumber;
        AssignedBy = assignedBy;

        AssignmentStatus = AssignmentStatus.Assigned;
        AssignedAt = DateTime.Now;
    }

    /// <summary>
    /// Attach internal followup staff after the company has been assigned.
    /// </summary>
    public void AssignInternalFollowup(string internalAppraiserId, string internalFollowupMethod)
    {
        InternalAppraiserId = internalAppraiserId;
        InternalFollowupAssignmentMethod = internalFollowupMethod;
    }

    /// <summary>
    /// Start work on this assignment
    /// </summary>
    public void StartWork()
    {
        ValidateStatus(AssignmentStatus.Assigned, "start work");

        AssignmentStatus = AssignmentStatus.InProgress;
        StartedAt = DateTime.Now;
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
        LastProgressUpdate = DateTime.Now;
    }

    /// <summary>
    /// Complete this assignment
    /// </summary>
    public void Complete()
    {
        ValidateStatus(AssignmentStatus.InProgress, "complete");

        AssignmentStatus = AssignmentStatus.Completed;
        ProgressPercent = 100;
        CompletedAt = DateTime.Now;
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
    public void SetExternalAppraiser(string appraiserId, string name, string? license)
    {
        if (AssignmentType != AssignmentType.External)
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