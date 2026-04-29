using Shared.Data;
using Shared.Pagination;
using Shared.Time;

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
    bool? IsHistory = null,
    string? Search = null,
    string? MeetingNo = null,
    string? CustomerName = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
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

public class GetMeetingsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetMeetingsQuery, PaginatedResult<MeetingListItemDto>>
{
    /// <summary>
    /// Effective-status CASE expression:
    ///   InvitationSent + StartAt &lt;= now  → 'InProgress'  (computed, never persisted)
    ///   otherwise                           → persisted Status value
    ///
    /// "Now" is the application-time anchor (<see cref="IDateTimeProvider.ApplicationNow"/>),
    /// passed in as the @Now parameter so projection and WHERE share the same value and so
    /// the comparison happens in application time (matching how StartAt is persisted).
    ///
    /// Filter mapping:
    ///   @Status = NULL          → all rows
    ///   @Status = 'InProgress'  → Status='InvitationSent' AND StartAt &lt;= @Now
    ///   @Status = 'InvitationSent' → Status='InvitationSent' AND (StartAt IS NULL OR StartAt > @Now)
    ///   any other @Status       → plain Status = @Status
    /// </summary>
    private const string Sql = """
                               SELECT
                                   m.Id,
                                   m.Title,
                                   CASE
                                       WHEN m.Status = 'InvitationSent' AND m.StartAt IS NOT NULL AND m.StartAt <= @Now
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
                                           AND m.StartAt <= @Now
                                   )
                                   OR (
                                       @Status = 'InvitationSent'
                                           AND m.Status = 'InvitationSent'
                                           AND (m.StartAt IS NULL OR m.StartAt > @Now)
                                   )
                                   OR (
                                       @Status <> 'InProgress'
                                       AND @Status <> 'InvitationSent'
                                       AND m.Status = @Status
                                   )
                               )
                               AND (
                                   @IsHistory IS NULL
                                   OR (@IsHistory = 1 AND m.Status IN ('Ended', 'Cancelled'))
                                   OR (@IsHistory = 0 AND m.Status NOT IN ('Ended', 'Cancelled'))
                               )
                               AND (@MeetingNo IS NULL OR m.MeetingNo LIKE '%' + @MeetingNo + '%')
                               AND (@FromDate IS NULL OR m.StartAt >= @FromDate)
                               AND (@ToDate IS NULL OR m.StartAt < DATEADD(DAY, 1, @ToDate))
                               AND (
                                   @CustomerName IS NULL
                                   OR EXISTS (
                                       SELECT 1
                                       FROM workflow.MeetingItems mi
                                       INNER JOIN [appraisal].[Appraisals] a ON a.Id = mi.AppraisalId
                                       INNER JOIN [request].[RequestCustomers] rc ON rc.RequestId = a.RequestId
                                       WHERE mi.MeetingId = m.Id
                                         AND rc.Name LIKE '%' + @CustomerName + '%'
                                   )
                               )
                               AND (
                                   @Search IS NULL
                                   OR m.MeetingNo LIKE '%' + @Search + '%'
                                   OR EXISTS (
                                       SELECT 1
                                       FROM workflow.MeetingItems mi
                                       INNER JOIN [appraisal].[Appraisals] a ON a.Id = mi.AppraisalId
                                       INNER JOIN [request].[RequestCustomers] rc ON rc.RequestId = a.RequestId
                                       WHERE mi.MeetingId = m.Id
                                         AND rc.Name LIKE '%' + @Search + '%'
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
            new
            {
                Now = dateTimeProvider.ApplicationNow,
                Status = query.Status,
                IsHistory = query.IsHistory,
                Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
                MeetingNo = string.IsNullOrWhiteSpace(query.MeetingNo) ? null : query.MeetingNo.Trim(),
                CustomerName = string.IsNullOrWhiteSpace(query.CustomerName) ? null : query.CustomerName.Trim(),
                FromDate = query.FromDate,
                ToDate = query.ToDate,
            });
    }
}