namespace Shared.Messaging.Events;

public record DocumentFollowupRequiredIntegrationEvent : IntegrationEvent
{
    public Guid AppraisalId { get; init; }
    public Guid FollowupId { get; init; }
    public string ReasonCode { get; init; } = default!;
    public string Reason { get; init; } = default!;

    // Defaults to empty so outbox messages serialized before this field existed still deserialize.
    public IReadOnlyList<DocumentFollowupRequiredDocument> Documents { get; init; } = [];
}

public record DocumentFollowupRequiredDocument(string DocumentType, string DocumentTypeName, string Remark);
