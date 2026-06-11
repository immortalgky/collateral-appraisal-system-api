using FluentValidation;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.Documents;

public record LinkMeetingDocumentRequest(Guid DocumentId, string FileName);

public class LinkMeetingDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/documents", async (
                Guid id,
                LinkMeetingDocumentRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new LinkMeetingDocumentCommand(id, request.DocumentId, request.FileName);
                var result = await sender.Send(command, ct);
                return Results.Ok(result);
            })
            .WithName("LinkMeetingDocument")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<MeetingDocumentDto>();
    }
}

public record LinkMeetingDocumentCommand(Guid MeetingId, Guid DocumentId, string FileName)
    : ICommand<MeetingDocumentDto>, ITransactionalCommand<IWorkflowUnitOfWork>;

public class LinkMeetingDocumentCommandValidator : AbstractValidator<LinkMeetingDocumentCommand>
{
    public LinkMeetingDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
    }
}

public class LinkMeetingDocumentCommandHandler(
    IMeetingRepository meetingRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<LinkMeetingDocumentCommand, MeetingDocumentDto>
{
    public async Task<MeetingDocumentDto> Handle(
        LinkMeetingDocumentCommand command,
        CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        var userCode = currentUserService.UserCode
            ?? currentUserService.Username
            ?? "system";
        var now = dateTimeProvider.ApplicationNow;

        var data = new MeetingDocumentData(
            DocumentId: command.DocumentId,
            DocumentType: "Upload",
            FileName: command.FileName,
            Source: "Uploaded",
            CreatedBy: userCode,
            CreatedAt: now);

        var meetingDoc = meeting.AddDocument(data);

        // Publish link event so the Document module increments ReferenceCount.
        outbox.Publish(
            new DocumentLinkedIntegrationEventV2(
                RequestId: command.MeetingId,   // owner id — meeting id reuses the RequestId field
                DocumentId: command.DocumentId,
                DocumentType: "Upload"),
            correlationId: command.MeetingId.ToString());

        return new MeetingDocumentDto(
            meetingDoc.Id,
            command.DocumentId,
            command.FileName,
            "Upload",
            "Uploaded",
            userCode,
            now,
            FileSizeBytes: null,
            MimeType: null);
    }
}
