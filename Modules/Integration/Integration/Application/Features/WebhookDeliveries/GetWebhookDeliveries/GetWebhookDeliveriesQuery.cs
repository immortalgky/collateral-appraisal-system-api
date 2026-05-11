using Shared.CQRS;
using Shared.Pagination;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDeliveries;

public record GetWebhookDeliveriesQuery(
    int PageNumber,
    int PageSize,
    string? Status,
    Guid? SubscriptionId,
    string? EventType,
    DateTime? FromDate,
    DateTime? ToDate
) : IQuery<PaginatedResult<WebhookDeliveryListDto>>;

public record WebhookDeliveryListDto(
    Guid Id,
    Guid SubscriptionId,
    string SystemCode,
    string EventType,
    string Status,
    int AttemptCount,
    int? LastStatusCode,
    string? LastError,
    DateTime? DeliveredAt,
    DateTime CreatedAt
);
