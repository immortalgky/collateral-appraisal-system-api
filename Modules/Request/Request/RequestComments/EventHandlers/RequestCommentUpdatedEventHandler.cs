namespace Request.RequestComments.EventHandlers;

public class RequestCommentUpdatedEventHandler(ILogger<RequestCommentUpdatedEventHandler> logger)
    : INotificationHandler<RequestCommentUpdatedEvent>
{
    public Task Handle(RequestCommentUpdatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event: {EventType} - RequestId: {RequestId}, CommentId: {CommentId}, UpdatedBy: {UpdatedBy}, PreviousComment: {PreviousComment}",
            notification.GetType().Name,
            notification.RequestId,
            notification.RequestComment.Id,
            notification.RequestComment.UpdatedBy,
            notification.PreviousComment);

        return Task.CompletedTask;
    }
}