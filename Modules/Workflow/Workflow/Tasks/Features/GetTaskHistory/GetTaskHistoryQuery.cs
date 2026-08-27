using Shared.CQRS;

namespace Workflow.Tasks.Features.GetTaskHistory;

public record GetTaskHistoryQuery(Guid WorkflowInstanceId) : IQuery<GetTaskHistoryResponse>;

public record GetTaskHistoryResponse(IReadOnlyList<TaskHistoryItemDto> Items);

public record TaskHistoryItemDto
{
    public Guid TaskId { get; init; }
    public string TaskName { get; init; } = default!;
    public string? TaskDescription { get; init; }
    public string AssignedTo { get; init; } = default!;
    public string? AssignedToFirstName { get; init; }
    public string? AssignedToLastName { get; init; }
    public string? AssignedToDisplayName { get; init; }
    public string AssignedType { get; init; } = default!;
    /// <summary>The SLA clock anchor — frozen across a supervisor reassign.</summary>
    public DateTime AssignedAt { get; init; }

    /// <summary>
    /// When the assignee on THIS row received the task. Equals <see cref="AssignedAt"/> except on rows
    /// produced by a supervisor reassign. This is the field to order and display on.
    /// </summary>
    public DateTime AssigneeAssignedAt { get; init; }

    /// <summary>When this row's holder first opened the task. Null = never opened, or the row
    /// predates the column (archived rows are not backfilled — there is no truth to backfill).</summary>
    public DateTime? OpenedAt { get; init; }

    /// <summary>
    /// The task's own status (Assigned / InProgress / Completing / Completed).
    ///
    /// Use it to decide how to word a null <see cref="OpenedAt"/>: only a task still <c>Assigned</c>
    /// has provably never been opened. Anything else — including every archived row, which is always
    /// <c>Completed</c> — cannot separate "never opened" from "predates the column", so the honest
    /// wording there is "no record" rather than a claim about the holder's behaviour.
    /// </summary>
    public string? TaskState { get; init; }

    /// <summary>The SLA clock-start anchor for this leg — differs from <see cref="AssignedAt"/> for
    /// appointment-anchored and window-governed tasks.</summary>
    public DateTime? SlaStartAt { get; init; }

    public DateTime? DueAt { get; init; }
    public string? SlaStatus { get; init; }
    public int? SlaDurationHours { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ActionTaken { get; init; }
    public string Movement { get; init; } = "F";
    public string? Remark { get; init; }
}
