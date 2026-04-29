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
    IMeetingConfigurationRepository meetingConfigRepository,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<BulkCreateMeetingsCommand, BulkCreateMeetingsResponse>
{
    private const string COMMITTEE_WITH_MEETING = "COMMITTEE_WITH_MEETING";

    public async Task<BulkCreateMeetingsResponse> Handle(
        BulkCreateMeetingsCommand command, CancellationToken ct)
    {
        if (command.Request.Dates is null || command.Request.Dates.Length == 0)
            throw new ArgumentException("At least one date is required");

        Committee committee;
        if (command.Request.CommitteeId.HasValue)
        {
            committee = await committeeRepository.GetByIdWithMembersAsync(
                command.Request.CommitteeId.Value, ct)
                ?? throw new NotFoundException(
                    $"Committee {command.Request.CommitteeId.Value} not found");
        }
        else
        {
            committee = await committeeRepository.GetByCodeAsync(COMMITTEE_WITH_MEETING, ct)
                ?? throw new NotFoundException($"Committee {COMMITTEE_WITH_MEETING} not found");
        }

        var now = dateTimeProvider.ApplicationNow;
        var defaults = await meetingConfigRepository.GetMeetingDefaultsAsync(ct);
        var meetingIds = new List<Guid>();

        // Pre-compute schedule windows so we can validate intra-batch overlap and clashes
        // with existing meetings before allocating any meeting numbers.
        var windows = command.Request.Dates
            .Select(d => (Date: d, StartAt: d.Date.AddHours(9), EndAt: d.Date.AddHours(17)))
            .OrderBy(w => w.StartAt)
            .ToList();

        for (var i = 1; i < windows.Count; i++)
        {
            if (windows[i].StartAt < windows[i - 1].EndAt)
                throw new ConflictException(
                    $"Bulk request contains overlapping dates: {windows[i - 1].Date:yyyy-MM-dd} and {windows[i].Date:yyyy-MM-dd}");
        }

        foreach (var w in windows)
        {
            var conflict = await meetingRepository.GetOverlappingMeetingAsync(w.StartAt, w.EndAt, ct: ct);
            if (conflict is not null)
                throw new ConflictException(
                    $"Meeting on {w.Date:yyyy-MM-dd} overlaps with existing meeting {conflict.MeetingNo} ({conflict.StartAt:yyyy-MM-dd HH:mm}–{conflict.EndAt:HH:mm})");
        }

        // Meeting numbers are issued sequentially during the batch — the earliest window (which
        // gets the lowest new seq) must not predate any existing scheduled meeting.
        var latest = await meetingRepository.GetLatestScheduledMeetingAsync(ct);
        if (latest is not null && windows[0].StartAt < latest.StartAt!.Value)
            throw new ConflictException(
                $"Bulk meeting on {windows[0].Date:yyyy-MM-dd} cannot be scheduled before existing meeting {latest.MeetingNo} ({latest.StartAt:yyyy-MM-dd HH:mm})");

        foreach (var w in windows)
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
                : defaults.TitleTemplate.Replace("{meetingNo}", meetingNo);

            var meeting = Meeting.Create(title, notes: null, meetingNo, seq, beYear);

            meeting.SnapshotCommittee(committee, meeting.MeetingNoSeq!.Value);

            meeting.SetSchedule(w.StartAt, w.EndAt, defaults.Location, now);

            meeting.SetAgenda(
                from: defaults.AgendaFromText,
                to: defaults.AgendaToText,
                certifyMinutes: null,
                chairmanInformed: null,
                others: null,
                now);

            await meetingRepository.AddAsync(meeting, ct);
            meetingIds.Add(meeting.Id);
        }

        return new BulkCreateMeetingsResponse(meetingIds.ToArray());
    }
}
