using FluentValidation;
using Workflow.Data;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.SendInvitation;

public record SendInvitationRequest(
    string From,
    string To,
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
    string To,
    string Subject,
    string? Content,
    string[]? Attachments)
    : ICommand<SendInvitationResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public class SendInvitationCommandValidator : AbstractValidator<SendInvitationCommand>
{
    public SendInvitationCommandValidator()
    {
        RuleFor(x => x.From).NotEmpty().MaximumLength(500);
        RuleFor(x => x.To).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).MaximumLength(4000).When(x => x.Content is not null);
        RuleForEach(x => x.Attachments).MaximumLength(200).When(x => x.Attachments is not null);
        RuleFor(x => x.Attachments).Must(a => a is null || a.Length <= 10)
            .WithMessage("Maximum 10 attachments allowed.");
    }
}

public record SendInvitationResponse(Guid MeetingId, string? MeetingNo, DateTime? InvitationSentAt);

public class SendInvitationCommandHandler(
    IMeetingRepository meetingRepository,
    IDateTimeProvider dateTimeProvider,
    WorkflowDbContext dbContext)
    : ICommandHandler<SendInvitationCommand, SendInvitationResponse>
{
    public async Task<SendInvitationResponse> Handle(SendInvitationCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.SendInvitation(dateTimeProvider.ApplicationNow);

        var emailLog = MeetingInvitationEmail.Create(
            command.MeetingId,
            command.From,
            command.To,
            command.Subject,
            command.Content,
            command.Attachments);
        dbContext.MeetingInvitationEmails.Add(emailLog);

        return new SendInvitationResponse(meeting.Id, meeting.MeetingNo, meeting.InvitationSentAt);
    }
}
