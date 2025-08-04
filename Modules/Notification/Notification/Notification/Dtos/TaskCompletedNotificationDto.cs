namespace Notification.Notification.Dtos;

public record TaskCompletedNotificationDto(
    Guid CorrelationId,
    string TaskName,
    string CompletedBy,
    string ActionTaken,
    long RequestId,
    string PreviousState,
    string NextState,
    DateTime CompletedAt
);