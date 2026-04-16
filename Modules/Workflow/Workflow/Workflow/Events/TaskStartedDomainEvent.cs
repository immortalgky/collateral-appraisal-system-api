namespace Workflow.Workflow.Events;

public record TaskStartedDomainEvent(
    Guid CorrelationId,
    string AssignedTo,
    DateTime AssignedAt,
    string? PreviousAssignedTo = null  // non-null only for pool tasks opened via OpenTask
) : IDomainEvent;
