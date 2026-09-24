using Document.Data;
using Shared.Data;
using Shared.Messaging.Filters;

namespace Document.EventHandlers;

public class DocumentUpdatedIntegrationEventHandler(
    IDocumentUnitOfWork uow,
    IDateTimeProvider dateTimeProvider,
    ILogger<DocumentUpdatedIntegrationEventHandler> logger,
    InboxGuard<DocumentDbContext> inboxGuard)
    : IConsumer<DocumentUpdatedIntegrationEvent>
{
    private readonly IRepository<Domain.Documents.Models.Document, Guid> _documentRepository =
        uow.Repository<Domain.Documents.Models.Document, Guid>();

    public Task Consume(ConsumeContext<DocumentUpdatedIntegrationEvent> @event) =>
        inboxGuard.RunOnceAsync(@event.MessageId, GetType().Name, _ => HandleAsync(@event), @event.CancellationToken);

    private async Task HandleAsync(ConsumeContext<DocumentUpdatedIntegrationEvent> @event)
    {
        var message = @event.Message;

        // Unlink the previous document
        var previousDocument =
            await _documentRepository.GetByIdAsync(message.PreviousDocumentId, @event.CancellationToken);

        if (previousDocument is not null)
        {
            previousDocument.Unlink(dateTimeProvider.Now);
            logger.LogInformation("Previous document {DocumentId} unlinked", message.PreviousDocumentId);
        }
        else
        {
            logger.LogError("Previous document {DocumentId} not found for unlinking", message.PreviousDocumentId);
        }

        // Link the new document
        var newDocument =
            await _documentRepository.GetByIdAsync(message.DocumentId, @event.CancellationToken);

        if (newDocument is not null)
        {
            newDocument.Link(dateTimeProvider.Now);
            logger.LogInformation("New document {DocumentId} linked", message.DocumentId);
        }
        else
        {
            logger.LogError("New document {DocumentId} not found for linking", message.DocumentId);
        }

        await uow.SaveChangesAsync(@event.CancellationToken);
    }
}
