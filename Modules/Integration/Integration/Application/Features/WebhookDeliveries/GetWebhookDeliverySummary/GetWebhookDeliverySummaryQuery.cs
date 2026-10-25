using Shared.CQRS;

namespace Integration.Application.Features.WebhookDeliveries.GetWebhookDeliverySummary;

/// <summary>Same filters as the list, minus status: the counts feed the status tabs.</summary>
public record GetWebhookDeliverySummaryQuery(
    Guid? SubscriptionId,
    string? SystemCode,
    string? EventType,
    string? Search,
    DateTime? FromDate,
    DateTime? ToDate
) : IQuery<WebhookDeliverySummaryDto>;

public record WebhookDeliverySummaryDto(
    int Total,
    int Pending,
    int Delivered,
    int Failed,
    OldestPendingDto? OldestPending,
    List<EventTypeCountDto> FailedByEventType,
    List<FailureCauseDto> FailedByCause
);

public record OldestPendingDto(
    DateTime? CreatedAt,
    int NeverAttemptedCount,
    List<SystemCountDto> BySystem
);

public record SystemCountDto(string SystemCode, int Count);

public record EventTypeCountDto(string EventType, int Count);

public record FailureCauseDto(
    string SystemCode,
    int? LastStatusCode,
    string HttpMethod,
    string CallbackUrl,
    int Count
);
