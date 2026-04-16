using Dapper;
using Shared.Data;
using Shared.Pagination;

namespace Workflow.Tasks.Features.GetTasks;

public class GetTasksQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetTasksQuery, GetTasksResult>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppraisalNumber", "RequestNumber", "CustomerName", "TaskType", "Purpose", "PropertyType",
        "Status", "AppointmentDateTime", "RequestedBy", "RequestReceivedDate",
        "AssignedDate", "Movement", "InternalFollowupStaff", "Appraiser",
        "Priority", "DueAt", "SlaStatus", "ElapsedHours", "RemainingHours"
    };

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

            if (!string.IsNullOrWhiteSpace(filter.ActivityId))
            {
                conditions.Add("ActivityId = @ActivityId");
                parameters.Add("ActivityId", filter.ActivityId);
            }

            if (!string.IsNullOrWhiteSpace(filter.AppraisalNumber))
            {
                conditions.Add("AppraisalNumber LIKE @AppraisalNumber + '%'");
                parameters.Add("AppraisalNumber", filter.AppraisalNumber);
            }

            if (!string.IsNullOrWhiteSpace(filter.CustomerName))
            {
                conditions.Add("CustomerName LIKE '%' + @CustomerName + '%'");
                parameters.Add("CustomerName", filter.CustomerName);
            }

            if (!string.IsNullOrWhiteSpace(filter.TaskStatus))
            {
                conditions.Add("PendingTaskStatus = @TaskStatus");
                parameters.Add("TaskStatus", filter.TaskStatus);
            }

            if (!string.IsNullOrWhiteSpace(filter.TaskType))
            {
                conditions.Add("TaskType = @TaskType");
                parameters.Add("TaskType", filter.TaskType);
            }

            if (filter.DateFrom.HasValue)
            {
                conditions.Add("AssignedDate >= @DateFrom");
                parameters.Add("DateFrom", filter.DateFrom.Value);
            }

            if (filter.DateTo.HasValue)
            {
                conditions.Add("AssignedDate < DATEADD(day, 1, @DateTo)");
                parameters.Add("DateTo", filter.DateTo.Value);
            }
        }

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "AssignedDate";
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";
        var orderBy = $"{sortField} {sortDir}";

        var result = await connectionFactory.QueryPaginatedAsync<TaskDto>(
            sql,
            orderBy,
            query.PaginationRequest,
            parameters);

        return new GetTasksResult(result);
    }
}
