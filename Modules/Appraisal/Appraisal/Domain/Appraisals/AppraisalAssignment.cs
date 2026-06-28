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
    // TODO: remove with a follow-up EF migration. The workflow-driven assignment flow no longer
    // needs this linkage column — fee resolution looks the QuotationRequest up by appraisal id.
    // Kept temporarily so existing rows don't break and to avoid scope creep in the refactor PR.
    public Guid? QuotationRequestId { get; private set; }

    // Reassignment Chain
    public Guid? PreviousAssignmentId { get; private set; }
    public int ReassignmentNumber { get; private set; } = 1;

    // Progress Tracking
    public int ProgressPercent { get; private set; }
    public DateTime? LastProgressUpdate { get; private set; }

    // Timestamps
    /// <summary>
    /// Stamped only when the workflow actually hands the task to an external company or internal
    /// appraiser via <see cref="Assign"/>. Null during administration and quotation-selection phases.
    /// </summary>
    public DateTime? AssignedAt { get; private set; }
    public string AssignedBy { get; private set; } = default!;
    public DateTime? StartedAt { get; private set; }
    /// <summary>
    /// Stamped exactly once on the first appraiser handoff (InProgress → UnderReview).
    /// Never overwritten on rework — <see cref="Cycles"/> captures per-round timing instead.
    /// Used by vw_EligibleAssignments as the appraiser-side submission date.
    /// </summary>
    public DateTime? SubmittedAt { get; private set; }
    /// <summary>
    /// Snapshot of the assignment-level SLA budget, frozen at the moment the stage clock starts
    /// (workflow transition into the stage's StartActivityKey). Null if no Stage SlaPolicy matched.
    /// SLA status, actual days, and within-SLA flag are derived in vw_AssignmentList from this +
    /// StartedAt + SubmittedAt — no precomputed fields persisted.
    /// Value is local-kind (matches GETDATE() in SQL views).
    /// </summary>
    public DateTime? SLADueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public string? CancellationReason { get; private set; }
    public string? Notes { get; private set; }

    // External engagement cycles — one per round-trip through ext-* activities
    private readonly List<ExternalEngagementCycle> _cycles = new();
    public IReadOnlyCollection<ExternalEngagementCycle> Cycles => _cycles.AsReadOnly();

    public int TotalExternalBusinessMinutes =>
        _cycles.Where(c => c.BusinessMinutes is not null).Sum(c => c.BusinessMinutes!.Value);

    public int SubmissionCount => _cycles.Count(c => c.Status == CycleStatus.Closed);

    /// <summary>
    /// True once the bank's verifier (int-appraisal-verification) has accepted the book.
    /// Invoicing is allowed from Verified onward (and remains true at terminal Completed).
    /// </summary>
    public bool IsInvoiceEligible =>
        AssignmentStatus == AssignmentStatus.Verified ||
        AssignmentStatus == AssignmentStatus.Completed;

    /// <summary>
    /// Stamps the assignment-level SLA deadline. No-op if already set — rework does not reset the clock.
    /// </summary>
    public void SetSlaDueDate(DateTime dueAt)
    {
        if (dueAt == default) throw new ArgumentException("dueAt must not be the default DateTime value.", nameof(dueAt));
        if (SLADueDate.HasValue) return;
        SLADueDate = dueAt;
    }

    /// <summary>
    /// Re-stamps the assignment-level SLA deadline unconditionally. Used when an appointment
    /// date changes and the governing stage window (AppointmentDate-anchored) must shift the
    /// deadline. Unlike <see cref="SetSlaDueDate"/>, this is NOT frozen after the first call —
    /// reschedules are a deliberate override of the prior value.
    /// </summary>
    public void RecalculateSlaDueDate(DateTime newDueAt)
    {
        if (newDueAt == default) throw new ArgumentException("newDueAt must not be the default DateTime value.", nameof(newDueAt));
        SLADueDate = newDueAt;
    }

    /// <summary>Opens a new external engagement cycle. Idempotent: returns the existing open cycle if one is already open.</summary>
    public ExternalEngagementCycle OpenExternalCycle(DateTime openedAt)
    {
        // FirstOrDefault ordered by CycleNumber DESC — defensive against any accidental double-open state.
        var openCycle = _cycles.OrderByDescending(c => c.CycleNumber).FirstOrDefault(c => c.Status == CycleStatus.Open);
        if (openCycle is not null) return openCycle;
        var nextNumber = _cycles.Count == 0 ? 1 : _cycles.Max(c => c.CycleNumber) + 1;
        var cycle = ExternalEngagementCycle.Open(Id, nextNumber, openedAt);
        _cycles.Add(cycle);
        return cycle;
    }

    /// <summary>Closes the currently open cycle (highest CycleNumber). Returns null if no open cycle exists.</summary>
    public ExternalEngagementCycle? CloseLatestOpenCycle(DateTime closedAt, int businessMinutes)
    {
        var open = _cycles.OrderByDescending(c => c.CycleNumber).FirstOrDefault(c => c.Status == CycleStatus.Open);
        open?.Close(closedAt, businessMinutes);
        return open;
    }

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
    /// Links this assignment to the QuotationRequest that produced it.
    /// Used by the Quotation fee-source resolution path to backfill the linkage on the assignment.
    /// </summary>
    public void SetQuotationRequestId(Guid quotationRequestId)
    {
        QuotationRequestId = quotationRequestId;
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
        ValidateStatus("start work", AssignmentStatus.Assigned);

        AssignmentStatus = AssignmentStatus.InProgress;
        StartedAt = DateTime.Now;
    }

    /// <summary>
    /// Bank-side review is in progress. Reachable from:
    ///   - InProgress (handoff: ext-appraisal-verification → appraisal-book-verification, or
    ///     int-appraisal-execution → int-appraisal-check). Stamps SubmittedAt.
    ///   - Verified (demote: meeting/approval routed back to appraisal-book-verification on the
    ///     External path — bank is re-examining, no longer eligible to invoice). Does NOT
    ///     re-stamp SubmittedAt because the appraiser did not resubmit.
    /// Idempotent if already UnderReview.
    /// </summary>
    public void MarkUnderReview()
    {
        if (AssignmentStatus == AssignmentStatus.UnderReview) return;
        ValidateStatus("mark under review", AssignmentStatus.InProgress, AssignmentStatus.Verified);

        var isFirstSubmission = AssignmentStatus == AssignmentStatus.InProgress && SubmittedAt is null;
        AssignmentStatus = AssignmentStatus.UnderReview;
        if (isFirstSubmission) SubmittedAt = DateTime.Now;
    }

    /// <summary>
    /// Bank verifier (int-appraisal-verification) accepted the book. Invoicing is allowed from here.
    /// </summary>
    public void MarkVerified()
    {
        if (AssignmentStatus == AssignmentStatus.Verified) return;
        ValidateStatus("mark verified", AssignmentStatus.UnderReview);

        AssignmentStatus = AssignmentStatus.Verified;
    }

    /// <summary>
    /// Routeback from bank-side review (book-verification, int-verification, meeting, or approval)
    /// back to the appraiser. Returns the assignment to InProgress directly — there is no separate
    /// ReturnedForRework state. For External, opens a new ExternalEngagementCycle as the durable
    /// signal that rework occurred.
    /// </summary>
    public void Rework(string reason, DateTime cycleOpenedAt)
    {
        ValidateStatus("rework", AssignmentStatus.UnderReview, AssignmentStatus.Verified);

        AssignmentStatus = AssignmentStatus.InProgress;

        // Append the rework reason to Notes rather than overwriting; multiple round-trips would
        // otherwise destroy prior context.
        if (!string.IsNullOrWhiteSpace(reason))
        {
            var stamp = $"[Rework {DateTime.Now:yyyy-MM-dd HH:mm}] {reason}";
            Notes = string.IsNullOrWhiteSpace(Notes) ? stamp : $"{Notes}\n{stamp}";
        }

        if (AssignmentType == AssignmentType.External)
        {
            OpenExternalCycle(cycleOpenedAt);
        }
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
    /// Terminal completion — only legal from Verified. Committee approval owns this transition.
    /// </summary>
    public void Complete()
    {
        ValidateStatus("complete", AssignmentStatus.Verified);

        AssignmentStatus = AssignmentStatus.Completed;
        ProgressPercent = 100;
        CompletedAt = DateTime.Now;
    }

    /// <summary>
    /// Assignee declines the engagement. Only legal before the work has been handed to the bank —
    /// once the appraiser has submitted (UnderReview) or the bank has verified (Verified), the
    /// product is on the bank's side and rejection by the assignee is meaningless. Use Cancel
    /// (bank withdraws) for late-stage termination instead.
    /// </summary>
    public void Reject(string reason)
    {
        ValidateStatus("reject",
            AssignmentStatus.Pending,
            AssignmentStatus.Assigned,
            AssignmentStatus.InProgress);

        AssignmentStatus = AssignmentStatus.Rejected;
        RejectionReason = reason;
    }

    /// <summary>
    /// Bank cancels the engagement. Idempotent if already Cancelled. Disallowed once terminal
    /// (Completed) or already failed (Rejected).
    /// </summary>
    public void Cancel(string reason)
    {
        if (AssignmentStatus == AssignmentStatus.Cancelled) return;
        if (AssignmentStatus == AssignmentStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed assignment");
        if (AssignmentStatus == AssignmentStatus.Rejected)
            throw new InvalidOperationException("Cannot cancel a rejected assignment");

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

    private void ValidateStatus(string action, params AssignmentStatus[] expectedStatuses)
    {
        if (expectedStatuses.Any(s => AssignmentStatus == s)) return;

        var expected = string.Join(" or ", expectedStatuses.Select(s => $"'{s}'"));
        throw new InvalidOperationException(
            $"Cannot {action} assignment in status '{AssignmentStatus}'. Expected: {expected}");
    }
}