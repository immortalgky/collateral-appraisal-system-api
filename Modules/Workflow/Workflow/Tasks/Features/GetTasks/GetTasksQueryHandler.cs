using Dapper;
using Shared.Data;
using Shared.Pagination;
using Workflow.Contracts.Sla;
using Shared.Time;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetTasks;

public class GetTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    IBusinessTimeCalculator businessTime,
    IDateTimeProvider clock
) : IQueryHandler<GetTasksQuery, GetTasksResult>
{
    public async Task<GetTasksResult> Handle(
        GetTasksQuery query,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT * FROM workflow.vw_TaskList";
        var parameters = new DynamicParameters();

        var filter = query.Filter;
        var conditions = TaskListFilterBuilder.BuildConditions(filter, parameters);

        if (!string.IsNullOrWhiteSpace(filter?.AssigneeUserId))
        {
            conditions.Add("AssigneeUserId = @AssigneeUserId");
            parameters.Add("AssigneeUserId", filter.AssigneeUserId);
        }

        if (conditions.Count > 0)
            sql += " WHERE " + string.Join(" AND ", conditions);

        var orderBy = TaskListFilterBuilder.ResolveOrderBy(filter);

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
                await businessTime.ComputeElapsedRemainingHoursAsync(now, t.SlaStartAt ?? t.AssignedDate, t.DueAt, clockStart: t.SlaStartAt, ct: cancellationToken);
            items.Add(t with { ElapsedHours = elapsed, RemainingHours = remaining });
        }

        var paged = new PaginatedResult<TaskDto>(items, result.Count, result.PageNumber, result.PageSize);
        return new GetTasksResult(paged);
    }
}
