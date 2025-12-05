using Document.Documents;
using Document.UploadSessions;
using Document.UploadSessions.Model;
using Shared.Data;

namespace Document.EventHandlers;

public class DocumentLinkedIntegrationEventHandler(
    IDocumentUnitOfWork uow,
    IDateTimeProvider dateTimeProvider,
    ILogger<DocumentLinkedIntegrationEventHandler> logger)
    : IConsumer<DocumentLinkedIntegrationEvent>
{
    private readonly IRepository<Documents.Models.Document, Guid> _documentRepository =
        uow.Repository<Documents.Models.Document, Guid>();

    private readonly IRepository<UploadSession, Guid> _uploadSessionRepository = uow.Repository<UploadSession, Guid>();

    public async Task Consume(ConsumeContext<DocumentLinkedIntegrationEvent> @event)
    {
        await uow.BeginTransactionAsync();


        if (@event.Message.SessionId.HasValue)
        {
            var uploadSession =
                await _uploadSessionRepository.GetByIdForUpdateAsync((Guid)@event.Message.SessionId,
                    @event.CancellationToken);
            if (uploadSession is not null)
            {
                uploadSession.Complete(dateTimeProvider.Now);
                logger.LogInformation("Completed upload session {SessionId}", @event.Message.SessionId);
            }
        }

        foreach (var documentLink in @event.Message.DocumentLinks)
        {
            var document =
                await _documentRepository.GetByIdForUpdateAsync(documentLink.DocumentId, @event.CancellationToken);

            if (document is null)
            {
                logger.LogError("Document {DocumentId} not found for linking", documentLink.DocumentId);
                continue;
            }

            if (documentLink.IsUnlink)
            {
                document.Unlink(dateTimeProvider.Now);
                logger.LogInformation("Unlinked document {DocumentId} from {EntityType}({EntityId})",
                    documentLink.DocumentId, documentLink.EntityType, documentLink.EntityId);
            }
            else
            {
                document.Link(dateTimeProvider.Now);
                logger.LogInformation("Linked document {DocumentId} to {EntityType}({EntityId})",
                    documentLink.DocumentId, documentLink.EntityType, documentLink.EntityId);
            }
        }

        await uow.SaveChangesAsync(@event.CancellationToken);
    }
}

public class DocumentLinkedIntegrationEventTest : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/DocumentLinkedIntegrationEventTest", async (Input request, IBus bus) =>
        {
            var @event = new DocumentLinkedIntegrationEvent
            {
                SessionId = request.SessionId is null ? null : Guid.Parse(request.SessionId),
                DocumentLinks = request.DocumentLinks
            };

            await bus.Publish(@event);

            return Results.Ok();
        });
    }
}

public record Input(
    string? SessionId,
    List<DocumentLink> DocumentLinks
);