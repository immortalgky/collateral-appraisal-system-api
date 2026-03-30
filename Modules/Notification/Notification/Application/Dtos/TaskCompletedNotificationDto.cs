namespace Notification.Domain.Notifications.Dtos;

public record TaskCompletedNotificationDto(
    Guid CorrelationId,
    string TaskName,
    string CompletedBy,
    string ActionTaken,
    string AppraisalNumber,
    string PreviousState,
    string NextState,
    DateTime CompletedAt
);
