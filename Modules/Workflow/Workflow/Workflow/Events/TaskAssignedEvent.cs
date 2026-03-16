using Shared.Messaging.Values;

namespace Workflow.Workflow.Events;

public record TaskAssignedEvent(
    Guid CorrelationId,
    TaskName TaskName,
    string AssignedTo,
    string AssignedType,
    DateTime AssignedAt
) : IDomainEvent;
