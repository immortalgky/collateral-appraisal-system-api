using Document.Domain.UploadSessions.Model;
using Shared.Data;

namespace Document.EventHandlers;

public class DocumentLinkedIntegrationEventHandler(
    IDocumentUnitOfWork uow,
    IDateTimeProvider dateTimeProvider,
    ILogger<DocumentLinkedIntegrationEventHandler> logger)
    : IConsumer<DocumentLinkedIntegrationEventV2>
{
    private readonly IRepository<Domain.Documents.Models.Document, Guid> _documentRepository =
        uow.Repository<Domain.Documents.Models.Document, Guid>();

    public async Task Consume(ConsumeContext<DocumentLinkedIntegrationEventV2> @event)
    {
        var document =
            await _documentRepository.GetByIdAsync(@event.Message.DocumentId, @event.CancellationToken);

        if (document is null)
        {
            logger.LogError("Document {DocumentId} not found for linking", @event.Message.DocumentId);
            return;
        }

        document.Link(dateTimeProvider.Now);

        logger.LogInformation("Document {DocumentId} linked", @event.Message.DocumentId);

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
                SessionId = Guid.Parse(request.SessionId),
                DocumentLinks = request.DocumentLinks
            };

            await bus.Publish(@event);

            return Results.Ok();
        });
    }
}

public record Input(
    string SessionId,
    List<DocumentLink> DocumentLinks
);