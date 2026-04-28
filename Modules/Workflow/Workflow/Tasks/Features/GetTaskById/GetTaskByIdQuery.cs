using Shared.CQRS;

namespace Workflow.Tasks.Features.GetTaskById;

public record GetTaskByIdQuery(Guid TaskId) : IQuery<TaskDetailResult>;

public record TaskDetailResult
{
    public Guid TaskId { get; init; }
    public Guid RequestId { get; init; }
    public Guid? AppraisalId { get; init; }
    /// <summary>
    /// Populated only for quotation-workflow tasks (pt.CorrelationId = QuotationRequestId).
    /// Null for all other task types. Frontend should use this field — not TaskId — as the
    /// quotation route identifier.
    /// </summary>
    public Guid? QuotationRequestId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public string ActivityId { get; init; } = default!;
    public string AssigneeUserId { get; init; } = default!;
    public string AssignedType { get; init; } = default!;
    public string? TaskName { get; init; }
    public string? TaskDescription { get; init; }
    public bool IsOwner { get; init; }
    public string? WorkingBy { get; init; }
    public DateTime? LockedAt { get; init; }
}
