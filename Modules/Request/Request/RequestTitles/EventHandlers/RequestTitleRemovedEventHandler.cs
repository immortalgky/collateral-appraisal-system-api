namespace Request.RequestTitles.EventHandlers;

public class RequestTitleRemovedEventHandler(ILogger<RequestTitleRemovedEventHandler> logger)
    : INotificationHandler<RequestTitleRemovedEvent>
{
    public Task Handle(RequestTitleRemovedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event: {EventType} - RequestId: {RequestId}, TitleId: {TitleId}",
            notification.GetType().Name,
            notification.RequestId,
            notification.TitleId);

        return Task.CompletedTask;
    }
}