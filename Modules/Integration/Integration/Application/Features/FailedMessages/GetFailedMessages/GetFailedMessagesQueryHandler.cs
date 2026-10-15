using Dapper;
using Integration.FailedMessages;
using Shared.CQRS;
using Shared.Data;
using Shared.Pagination;
using Shared.Time;

namespace Integration.Application.Features.FailedMessages.GetFailedMessages;

public class GetFailedMessagesQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetFailedMessagesQuery, PaginatedResult<FailedMessageListDto>>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S2077:Formatting SQL queries is security-sensitive",
        Justification =
            "{where} is assembled only from the fixed predicate literals in this method (each one a column " +
            "compared to an @parameter), joined with AND — no request value is ever concatenated into it. " +
            "Status, Queue, Node, ExceptionType and the LIKE-escaped Search term are all bound as " +
            "DynamicParameters, and the table name is a constant.")]
    public async Task<PaginatedResult<FailedMessageListDto>> Handle(
        GetFailedMessagesQuery request, CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.Equals(request.Status, "All", StringComparison.Ordinal))
        {
            conditions.Add("f.Status = @Status");
            parameters.Add("Status", request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Queue))
        {
            conditions.Add("f.SourceQueue = @Queue");
            parameters.Add("Queue", request.Queue);
        }

        if (!string.IsNullOrWhiteSpace(request.Node))
        {
            conditions.Add("f.Node = @Node");
            parameters.Add("Node", request.Node);
        }

        if (!string.IsNullOrWhiteSpace(request.ExceptionType))
        {
            conditions.Add("f.ExceptionType = @ExceptionType");
            parameters.Add("ExceptionType", request.ExceptionType);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // The term is LIKE-escaped so a literal %, _, or [ in the search box matches literally,
            // not as a wildcard, and is always bound as a parameter.
            conditions.Add(
                "(CAST(f.MessageId AS NVARCHAR(36)) LIKE @Search ESCAPE '\\' OR f.ExceptionMessage LIKE @Search ESCAPE '\\' OR f.RefNumber LIKE @Search ESCAPE '\\')");
            // LikePattern.Escape is byte-for-byte the same escaping SqlLikeEscaper
            // duplicated (%, _, [, and \ itself, in the same order, same ESCAPE '\' convention).
            parameters.Add("Search", $"%{LikePattern.Escape(request.Search)}%");
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT
                f.Id, f.Node, f.SourceQueue, f.Kind, f.Status, f.MessageId, f.MessageType, f.ConsumerType,
                f.ExceptionType, f.ExceptionMessage, f.RetryCount, f.FaultedAt, f.CollectedAt,
                f.RefType, f.RefId, f.RefNumber, f.ActionBy, f.ActionAt,
                CASE WHEN f.MessageId IS NULL THEN 0
                     ELSE (SELECT COUNT(*) FROM integration.FailedMessages s
                           WHERE s.MessageId = f.MessageId AND s.Id <> f.Id)
                END AS SiblingCount
            FROM integration.FailedMessages f
            {where}
            """;

        var paginationRequest = new PaginationRequest(request.PageNumber - 1, request.PageSize);
        // FaultedAt alone is not unique (a batch can fault in the same second) — without the Id
        // tie-breaker, OFFSET/FETCH can repeat or skip rows across pages.
        var raw = await sqlConnectionFactory.QueryPaginatedAsync<FailedMessageRow>(
            sql, "f.FaultedAt DESC, f.Id DESC", paginationRequest, parameters);

        // Echo the caller's 1-based pageNumber back (QueryPaginatedAsync works in 0-based terms).
        var now = dateTimeProvider.ApplicationNow;
        var items = raw.Items.Select(r => new FailedMessageListDto(
            r.Id, r.Node, r.SourceQueue, r.Kind, r.Status, r.MessageId, r.MessageType, r.ConsumerType,
            r.ExceptionType, r.ExceptionMessage, r.RetryCount, r.FaultedAt, r.CollectedAt,
            r.RefType, r.RefId, r.RefNumber, r.ActionBy, r.ActionAt,
            FailedMessageRetryPolicy.RetryAvailableAtOrNull(r.Status, r.FaultedAt, r.CollectedAt, now),
            OrderedEndpoints.IsOrdered(r.SourceQueue), ExceptionTypeClassifier.IsNonTransient(r.ExceptionType),
            r.SiblingCount)).ToList();

        return new PaginatedResult<FailedMessageListDto>(items, raw.Count, request.PageNumber, request.PageSize);
    }

    private record FailedMessageRow(
        Guid Id, string Node, string SourceQueue, string Kind, string Status, Guid? MessageId, string MessageType,
        string? ConsumerType, string ExceptionType, string ExceptionMessage, int RetryCount, DateTime FaultedAt,
        DateTime CollectedAt, string? RefType, Guid? RefId, string? RefNumber, string? ActionBy, DateTime? ActionAt,
        int SiblingCount);
}
