namespace Request.RequestTitles.EventHandlers;

public class RequestTitleAddedEventHandler(ILogger<RequestTitleAddedEventHandler> logger)
    : INotificationHandler<RequestTitleAddedEvent>
{
    public Task Handle(RequestTitleAddedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event: {EventType} - RequestId: {RequestId}, TitleId: {TitleId}, CollateralType: {CollateralType}",
            notification.GetType().Name,
            notification.RequestId,
            notification.RequestTitle.Id,
            notification.RequestTitle.CollateralType);

        return Task.CompletedTask;
    }
}