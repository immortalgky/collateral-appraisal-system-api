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
    string Title,
    string? Notes,
    Guid? CommitteeId,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location);

public record CreateMeetingCommand(CreateMeetingRequest Request)
    : ICommand<CreateMeetingResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record CreateMeetingResponse(Guid Id, string Title, string Status, string? MeetingNo);

public class CreateMeetingCommandHandler(
    IMeetingRepository meetingRepository,
    ICommitteeRepository committeeRepository,
    IMeetingNoGenerator meetingNoGenerator,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateMeetingCommand, CreateMeetingResponse>
{
    private const string COMMITTEE_WITH_MEETING = "COMMITTEE_WITH_MEETING";

    public async Task<CreateMeetingResponse> Handle(CreateMeetingCommand command, CancellationToken ct)
    {
        // Generate meeting number first — available from birth.
        var now = dateTimeProvider.ApplicationNow;
        var meetingNo = await meetingNoGenerator.NextAsync(now, ct);

        // Parse "{seq}/{BE-year}" for storage
        var parts = meetingNo.Split('/');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var seq)
            || !int.TryParse(parts[1], out var beYear))
            throw new InvalidOperationException(
                $"Meeting number generator returned an unexpected format: '{meetingNo}'");

        var meeting = Meeting.Create(command.Request.Title, command.Request.Notes, meetingNo, seq, beYear);

        var committee = await committeeRepository.GetByCodeAsync(
                            COMMITTEE_WITH_MEETING, ct)
                        ?? throw new NotFoundException(
                            $"Committee {COMMITTEE_WITH_MEETING} not found");

        meeting.SnapshotCommittee(committee, meeting.MeetingNoSeq!.Value);

        if (command.Request.StartAt.HasValue && command.Request.EndAt.HasValue)
            meeting.SetSchedule(command.Request.StartAt.Value, command.Request.EndAt.Value, command.Request.Location, now);

        await meetingRepository.AddAsync(meeting, ct);
        return new CreateMeetingResponse(meeting.Id, meeting.Title, meeting.Status.ToString(), meeting.MeetingNo);
    }
}
