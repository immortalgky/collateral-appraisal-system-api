using Shared.Data;
using Shared.Pagination;

namespace Workflow.Meetings.Features.GetMeetings;

public class GetMeetingsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/meetings", async (
                [AsParameters] GetMeetingsQuery query,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(query, ct);
                return Results.Ok(result);
            })
            .WithName("GetMeetings")
            .WithTags("Meetings")
            .RequireAuthorization();
    }
}

public record GetMeetingsQuery(
    string? Status = null,
    int PageNumber = 0,
    int PageSize = 20) : IQuery<PaginatedResult<MeetingListItemDto>>;

public record MeetingListItemDto(
    Guid Id,
    string Title,
    string Status,
    DateTime? ScheduledAt,
    string? Location,
    int ItemCount);

public class GetMeetingsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetMeetingsQuery, PaginatedResult<MeetingListItemDto>>
{
    private const string Sql = """
        SELECT
            m.Id,
            m.Title,
            m.Status,
            m.ScheduledAt,
            m.Location,
            (SELECT COUNT(1) FROM workflow.MeetingItems i WHERE i.MeetingId = m.Id) AS ItemCount
        FROM workflow.Meetings m
        WHERE (@Status IS NULL OR m.Status = @Status)
        """;

    public async Task<PaginatedResult<MeetingListItemDto>> Handle(
        GetMeetingsQuery query, CancellationToken cancellationToken)
    {
        var request = new PaginationRequest(query.PageNumber, query.PageSize);
        return await connectionFactory.QueryPaginatedAsync<MeetingListItemDto>(
            Sql,
            orderBy: "ScheduledAt DESC, Id DESC",
            request,
            param: new { Status = query.Status });
    }
}
