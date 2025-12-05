namespace Document.Documents.EventHandlers;

public record DocumentLinkedDomainEvent
{
    public Guid DocumentId { get; init; }
}

public class DocumentLinkedDomainEventHandler(
    ILogger<DocumentLinkedDomainEventHandler> logger
) : IConsumer<DocumentLinkedDomainEvent>
{
    public Task Consume(ConsumeContext<DocumentLinkedDomainEvent> context)
    {
        var @event = context.Message;

        // NOTE: Files are now saved directly to permanent storage (upload/documents) during upload
        // No need to move files anymore - this handler is kept for backward compatibility
        logger.LogInformation(
            "Document {DocumentId} linked. File already in permanent storage",
            @event.DocumentId);

        return Task.CompletedTask;
    }
}