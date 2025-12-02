namespace Request.RequestTitles.EventHandlers;

public class RequestTitleAddedEventHandler(ILogger<RequestTitleAddedEventHandler> logger)
    : INotificationHandler<RequestTitleCreatedEvent>
{
    public Task Handle(RequestTitleCreatedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event: {EventType} - RequestId: {RequestId}, TitleId: {TitleId}, CollateralType: {CollateralType}",
            notification.GetType().Name,
            notification.RequestId,
            notification.RequestTitle.Id,
            notification.RequestTitle.CollateralType);

        return Task.CompletedTask;
    }
}