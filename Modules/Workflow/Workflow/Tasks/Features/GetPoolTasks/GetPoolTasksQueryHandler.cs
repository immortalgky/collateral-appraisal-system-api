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

        // Query pool tasks where AssignedType='2' and AssignedTo contains one of user's groups
        var sql = """
            SELECT
                pt.Id,
                pt.WorkflowInstanceId,
                pt.ActivityId,
                a.AppraisalNumber,
                c.Name AS CustomerName,
                pt.TaskName AS TaskType,
                pt.TaskDescription,
                r.Purpose,
                p.PropertyType,
                a.Status,
                ap.AppointmentDateTime,
                pt.AssignedTo,
                pt.AssignedType,
                pt.WorkingBy,
                r.RequestedAt,
                pt.AssignedAt AS ReceivedDate,
                NULL AS Movement,
                NULL AS SLADays,
                NULL AS OLAActual,
                NULL AS OLADiff,
                NULL AS Priority
            FROM workflow.PendingTasks pt
            LEFT JOIN appraisal.Appraisals a ON a.Id = pt.CorrelationId
            LEFT JOIN request.Requests r ON a.RequestId = r.Id
            LEFT JOIN request.Contacts c ON r.Id = c.RequestId
            LEFT JOIN appraisal.Properties p ON a.Id = p.AppraisalId
            LEFT JOIN appraisal.Appointments ap ON a.Id = ap.AppraisalId
            WHERE pt.AssignedType = '2'
            """;

        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // Match user's groups against the pool's AssignedTo field
        // AssignedTo may contain group names like "Admin" or "Admin:Team_xxx"
        var groupConditions = new List<string>();
        for (var i = 0; i < userGroups.Count; i++)
        {
            groupConditions.Add($"pt.AssignedTo LIKE @Group{i}");
            parameters.Add($"Group{i}", $"%{userGroups[i]}%");
        }

        conditions.Add($"({string.Join(" OR ", groupConditions)})");

        var filter = query.Filter;
        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                conditions.Add("a.Status = @Status");
                parameters.Add("Status", filter.Status);
            }

            if (!string.IsNullOrWhiteSpace(filter.Priority))
            {
                conditions.Add("1=1"); // Priority column not yet available
            }

            if (!string.IsNullOrWhiteSpace(filter.TaskName))
            {
                conditions.Add("pt.TaskName = @TaskName");
                parameters.Add("TaskName", filter.TaskName);
            }
        }

        if (conditions.Count > 0)
            sql += " AND " + string.Join(" AND ", conditions);

        var result = await connectionFactory.QueryPaginatedAsync<PoolTaskDto>(
            sql,
            "ReceivedDate DESC",
            query.PaginationRequest,
            parameters);

        return new GetPoolTasksResult(result);
    }
}
