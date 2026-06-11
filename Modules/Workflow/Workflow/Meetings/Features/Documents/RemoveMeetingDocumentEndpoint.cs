using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.Documents;

public class RemoveMeetingDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/meetings/{id:guid}/documents/{documentId:guid}", async (
                Guid id,
                Guid documentId,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new RemoveMeetingDocumentCommand(id, documentId), ct);
                return Results.NoContent();
            })
            .WithName("RemoveMeetingDocument")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record RemoveMeetingDocumentCommand(Guid MeetingId, Guid DocumentId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class RemoveMeetingDocumentCommandHandler(
    IMeetingRepository meetingRepository,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<RemoveMeetingDocumentCommand>
{
    public async Task<Unit> Handle(RemoveMeetingDocumentCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.RemoveDocument(command.DocumentId);

        // Publish unlink event so the Document module decrements ReferenceCount.
        outbox.Publish(
            new DocumentUnlinkedIntegrationEvent(
                RequestId: command.MeetingId,   // owner id — meeting id reuses the RequestId field
                DocumentId: command.DocumentId),
            correlationId: command.MeetingId.ToString());

        return Unit.Value;
    }
}
