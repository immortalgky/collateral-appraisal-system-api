namespace Shared.Messaging.Events;

/// <summary>
/// Published by <c>WorkflowLifecycleManager</c> on every activity advance — including
/// the initial transition into the start activity (SourceActivityId == null) and the
/// terminal completion (DestinationActivityId == null). Acts as the single generic signal
/// for all workflow transitions; each subscribing module maps the activity IDs to its own
/// state model without requiring dedicated event types per transition.
/// </summary>
public record WorkflowTransitionedIntegrationEvent(
    Guid WorkflowInstanceId,
    Guid CorrelationId,
    Guid? AppraisalId,
    string? SourceActivityId,
    string? DestinationActivityId,
    string? CompletedBy,
    DateTime CompletedAt) : IntegrationEvent;
