namespace Workflow.Workflow.Events;

public record TaskAssignedEvent(
    Guid CorrelationId,
    string TaskName,
    string AssignedTo,
    string AssignedType,
    DateTime AssignedAt,
    Guid WorkflowInstanceId,
    string ActivityId,
    DateTime? DueAt = null,
    string? StartedBy = null,
    string? WorkflowInstanceName = null,
    string? TaskDescription = null,
    string? CompletedBy = null,
    string? AppraisalNumber = null
) : IDomainEvent;
