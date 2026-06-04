using Dapper;

namespace Auth.Application.Features.AuditLog.GetAuditLogs;

public class GetAuditLogsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<GetAuditLogsQuery, GetAuditLogsResult>
{
    public async Task<GetAuditLogsResult> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

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

        var connection = sqlConnectionFactory.GetOpenConnection();

        using var multi = await connection.QueryMultipleAsync(
            countSql + "; " + dataSql,
            parameters);

        var totalCount = await multi.ReadFirstOrDefaultAsync<int>();
        var items = (await multi.ReadAsync<AuditLogItemDto>()).ToList();

        return new GetAuditLogsResult(items, totalCount, pageNumber, pageSize);
    }
}
