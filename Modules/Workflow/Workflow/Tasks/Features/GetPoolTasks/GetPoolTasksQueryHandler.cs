using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.Services.Groups;

namespace Workflow.Tasks.Features.GetPoolTasks;

public class GetPoolTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    IUserGroupService userGroupService
) : IQueryHandler<GetPoolTasksQuery, GetPoolTasksResult>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "CustomerName", "TaskType", "Purpose", "PropertyType",
        "Status", "AppointmentDateTime", "RequestedBy", "RequestReceivedDate",
        "AssignedDate", "Movement", "InternalFollowupStaff", "Appraiser",
        "Priority", "DueAt", "SlaStatus", "ElapsedHours", "RemainingHours"
    };

    public async Task<GetPoolTasksResult> Handle(
        GetPoolTasksQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        // Get user's groups (role names) to match against pool assignments
        var userGroups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        if (userGroups.Count == 0)
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        var sql = "SELECT * FROM workflow.vw_TaskList";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // Pool tasks are assigned to a group (AssigneeUserId contains a group name)
        var groupConditions = new List<string>();
        for (var i = 0; i < userGroups.Count; i++)
        {
            groupConditions.Add($"AssigneeUserId LIKE @Group{i}");
            parameters.Add($"Group{i}", $"%{userGroups[i]}%");
        }

        conditions.Add($"({string.Join(" OR ", groupConditions)})");

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

        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "AssignedDate";
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"{sortField} {sortDir}";

        var result = await connectionFactory.QueryPaginatedAsync<PoolTaskDto>(
            sql,
            orderBy,
            query.PaginationRequest,
            parameters);

        return new GetPoolTasksResult(result);
    }
}
