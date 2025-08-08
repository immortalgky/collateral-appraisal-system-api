namespace Request.RequestComments.EventHandlers;

public class RequestCommentAddedEventHandler(ILogger<RequestCommentAddedEventHandler> logger)
    : INotificationHandler<RequestCommentAddedEvent>
{
    public Task Handle(RequestCommentAddedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event: {EventType} - RequestId: {RequestId}, CommentId: {CommentId}, CreatedBy: {CreatedBy}",
            notification.GetType().Name,
            notification.RequestId,
            notification.RequestComment.Id,
            notification.RequestComment.CreatedBy);

        return Task.CompletedTask;
    }
}