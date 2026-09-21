using System.Data;
using Dapper;

namespace Auth.Application.Features.AuditLog.GetAuditLogs;

public class GetAuditLogsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetAuditLogsQuery, GetAuditLogsResult>
{
    public async Task<GetAuditLogsResult> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var connection = sqlConnectionFactory.GetOpenConnection();

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        await AddHiddenAccountExclusionsAsync(connection, conditions, parameters);

        if (request.EntityType.HasValue)
        {
            conditions.Add("EntityType = @EntityType");
            parameters.Add("EntityType", request.EntityType.Value.ToString());
        }

        if (request.EntityId.HasValue)
        {
            conditions.Add("EntityId = @EntityId");
            parameters.Add("EntityId", request.EntityId.Value);
        }

        if (request.ActorUserId.HasValue)
        {
            conditions.Add("ActorUserId = @ActorUserId");
            parameters.Add("ActorUserId", request.ActorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ActorName))
        {
            conditions.Add("ActorName LIKE @ActorName ESCAPE '\\'");
            parameters.Add("ActorName", "%" + EscapeLike(request.ActorName.Trim()) + "%");
        }

        if (request.From.HasValue)
        {
            conditions.Add("OccurredAt >= @From");
            parameters.Add("From", request.From.Value);
        }

        if (request.To.HasValue)
        {
            conditions.Add("OccurredAt <= @To");
            parameters.Add("To", request.To.Value);
        }

        if (request.Action.HasValue)
        {
            conditions.Add("[Action] = @Action");
            parameters.Add("Action", request.Action.Value.ToString());
        }

        var where = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : "";

        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var pageNumber = request.PageNumber;

        const string table = "auth.AuthAuditLogs";
        const string listColumns =
            "Id, OccurredAt, ActorUserId, ActorName, [Action], EntityType, EntityId, EntityName, ChangesJson, IpAddress";

        var offset = pageNumber * pageSize;
        var dataSql =
            $"SELECT {listColumns} FROM {table} {where} " +
            $"ORDER BY OccurredAt DESC OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        var countSql = $"SELECT COUNT(Id) FROM {table} {where}";

        using var multi = await connection.QueryMultipleAsync(
            countSql + "; " + dataSql,
            parameters);

        var totalCount = await multi.ReadFirstOrDefaultAsync<int>();
        var items = (await multi.ReadAsync<AuditLogItemDto>()).ToList();

        return new GetAuditLogsResult(items, totalCount, pageNumber, pageSize);
    }
    private static string EscapeLike(string input) =>
    input.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    /// <summary>
    /// Keeps technical accounts (break-glass admin, service accounts) out of the audit log, both as the
    /// actor of a change and as its subject, so they stay invisible the same way they are in the Access
    /// Report and the user list. No query flag brings them back.
    /// </summary>
    /// <remarks>
    /// Two steps on purpose. The set of hidden accounts is tiny, so reading it first and passing ids and
    /// names as list parameters keeps the audit query a plain seek. Expressing this as one correlated
    /// NOT EXISTS would OR together Id, NormalizedUserName and EntityId, which no index on AspNetUsers
    /// can serve — SQL Server would scan the whole user table once per audit row.
    /// The actor is recorded twice (a nullable Guid and a username string) and either can be the only one
    /// present: login events carry a name with no id, so both columns have to be excluded.
    ///
    /// Accepted consequence, decided by the bank rather than overlooked: this also suppresses the
    /// LoginFailed rows for a hidden account, including attempts by someone who is not the account
    /// holder. A brute-force run against the hidden admin leaves no trace on this screen. The rows are
    /// still written to auth.AuthAuditLogs and are readable in the database; only this read hides them.
    /// Narrow the exclusion (for instance by letting AuditAction.LoginFailed through) if that trade is
    /// ever revisited.
    /// </remarks>
    private static async Task AddHiddenAccountExclusionsAsync(
        IDbConnection connection,
        List<string> conditions,
        DynamicParameters parameters)
    {
        // Column order must match HiddenAccount's positional constructor.
        var hidden = (await connection.QueryAsync<HiddenAccount>(
                "SELECT Id, NormalizedUserName FROM auth.AspNetUsers WHERE IsSystem = 1"))
            .ToList();

        // Nothing hidden: leave the query exactly as it was.
        if (hidden.Count == 0)
            return;

        conditions.Add("(ActorUserId IS NULL OR ActorUserId NOT IN @HiddenUserIds)");
        conditions.Add("(EntityType <> 'User' OR EntityId IS NULL OR EntityId NOT IN @HiddenUserIds)");
        parameters.Add("HiddenUserIds", hidden.Select(h => h.Id).ToList());

        var hiddenNames = hidden
            .Select(h => h.NormalizedUserName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        // NormalizedUserName is already upper-cased by Identity; UPPER() on the stored actor name
        // makes the comparison case-insensitive regardless of the column's collation.
        if (hiddenNames.Count > 0)
        {
            conditions.Add("(ActorName IS NULL OR UPPER(ActorName) NOT IN @HiddenUserNames)");
            parameters.Add("HiddenUserNames", hiddenNames);
        }
    }

    private sealed record HiddenAccount(Guid Id, string? NormalizedUserName);
}
