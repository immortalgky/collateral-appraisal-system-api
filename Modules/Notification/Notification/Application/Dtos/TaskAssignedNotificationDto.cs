namespace Notification.Domain.Notifications.Dtos;

public record TaskAssignedNotificationDto(
    Guid CorrelationId,
    string TaskName,
    string AssignedTo,
    string AssignedType,
    string AppraisalNumber,
    string CurrentState,
    DateTime AssignedAt,
    string? NotifiedTo = default!
);
