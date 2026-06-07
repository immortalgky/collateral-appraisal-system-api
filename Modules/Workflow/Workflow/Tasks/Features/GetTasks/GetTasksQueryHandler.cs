using Dapper;
using Shared.Data;
using Shared.Pagination;
using Workflow.Contracts.Sla;
using Shared.Time;

namespace Workflow.Tasks.Features.GetTasks;

public class GetTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IBusinessTimeCalculator businessTime,
    IDateTimeProvider clock
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

            if (!string.IsNullOrWhiteSpace(filter.SlaStatus))
            {
                conditions.Add("SlaStatus = @SlaStatus");
                parameters.Add("SlaStatus", filter.SlaStatus);
            }
        }

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var sortField = AllowedSortFields.Contains(filter?.SortBy ?? "") ? filter!.SortBy! : "AssignedDate";
        var sortDir = string.Equals(filter?.SortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // ElapsedHours/RemainingHours are no longer columns on the view (computed in C# below).
        // Their business-time values are monotonic in the underlying timestamps, so we translate
        // the sort to those columns for exact ordering:
        //   ElapsedHours  ASC  ≡ AssignedDate DESC (least elapsed = most recent assignment)
        //   RemainingHours ASC ≡ DueAt        ASC  (least remaining = earliest due)
        var orderBy = sortField switch
        {
            "ElapsedHours" => $"AssignedDate {Invert(sortDir)}",
            "RemainingHours" => $"DueAt {sortDir}",
            _ => $"{sortField} {sortDir}"
        };

        var result = await connectionFactory.QueryPaginatedAsync<TaskDto>(
            sql,
            orderBy,
            query.PaginationRequest,
            parameters);

        // Business-time Elapsed/Remaining: exclude weekends, holidays and lunch via the shared
        // calculator. Only the returned page is recomputed; the calculator caches config/holidays.
        var now = clock.ApplicationNow;
        var items = new List<TaskDto>();
        foreach (var t in result.Items)
        {
            var (elapsed, remaining) =
                await businessTime.ComputeElapsedRemainingHoursAsync(now, t.AssignedDate, t.DueAt, cancellationToken);
            items.Add(t with { ElapsedHours = elapsed, RemainingHours = remaining });
        }

        var paged = new PaginatedResult<TaskDto>(items, result.Count, result.PageNumber, result.PageSize);
        return new GetTasksResult(paged);
    }

    private static string Invert(string dir) =>
        string.Equals(dir, "ASC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
}
