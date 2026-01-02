using Shared.Messaging.Values;

namespace Notification.Domain.Notifications.Dtos;

public record TaskCompletedNotificationDto(
    Guid CorrelationId,
    TaskName TaskName,
    string CompletedBy,
    string ActionTaken,
    long RequestId,
    string PreviousState,
    string NextState,
    DateTime CompletedAt
);