using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetMonitoredTasks;

public class GetMonitoredTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetMonitoredTasksQuery, GetMonitoredTasksResult>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssignedAt", "DueAt", "SlaStatus", "ElapsedHours", "RemainingHours",
        "AppraisalNumber", "CustomerName", "TaskName", "AssignedTo", "GroupName",
        "Purpose", "AppraisalStatus"
    };

    public async Task<GetMonitoredTasksResult> Handle(
        GetMonitoredTasksQuery query, CancellationToken cancellationToken)
    {
        var currentUser = currentUserService.Username;

        var sql = "SELECT * FROM workflow.vw_TaskMonitor";
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // Base filter: only tasks in groups monitored by the current user
        conditions.Add(
            """
            AssignedTo IN (
                SELECT tgtU.UserName
                FROM auth.GroupMonitoring gm
                INNER JOIN auth.GroupUsers supGu ON supGu.GroupId = gm.MonitorGroupId
                INNER JOIN auth.AspNetUsers supU ON supU.Id = supGu.UserId
                    AND supU.NormalizedUserName = @SupervisorNormalizedUserName
                INNER JOIN auth.Groups supG ON supG.Id = gm.MonitorGroupId AND supG.IsDeleted = 0
                INNER JOIN auth.Groups tgtG ON tgtG.Id = gm.MonitoredGroupId AND tgtG.IsDeleted = 0
                INNER JOIN auth.GroupUsers tgtGu ON tgtGu.GroupId = gm.MonitoredGroupId
                INNER JOIN auth.AspNetUsers tgtU ON tgtU.Id = tgtGu.UserId
            )
            """);
        parameters.Add("SupervisorNormalizedUserName", currentUser?.ToUpperInvariant() ?? "");

        var filter = query.Filter;
        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.GroupId))
            {
                conditions.Add("GroupId = @GroupId");
                parameters.Add("GroupId", filter.GroupId);
            }

            if (!string.IsNullOrWhiteSpace(filter.AssigneeUsername))
            {
                conditions.Add("AssignedTo = @AssigneeUsername");
                parameters.Add("AssigneeUsername", filter.AssigneeUsername);
            }

            if (!string.IsNullOrWhiteSpace(filter.SlaStatus))
            {
                conditions.Add("SlaStatus = @SlaStatus");
                parameters.Add("SlaStatus", filter.SlaStatus);
            }

            if (!string.IsNullOrWhiteSpace(filter.ActivityId))
            {
                conditions.Add("ActivityId = @ActivityId");
                parameters.Add("ActivityId", filter.ActivityId);
            }

            if (!string.IsNullOrWhiteSpace(filter.AppraisalNumber))
            {
                conditions.Add("AppraisalNumber LIKE '%' + @AppraisalNumber + '%'");
                parameters.Add("AppraisalNumber", filter.AppraisalNumber);
            }

            if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            {
                conditions.Add("CustomerName LIKE '%' + @CustomerName + '%'");
                parameters.Add("CustomerName", filter.CustomerName);
            }

            if (!string.IsNullOrWhiteSpace(filter.AppraisalStatus))
            {
                conditions.Add("AppraisalStatus = @AppraisalStatus");
                parameters.Add("AppraisalStatus", filter.AppraisalStatus);
            }

            if (!string.IsNullOrWhiteSpace(filter.TaskType))
            {
                conditions.Add("TaskDescription = @TaskType");
                parameters.Add("TaskType", filter.TaskType);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                conditions.Add("(AppraisalNumber LIKE '%' + @Search + '%' OR CustomerName LIKE '%' + @Search + '%' OR AssignedTo LIKE '%' + @Search + '%')");
                parameters.Add("Search", filter.Search);
            }
        }

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "AssignedAt";
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"{sortField} {sortDir}";

        var result = await connectionFactory.QueryPaginatedAsync<MonitoredTaskDto>(
            sql, orderBy, query.PaginationRequest, parameters);

        return new GetMonitoredTasksResult(result);
    }
}
