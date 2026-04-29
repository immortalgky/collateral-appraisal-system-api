namespace Shared.Messaging.Events;

public record DocumentFollowupRequiredIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid FollowupId { get; init; }
    public string ReasonCode { get; init; } = default!;
    public string Reason { get; init; } = default!;
}
