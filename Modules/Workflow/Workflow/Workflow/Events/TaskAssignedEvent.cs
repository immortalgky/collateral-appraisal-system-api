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
    string? AppraisalNumber = null,
    string Movement = "F",
    Guid? AppraisalId = null,
    string? ReasonCode = null,
    string? Reason = null
) : IDomainEvent;
