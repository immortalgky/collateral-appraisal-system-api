using FluentValidation;
using Shared.Data.Outbox;
using Shared.Messaging.Events;
using Workflow.Data;
using Workflow.Meetings.Application;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.SendInvitation;

public record SendInvitationRequest(
    string From,
    string? To,
    string? Cc,
    string? Bcc,
    string Subject,
    string? Content,
    string[]? Attachments);

public class SendInvitationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/send-invitation", async (
                Guid id,
                SendInvitationRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var command = new SendInvitationCommand(
                    id,
                    request.From,
                    request.To,
                    request.Cc,
                    request.Bcc,
                    request.Subject,
                    request.Content,
                    request.Attachments);
                var result = await sender.Send(command, ct);
                return Results.Ok(result);
            })
            .WithName("SendInvitation")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<SendInvitationResponse>();
    }
}

public record SendInvitationCommand(
    Guid MeetingId,
    string From,
    string? To,
    string? Cc,
    string? Bcc,
    string Subject,
    string? Content,
    string[]? Attachments)
    : ICommand<SendInvitationResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public class SendInvitationCommandValidator : AbstractValidator<SendInvitationCommand>
{
    public SendInvitationCommandValidator()
    {
        RuleFor(x => x.From).NotEmpty().MaximumLength(500);
        // To is optional — recipients may all be in Cc/Bcc (bank hides recipients).
        RuleFor(x => x.To).MaximumLength(500).When(x => x.To is not null);
        RuleFor(x => x.Cc).MaximumLength(500).When(x => x.Cc is not null);
        RuleFor(x => x.Bcc).MaximumLength(500).When(x => x.Bcc is not null);
        RuleFor(x => x)
            .Must(c => !string.IsNullOrWhiteSpace(c.To)
                       || !string.IsNullOrWhiteSpace(c.Cc)
                       || !string.IsNullOrWhiteSpace(c.Bcc))
            .WithMessage("At least one recipient (To, Cc, or Bcc) is required.");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).MaximumLength(4000).When(x => x.Content is not null);
        RuleForEach(x => x.Attachments).MaximumLength(200).When(x => x.Attachments is not null);
        RuleFor(x => x.Attachments).Must(a => a is null || a.Length <= 10)
            .WithMessage("Maximum 10 attachments allowed.");
        RuleFor(x => x.Attachments).Must(a => a is not null && a.Any(d => !string.IsNullOrWhiteSpace(d)))
            .WithMessage("At least one attachment is required.");
    }
}

public record SendInvitationResponse(Guid MeetingId, string? MeetingNo, DateTime? InvitationSentAt);

public class SendInvitationCommandHandler(
    IMeetingRepository meetingRepository,
    IDateTimeProvider dateTimeProvider,
    WorkflowDbContext dbContext,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<SendInvitationCommand, SendInvitationResponse>
{
    public async Task<SendInvitationResponse> Handle(SendInvitationCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        // Build the set of user-picked document ids.
        var pickedDocIds = (command.Attachments ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .ToList();

        // Build attachment refs — all as ("document", id) so the email consumer
        // always resolves a real persisted document (no async report rendering after send).
        // The email carries exactly what the user attached; nothing is auto-generated here.
        var attachmentRefs = pickedDocIds
            .Select(docId => new EmailAttachmentRefData("document", docId))
            .ToList();

        meeting.SendInvitation(dateTimeProvider.ApplicationNow);

        // Audit log records every document actually attached.
        var attachedDocumentIds = attachmentRefs.Select(r => r.Value).ToArray();

        var emailLog = MeetingInvitationEmail.Create(
            command.MeetingId,
            command.From,
            command.To,
            command.Cc,
            command.Bcc,
            command.Subject,
            command.Content,
            attachedDocumentIds);
        dbContext.MeetingInvitationEmails.Add(emailLog);

        outbox.Publish(new MeetingInvitationEmailIntegrationEvent
        {
            MeetingId = command.MeetingId,
            To = command.To,
            Cc = command.Cc,
            Bcc = command.Bcc,
            Subject = command.Subject,
            Content = command.Content,
            AttachmentRefs = attachmentRefs
        }, correlationId: command.MeetingId.ToString());

        return new SendInvitationResponse(meeting.Id, meeting.MeetingNo, meeting.InvitationSentAt);
    }
}
