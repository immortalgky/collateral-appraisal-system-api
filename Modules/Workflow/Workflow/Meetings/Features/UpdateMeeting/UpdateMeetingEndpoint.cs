using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.UpdateMeeting;

public class UpdateMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/meetings/{id:guid}", async (
                Guid id,
                UpdateMeetingRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                await sender.Send(new UpdateMeetingCommand(id, request), ct);
                return Results.NoContent();
            })
            .WithName("UpdateMeeting")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces(StatusCodes.Status204NoContent);
    }
}

public record UpdateMeetingRequest(
    string Title,
    string? Location,
    string? FromText,
    string? ToText,
    DateTime? StartAt,
    DateTime? EndAt);

public record UpdateMeetingCommand(Guid MeetingId, UpdateMeetingRequest Request)
    : ICommand, ITransactionalCommand<IWorkflowUnitOfWork>;

public class UpdateMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateMeetingCommand>
{
    public async Task<Unit> Handle(UpdateMeetingCommand command, CancellationToken ct)
    {
        var meeting = await meetingRepository.GetByIdAsync(command.MeetingId, ct)
            ?? throw new NotFoundException($"Meeting {command.MeetingId} not found");

        var now = dateTimeProvider.ApplicationNow;

        // Notes are no longer editable from this endpoint — preserve whatever's persisted.
        meeting.UpdateDetails(command.Request.Title, command.Request.Location, meeting.Notes, now);

        if (command.Request.StartAt.HasValue && command.Request.EndAt.HasValue)
        {
            var startAt = command.Request.StartAt.Value;
            var endAt = command.Request.EndAt.Value;

            if (endAt <= startAt)
                throw new BadRequestException("EndAt must be after StartAt");

            var conflict = await meetingRepository.GetOverlappingMeetingAsync(
                startAt, endAt, excludeMeetingId: meeting.Id, ct: ct);
            if (conflict is not null)
                throw new ConflictException(
                    $"Meeting schedule overlaps with existing meeting {conflict.MeetingNo} ({conflict.StartAt:yyyy-MM-dd HH:mm}–{conflict.EndAt:HH:mm})");

            // Preserve chronological order of meeting numbers: this meeting must sit between any
            // lower-numbered and higher-numbered scheduled meeting.
            if (meeting.MeetingNoYear.HasValue && meeting.MeetingNoSeq.HasValue)
            {
                var lower = await meetingRepository.GetLatestScheduledBeforeNumberAsync(
                    meeting.MeetingNoYear.Value, meeting.MeetingNoSeq.Value, ct);
                if (lower is not null && startAt < lower.StartAt!.Value)
                    throw new ConflictException(
                        $"Meeting cannot be scheduled before lower-numbered meeting {lower.MeetingNo} ({lower.StartAt:yyyy-MM-dd HH:mm})");

                var higher = await meetingRepository.GetEarliestScheduledAfterNumberAsync(
                    meeting.MeetingNoYear.Value, meeting.MeetingNoSeq.Value, ct);
                if (higher is not null && startAt > higher.StartAt!.Value)
                    throw new ConflictException(
                        $"Meeting cannot be scheduled after higher-numbered meeting {higher.MeetingNo} ({higher.StartAt:yyyy-MM-dd HH:mm})");
            }

            meeting.SetSchedule(startAt, endAt, command.Request.Location, now);
        }

        // From/To moved here from the Agenda form. Other agenda fields are preserved.
        meeting.SetAgenda(
            command.Request.FromText,
            command.Request.ToText,
            meeting.AgendaCertifyMinutes,
            meeting.AgendaChairmanInformed,
            meeting.AgendaOthers,
            now);

        return Unit.Value;
    }
}
