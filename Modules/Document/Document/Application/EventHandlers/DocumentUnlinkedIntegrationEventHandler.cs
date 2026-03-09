using Shared.Data;

namespace Document.EventHandlers;

public class DocumentUnlinkedIntegrationEventHandler(
    IDocumentUnitOfWork uow,
    IDateTimeProvider dateTimeProvider,
    ILogger<DocumentUnlinkedIntegrationEventHandler> logger)
    : IConsumer<DocumentUnlinkedIntegrationEvent>
{
    private readonly IRepository<Domain.Documents.Models.Document, Guid> _documentRepository =
        uow.Repository<Domain.Documents.Models.Document, Guid>();

    public async Task Consume(ConsumeContext<DocumentUnlinkedIntegrationEvent> @event)
    {
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
    }
}
