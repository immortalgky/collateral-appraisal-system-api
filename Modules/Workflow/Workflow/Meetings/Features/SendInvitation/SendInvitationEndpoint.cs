using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.SendInvitation;

public class SendInvitationEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/send-invitation", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new SendInvitationCommand(id), ct);
                return Results.Ok(result);
            })
            .WithName("SendInvitation")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<SendInvitationResponse>();
    }
}

public record SendInvitationCommand(Guid MeetingId)
    : ICommand<SendInvitationResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record SendInvitationResponse(Guid MeetingId, string? MeetingNo, DateTime? InvitationSentAt);

public class SendInvitationCommandHandler(
    IMeetingRepository meetingRepository,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SendInvitationCommand, SendInvitationResponse>
{
    public async Task<SendInvitationResponse> Handle(SendInvitationCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        meeting.SendInvitation(dateTimeProvider.ApplicationNow);

        return new SendInvitationResponse(meeting.Id, meeting.MeetingNo, meeting.InvitationSentAt);
    }
}
