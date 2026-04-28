using Workflow.Domain.Committees;
using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.BulkCreateMeetings;

public class BulkCreateMeetingsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/meetings/bulk", async (
                BulkCreateMeetingsRequest request,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new BulkCreateMeetingsCommand(request), ct);
                return Results.Created("/meetings", result);
            })
            .WithName("BulkCreateMeetings")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<BulkCreateMeetingsResponse>(StatusCodes.Status201Created);
    }
}

public record BulkCreateMeetingsRequest(
    DateTime[] Dates,
    Guid? CommitteeId,
    string? DefaultTitle);

public record BulkCreateMeetingsCommand(BulkCreateMeetingsRequest Request)
    : ICommand<BulkCreateMeetingsResponse>, ITransactionalCommand<IWorkflowUnitOfWork>;

public record BulkCreateMeetingsResponse(Guid[] MeetingIds);

public class BulkCreateMeetingsCommandHandler(
    IMeetingRepository meetingRepository,
    ICommitteeRepository committeeRepository,
    IMeetingNoGenerator meetingNoGenerator,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<BulkCreateMeetingsCommand, BulkCreateMeetingsResponse>
{
    public async Task<BulkCreateMeetingsResponse> Handle(
        BulkCreateMeetingsCommand command, CancellationToken ct)
    {
        if (command.Request.Dates is null || command.Request.Dates.Length == 0)
            throw new ArgumentException("At least one date is required");

        Committee? committee = null;
        if (command.Request.CommitteeId.HasValue)
        {
            committee = await committeeRepository.GetByIdWithMembersAsync(
                command.Request.CommitteeId.Value, ct)
                ?? throw new NotFoundException(
                    $"Committee {command.Request.CommitteeId.Value} not found");
        }

        var now = dateTimeProvider.ApplicationNow;
        var meetingIds = new List<Guid>();

        foreach (var date in command.Request.Dates)
        {
            // Each meeting in the batch gets the next sequential number.
            var meetingNo = await meetingNoGenerator.NextAsync(now, ct);

            var parts = meetingNo.Split('/');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out var seq)
                || !int.TryParse(parts[1], out var beYear))
                throw new InvalidOperationException(
                    $"Meeting number generator returned an unexpected format: '{meetingNo}'");

            var title = !string.IsNullOrWhiteSpace(command.Request.DefaultTitle)
                ? command.Request.DefaultTitle
                : $"ประชุมพิจารณาราคา — {date:yyyy-MM-dd}";

            var meeting = Meeting.Create(title, notes: null, meetingNo, seq, beYear);

            if (committee is not null)
                meeting.SnapshotCommittee(committee, meeting.MeetingNoSeq!.Value);

            // Default schedule: 09:00–17:00 on the specified date
            var startAt = date.Date.AddHours(9);
            var endAt = date.Date.AddHours(17);
            meeting.SetSchedule(startAt, endAt, location: null, now);

            await meetingRepository.AddAsync(meeting, ct);
            meetingIds.Add(meeting.Id);
        }

        return new BulkCreateMeetingsResponse(meetingIds.ToArray());
    }
}
