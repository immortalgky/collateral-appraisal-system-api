using Dapper;
using Shared.Data;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public class GetTasksQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetTasksQuery, GetTasksResult>
{
    public async Task<GetTasksResult> Handle(
        GetTasksQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM workflow.vw_TaskList";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var filter = query.Filter;
        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                conditions.Add("Status = @Status");
                parameters.Add("Status", filter.Status);
            }

            if (!string.IsNullOrWhiteSpace(filter.AssigneeUserId))
            {
                conditions.Add("AssigneeUserId = @AssigneeUserId");
                parameters.Add("AssigneeUserId", filter.AssigneeUserId);
            }

            if (!string.IsNullOrWhiteSpace(filter.Priority))
            {
                conditions.Add("Priority = @Priority");
                parameters.Add("Priority", filter.Priority);
            }

            if (!string.IsNullOrWhiteSpace(filter.TaskName))
            {
                conditions.Add("TaskType = @TaskName");
                parameters.Add("TaskName", filter.TaskName);
            }
        }

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var result = await connectionFactory.QueryPaginatedAsync<TaskDto>(
            sql,
            "RequestedAt DESC",
            query.PaginationRequest,
            parameters);

        return new GetTasksResult(result);
    }
}
