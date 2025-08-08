namespace Request.RequestComments.EventHandlers;

public class RequestCommentRemovedEventHandler(ILogger<RequestCommentRemovedEventHandler> logger)
    : INotificationHandler<RequestCommentRemovedEvent>
{
    public Task Handle(RequestCommentRemovedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogWarning("Domain Event: {EventType} - RequestId: {RequestId}, CommentId: {CommentId}, RemovedBy: {RemovedBy}, Comment: {Comment}",
            notification.GetType().Name,
            notification.RequestId,
            notification.CommentId,
            notification.RemovedBy,
            notification.Comment);

        return Task.CompletedTask;
    }
}