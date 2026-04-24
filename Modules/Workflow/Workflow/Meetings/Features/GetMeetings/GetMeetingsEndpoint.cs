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
            .RequireAuthorization("CommitteeMember");
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
    string? MeetingNo,
    DateTime? StartAt,
    DateTime? EndAt,
    string? Location,
    int ItemCount,
    DateTime? CutOffAt,
    DateTime? InvitationSentAt);

public class GetMeetingsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetMeetingsQuery, PaginatedResult<MeetingListItemDto>>
{
    /// <summary>
    /// Effective-status CASE expression:
    ///   InvitationSent + StartAt &lt;= now  → 'InProgress'  (computed, never persisted)
    ///   otherwise                           → persisted Status value
    ///
    /// Filter mapping (same SYSUTCDATETIME() anchor used by both projection and WHERE):
    ///   @Status = NULL          → all rows
    ///   @Status = 'InProgress'  → Status='InvitationSent' AND StartAt &lt;= now
    ///   @Status = 'InvitationSent' → Status='InvitationSent' AND (StartAt IS NULL OR StartAt > now)
    ///   any other @Status       → plain Status = @Status
    /// </summary>
    private const string Sql = """
                               SELECT
                                   m.Id,
                                   m.Title,
                                   CASE
                                       WHEN m.Status = 'InvitationSent' AND m.StartAt IS NOT NULL AND m.StartAt <= GETDATE()
                                           THEN 'InProgress'
                                       ELSE m.Status
                                   END AS Status,
                                   m.MeetingNo,
                                   m.StartAt,
                                   m.EndAt,
                                   m.Location,
                                   (SELECT COUNT(1) FROM workflow.MeetingItems i WHERE i.MeetingId = m.Id) AS ItemCount,
                                   m.CutOffAt,
                                   m.InvitationSentAt
                               FROM workflow.Meetings m
                               WHERE (
                                   @Status IS NULL
                                   OR (
                                       @Status = 'InProgress'
                                           AND m.Status = 'InvitationSent'
                                           AND m.StartAt IS NOT NULL
                                           AND m.StartAt <= GETDATE()
                                   )
                                   OR (
                                       @Status = 'InvitationSent'
                                           AND m.Status = 'InvitationSent'
                                           AND (m.StartAt IS NULL OR m.StartAt > GETDATE())
                                   )
                                   OR (
                                       @Status <> 'InProgress'
                                       AND @Status <> 'InvitationSent'
                                       AND m.Status = @Status
                                   )
                               )
                               """;

    public async Task<PaginatedResult<MeetingListItemDto>> Handle(
        GetMeetingsQuery query, CancellationToken cancellationToken)
    {
        var request = new PaginationRequest(query.PageNumber, query.PageSize);
        return await connectionFactory.QueryPaginatedAsync<MeetingListItemDto>(
            Sql,
            "m.StartAt, m.MeetingNoSeq",
            request,
            new { Status = query.Status });
    }
}