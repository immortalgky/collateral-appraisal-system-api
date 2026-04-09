namespace Notification.Domain.Notifications.Models;

public enum NotificationType
{
    TaskAssigned,
    TaskCompleted,
    WorkflowTransition,
    SystemNotification,
    DocumentFollowupRaised,
    DocumentFollowupResolved,
    DocumentFollowupCancelled,
    DocumentLineItemDeclined
}
