namespace Workflow.Workflow.Events;

public record PendingTaskReassignedDomainEvent(
    Guid TaskId,
    Guid CorrelationId,
    string PreviousAssignedTo,
    string NewAssignedTo,
    Guid WorkflowInstanceId,
    string ActivityId,
    DateTime? DueAt
) : IDomainEvent;
