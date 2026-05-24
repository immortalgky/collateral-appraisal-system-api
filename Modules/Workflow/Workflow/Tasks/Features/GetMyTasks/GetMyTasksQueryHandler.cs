using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.Tasks.Features.GetTasks;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetMyTasks;

public class GetMyTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetMyTasksQuery, GetMyTasksResult>
{
    public async Task<GetMyTasksResult> Handle(
        GetMyTasksQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM workflow.vw_TaskList";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        conditions.Add("AssignedType = '1'");
        conditions.Add("AssigneeUserId = @AssigneeUserId");
        parameters.Add("AssigneeUserId", currentUserService.Username);

        var filter = query.Filter;
        TaskListFilterBuilder.ApplyFilters(filter, conditions, parameters);

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var orderBy = TaskListFilterBuilder.ResolveOrderBy(filter);

        var result = await connectionFactory.QueryPaginatedAsync<TaskDto>(
            sql,
            orderBy,
            query.PaginationRequest,
            parameters);

        return new GetMyTasksResult(result);
    }
}