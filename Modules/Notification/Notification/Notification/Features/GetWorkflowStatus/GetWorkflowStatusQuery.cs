namespace Notification.Notification.Features.GetWorkflowStatus;

public record GetWorkflowStatusQuery(
    long RequestId
) : IQuery<GetWorkflowStatusResponse>;