using Workflow.Meetings.Domain;

namespace Workflow.Meetings.Features.GetMeetingDetail;

public class GetMeetingDetailEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/meetings/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new GetMeetingDetailQuery(id), ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetMeetingDetail")
            .WithTags("Meetings")
            .RequireAuthorization();
    }
}

public record GetMeetingDetailQuery(Guid Id) : IQuery<MeetingDetailDto>;

public record MeetingDetailDto(
    Guid Id,
    string Title,
    string Status,
    DateTime? ScheduledAt,
    string? Location,
    string? Notes,
    string? CancelReason,
    DateTime? EndedAt,
    DateTime? CancelledAt,
    List<MeetingItemDto> Items);

public record MeetingItemDto(
    Guid Id,
    Guid AppraisalId,
    string? AppraisalNo,
    decimal FacilityLimit,
    Guid WorkflowInstanceId,
    string ActivityId,
    DateTime AddedAt);

public class GetMeetingDetailQueryHandler(IMeetingRepository meetingRepository)
    : IQueryHandler<GetMeetingDetailQuery, MeetingDetailDto>
{
    public async Task<MeetingDetailDto> Handle(GetMeetingDetailQuery query, CancellationToken cancellationToken)
    {
        var meeting = await meetingRepository.GetByIdWithItemsAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException($"Meeting {query.Id} not found");

        return new MeetingDetailDto(
            meeting.Id,
            meeting.Title,
            meeting.Status.ToString(),
            meeting.ScheduledAt,
            meeting.Location,
            meeting.Notes,
            meeting.CancelReason,
            meeting.EndedAt,
            meeting.CancelledAt,
            meeting.Items.Select(i => new MeetingItemDto(
                i.Id,
                i.AppraisalId,
                i.AppraisalNo,
                i.FacilityLimit,
                i.WorkflowInstanceId,
                i.ActivityId,
                i.AddedAt)).ToList());
    }
}
