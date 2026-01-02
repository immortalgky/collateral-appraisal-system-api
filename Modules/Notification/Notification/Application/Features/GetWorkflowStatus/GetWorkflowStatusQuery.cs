namespace Notification.Domain.Notifications.Features.GetWorkflowStatus;

public record GetWorkflowStatusQuery(
    long RequestId
) : IQuery<GetWorkflowStatusResponse>;