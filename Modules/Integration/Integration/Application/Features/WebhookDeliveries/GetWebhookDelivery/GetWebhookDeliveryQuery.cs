using Shared.CQRS;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDelivery;

public record GetWebhookDeliveryQuery(Guid Id) : IQuery<WebhookDeliveryDetailDto?>;

public record WebhookDeliveryDetailDto(
    Guid Id,
    Guid SubscriptionId,
    string SystemCode,
    string EventType,
    string Payload,
    string Status,
    int AttemptCount,
    int? LastStatusCode,
    string? LastError,
    DateTime? DeliveredAt,
    DateTime CreatedAt
);
