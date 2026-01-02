namespace Notification.Domain.Notifications.Models;

public enum NotificationType
{
    TaskAssigned,
    TaskCompleted,
    WorkflowTransition,
    SystemNotification
}