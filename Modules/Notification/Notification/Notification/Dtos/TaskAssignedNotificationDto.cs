namespace Notification.Notification.Dtos;

public record TaskAssignedNotificationDto(
    Guid CorrelationId,
    string TaskName,
    string AssignedTo,
    string AssignedType,
    long RequestId,
    string CurrentState,
    DateTime AssignedAt
);