namespace Request.RequestTitles.EventHandlers;

public class RequestTitleRemovedEventHandler(ILogger<RequestTitleRemovedEventHandler> logger)
    : INotificationHandler<RequestTitleRemovedEvent>
{
    public Task Handle(RequestTitleRemovedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Domain Event: {EventType} - RequestId: {RequestId}, TitleId: {TitleId}, CollateralType: {CollateralType}",
            notification.GetType().Name,
            notification.RequestId,
            notification.TitleId,
            notification.CollateralType);

        return Task.CompletedTask;
    }
}