using Workflow.Data.Repository;
using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.CreateMeeting;

public class CreateMeetingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings", async (
                CreateMeetingRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateMeetingCommand(request), ct);
                return Results.Created($"/meetings/{result.Id}", result);
            })
            .WithName("CreateMeeting")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<CreateMeetingResponse>(StatusCodes.Status201Created);
    }
}

public record CreateMeetingRequest(
    Guid? CommitteeId,
    DateTime? StartAt,
    DateTime? EndAt);

public record CreateMeetingCommand(CreateMeetingRequest Request)
    : ICommand<CreateMeetingResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CreateMeetingResponse(Guid Id, string Title, string Status, string? MeetingNo);

public class CreateMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    ICommitteeRepository committeeRepository,
    IMeetingNoGenerator meetingNoGenerator,
    IMeetingConfigurationRepository meetingConfigRepository,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateMeetingCommand, CreateMeetingResponse>
{
    private const string COMMITTEE_WITH_MEETING = "COMMITTEE_WITH_MEETING";

    public async Task<CreateMeetingResponse> Handle(CreateMeetingCommand command, CancellationToken ct)
    {
        var now = dateTimeProvider.ApplicationNow;

        if (command.Request.StartAt.HasValue && command.Request.EndAt.HasValue)
        {
            if (command.Request.EndAt.Value <= command.Request.StartAt.Value)
                throw new BadRequestException("EndAt must be after StartAt");

            var conflict = await meetingRepository.GetOverlappingMeetingAsync(
                command.Request.StartAt.Value, command.Request.EndAt.Value, ct: ct);
            if (conflict is not null)
                throw new ConflictException(
                    $"Meeting schedule overlaps with existing meeting {conflict.MeetingNo} ({conflict.StartAt:yyyy-MM-dd HH:mm}–{conflict.EndAt:HH:mm})");

            // The next meeting number must not be scheduled earlier than any previously-numbered
            // meeting — meeting numbers are issued sequentially and must follow chronological order.
            var latest = await meetingRepository.GetLatestScheduledMeetingAsync(ct);
            if (latest is not null && command.Request.StartAt.Value < latest.StartAt!.Value)
                throw new ConflictException(
                    $"New meeting cannot be scheduled before existing meeting {latest.MeetingNo} ({latest.StartAt:yyyy-MM-dd HH:mm})");
        }

        var defaults = await meetingConfigRepository.GetMeetingDefaultsAsync(ct);
        var meetingNo = await meetingNoGenerator.NextAsync(now, ct);

        // Parse "{seq}/{BE-year}" for storage
        var parts = meetingNo.Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var seq)
            || !int.TryParse(parts[1], out var beYear))
            throw new InvalidOperationException(
                $"Meeting number generator returned an unexpected format: '{meetingNo}'");

        var title = defaults.TitleTemplate.Replace("{meetingNo}", meetingNo);
        var meeting = Meeting.Create(title, notes: null, meetingNo, seq, beYear);

        var committee = await committeeRepository.GetByCodeAsync(COMMITTEE_WITH_MEETING, ct)
                        ?? throw new NotFoundException($"Committee {COMMITTEE_WITH_MEETING} not found");

        meeting.SnapshotCommittee(committee, meeting.MeetingNoSeq!.Value);

        if (command.Request.StartAt.HasValue && command.Request.EndAt.HasValue)
            meeting.SetSchedule(
                command.Request.StartAt.Value,
                command.Request.EndAt.Value,
                defaults.Location,
                now);

        meeting.SetAgenda(
            from: defaults.AgendaFromText,
            to: defaults.AgendaToText,
            certifyMinutes: null,
            chairmanInformed: null,
            others: null,
            now);

        await meetingRepository.AddAsync(meeting, ct);
        return new CreateMeetingResponse(meeting.Id, meeting.Title, meeting.Status.ToString(), meeting.MeetingNo);
    }
}
