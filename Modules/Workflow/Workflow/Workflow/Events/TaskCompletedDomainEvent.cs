namespace Workflow.Workflow.Events;

public record TaskCompletedDomainEvent(
    Guid CorrelationId,
    string TaskName,
    string ActionTaken,
    DateTime CompletedAt,
    string? CompletedBy = null,
    string? WorkflowInstanceName = null
) : IDomainEvent;
