using Shared.Messaging.Values;

namespace Workflow.Workflow.Events;

public record TaskCompletedDomainEvent(
    Guid CorrelationId,
    TaskName TaskName,
    string ActionTaken,
    DateTime CompletedAt
) : IDomainEvent;
