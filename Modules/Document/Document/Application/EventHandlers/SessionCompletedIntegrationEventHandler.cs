using Document.Domain.UploadSessions;

namespace Document.EventHandlers;

public class SessionCompletedIntegrationEventHandler(
    IUploadSessionRepository sessionRepository,
    IDocumentUnitOfWork uow,
    IDateTimeProvider dateTimeProvider,
    ILogger<SessionCompletedIntegrationEventHandler> logger)
    : IConsumer<SessionCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<SessionCompletedIntegrationEvent> @event)
    {
        var message = @event.Message;

        var session = await sessionRepository.GetByIdAsync(message.SessionId, @event.CancellationToken);

        if (session is null)
        {
            logger.LogError("Upload session {SessionId} not found", message.SessionId);
            return;
        }

        session.SetExternalReference(message.RequestId.ToString());
        session.Complete(dateTimeProvider.Now);

        logger.LogInformation("Upload session {SessionId} completed for request {RequestId}",
            message.SessionId, message.RequestId);

        await uow.SaveChangesAsync(@event.CancellationToken);
    }
}
