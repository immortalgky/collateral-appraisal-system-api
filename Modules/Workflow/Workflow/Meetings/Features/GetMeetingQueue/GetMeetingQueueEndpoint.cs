using Shared.Data;
using Shared.Pagination;

namespace Workflow.Meetings.Features.GetMeetingQueue;

public class GetMeetingQueueEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/meetings/queue", async (
                [AsParameters] GetMeetingQueueQuery query,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(query, ct);
                return Results.Ok(result);
            })
            .WithName("GetMeetingQueue")
            .WithTags("Meetings")
            .RequireAuthorization();
    }
}

public record GetMeetingQueueQuery(
    string? Status = "Queued",
    int PageNumber = 0,
    int PageSize = 20) : IQuery<PaginatedResult<MeetingQueueItemDto>>;

public record MeetingQueueItemDto(
    Guid Id,
    Guid AppraisalId,
    string? AppraisalNo,
    decimal FacilityLimit,
    Guid WorkflowInstanceId,
    string ActivityId,
    Guid? MeetingId,
    string Status,
    DateTime EnqueuedAt);

public class GetMeetingQueueQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetMeetingQueueQuery, PaginatedResult<MeetingQueueItemDto>>
{
    private const string Sql = """
        SELECT
            q.Id,
            q.AppraisalId,
            q.AppraisalNo,
            q.FacilityLimit,
            q.WorkflowInstanceId,
            q.ActivityId,
            q.MeetingId,
            q.Status,
            q.EnqueuedAt
        FROM workflow.MeetingQueueItems q
        WHERE (@Status IS NULL OR q.Status = @Status)
        """;

    public async Task<PaginatedResult<MeetingQueueItemDto>> Handle(
        GetMeetingQueueQuery query, CancellationToken cancellationToken)
    {
        var request = new PaginationRequest(query.PageNumber, query.PageSize);
        return await connectionFactory.QueryPaginatedAsync<MeetingQueueItemDto>(
            Sql,
            orderBy: "EnqueuedAt ASC",
            request,
            param: new { Status = query.Status });
    }
}
