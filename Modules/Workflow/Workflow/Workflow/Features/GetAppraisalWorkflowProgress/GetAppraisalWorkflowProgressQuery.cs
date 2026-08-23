using Shared.CQRS;

namespace Workflow.Workflow.Features.GetAppraisalWorkflowProgress;

public record GetAppraisalWorkflowProgressQuery(Guid AppraisalId) : IQuery<GetAppraisalWorkflowProgressResponse>;

public class GetAppraisalWorkflowProgressResponse
{
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowStatus { get; set; }
    public string RouteType { get; set; } = "Unknown";
    public string? CurrentActivityId { get; set; }
    public List<PhaseStepDto> Steps { get; set; } = [];
    public List<ActivityLogItemDto> ActivityLog { get; set; } = [];
}

public class PhaseStepDto
{
    public string Group { get; set; } = default!;
    public string Status { get; set; } = default!; // Completed | Current | Pending | Cancelled
}

public class ActivityLogItemDto
{
    public int SequenceNo { get; set; }
    public string ActivityName { get; set; } = default!;
    public string? TaskDescription { get; set; }
    public string? AssignedTo { get; set; }
    public string? AssignedToDisplayName { get; set; }
    /// <summary>When this row's assignee received the task (PendingTask/CompletedTask
    /// AssigneeAssignedAt), not the frozen SLA anchor — so a reassigned task reports each
    /// holder's own start and <see cref="TimeTaken"/> is that holder's own elapsed time.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>The task's own AssignedAt — when the workflow entered this step. Frozen across a
    /// supervisor hand-off, so several consecutive rows can share it. This is the SLA clock's
    /// reference for assignment-anchored policies; the tooltip shows it beside StartDate.</summary>
    public DateTime StepEnteredAt { get; set; }

    /// <summary>When this row's holder first opened the task. Null = never opened, or the row
    /// predates the column (archived rows are not backfilled — there is no truth to backfill).</summary>
    public DateTime? OpenedAt { get; set; }

    /// <summary>
    /// The task's own status (Assigned / InProgress / Completing / Completed) — distinct from
    /// <see cref="Status"/>, which is just Pending vs Completed for this list. Lets the reader tell a
    /// null <see cref="OpenedAt"/> that means "never opened" (still Assigned) from one that means
    /// "opened, but before the column existed" — the two are not the same claim.
    /// </summary>
    public string? TaskState { get; set; }

    /// <summary>The SLA clock-start anchor for this leg — differs from <see cref="StepEnteredAt"/>
    /// for appointment-anchored and window-governed tasks.</summary>
    public DateTime? SlaStartAt { get; set; }

    public DateTime? DueAt { get; set; }
    public string? SlaStatus { get; set; }
    public int? SlaDurationHours { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ActionTaken { get; set; }
    public string? TimeTaken { get; set; }
    public string? Remark { get; set; }
    public string Status { get; set; } = default!; // Completed | Pending
    public string? Group { get; set; }
    public string? ActivityId { get; set; }
    public string? CompanyName { get; set; }
    /// <summary>Thai company name; null when absent. The client picks by its own locale.</summary>
    public string? CompanyNameLocal { get; set; }
    public string? Movement { get; set; } // F | C (Cancel) | B (back) — "C" marks the cancelled activity
}
