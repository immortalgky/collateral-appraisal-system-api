using Document.Data;
using Shared.Data;
using Shared.Messaging.Filters;

namespace Document.EventHandlers;

public class DocumentUnlinkedIntegrationEventHandler(
    IDocumentUnitOfWork uow,
    IDateTimeProvider dateTimeProvider,
    ILogger<DocumentUnlinkedIntegrationEventHandler> logger,
    InboxGuard<DocumentDbContext> inboxGuard)
    : IConsumer<DocumentUnlinkedIntegrationEvent>
{
    private readonly IRepository<Domain.Documents.Models.Document, Guid> _documentRepository =
        uow.Repository<Domain.Documents.Models.Document, Guid>();

    public async Task Consume(ConsumeContext<DocumentUnlinkedIntegrationEvent> @event)
    {
        if (await inboxGuard.TryClaimAsync(@event.MessageId, GetType().Name, @event.CancellationToken))
            return;

        var document =
            await _documentRepository.GetByIdAsync(@event.Message.DocumentId, @event.CancellationToken);

        if (document is null)
        {
            logger.LogError("Document {DocumentId} not found for unlinking", @event.Message.DocumentId);
            return;
        }

        document.Unlink(dateTimeProvider.Now);

        logger.LogInformation("Document {DocumentId} unlinked, ReferenceCount={ReferenceCount}",
            @event.Message.DocumentId, document.ReferenceCount);

        await uow.SaveChangesAsync(@event.CancellationToken);

        await inboxGuard.MarkAsProcessedAsync(@event.MessageId, GetType().Name, @event.CancellationToken);
    }
}
