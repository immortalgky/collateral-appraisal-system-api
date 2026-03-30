using Dapper;
using Shared.CQRS;
using Shared.Data;
using Shared.Identity;

namespace Common.Application.Features.Dashboard.GetRecentTasks;

public class GetRecentTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetRecentTasksQuery, GetRecentTasksResult>
{
    public async Task<GetRecentTasksResult> Handle(
        GetRecentTasksQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetRecentTasksResult([]);

        var connection = connectionFactory.GetOpenConnection();

        var sql = """
            SELECT TOP (@Limit)
                Id, AppraisalNumber, CustomerName, TaskType, Purpose, Status, RequestedAt
            FROM workflow.vw_TaskList
            WHERE AssigneeUserId = @Username
            ORDER BY RequestedAt DESC
            """;

        var items = await connection.QueryAsync<RecentTaskDto>(sql,
            new { query.Limit, Username = username });

        return new GetRecentTasksResult(items.ToList());
    }
}
