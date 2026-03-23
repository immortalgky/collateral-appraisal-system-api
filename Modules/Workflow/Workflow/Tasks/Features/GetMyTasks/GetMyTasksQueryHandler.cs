using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.Tasks.Features.GetTasks;

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

        // Always filter by current user
        conditions.Add("AssigneeUserId = @AssigneeUserId");
        parameters.Add("AssigneeUserId", currentUserService.Username);

        var filter = query.Filter;
        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                conditions.Add("Status = @Status");
                parameters.Add("Status", filter.Status);
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
            "ReceivedDate DESC",
            query.PaginationRequest,
            parameters);

        return new GetMyTasksResult(result);
    }
}