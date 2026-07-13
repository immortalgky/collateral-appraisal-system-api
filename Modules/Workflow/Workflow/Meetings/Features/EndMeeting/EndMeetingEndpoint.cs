using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.EndMeeting;

public class EndMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/{id:guid}/end", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new EndMeetingCommand(id), ct);
                return Results.NoContent();
            })
            .WithName("EndMeeting")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record EndMeetingCommand(Guid MeetingId)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class EndMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<EndMeetingCommand>
{
    public async Task<Unit> Handle(EndMeetingCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdWithItemsAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        var now = dateTimeProvider.ApplicationNow;

        // Friendly 409s for state conflicts (mirrors CutOff's handler-level ConflictException).
        // Wording uses the user-facing "in progress" vocabulary, not the raw InvitationSent enum.
        if (meeting.Status != MeetingStatus.InvitationSent)
            throw new ConflictException(
                $"Cannot end meeting: it is {meeting.Status}, not in progress.");

        if (!meeting.StartAt.HasValue || meeting.StartAt.Value > now)
            throw new ConflictException(
                "Cannot end meeting: it has not started yet. Cancel it instead.");

        var outstanding = meeting.Items
            .Where(i => i.Kind == MeetingItemKind.Decision && i.ItemDecision != ItemDecision.Released)
            .ToList();
        if (outstanding.Count > 0)
            throw new ConflictException(
                $"Cannot end meeting: {outstanding.Count} decision item(s) are still pending release or routed back.");

        meeting.EndNow(now);
        return Unit.Value;
    }
}
