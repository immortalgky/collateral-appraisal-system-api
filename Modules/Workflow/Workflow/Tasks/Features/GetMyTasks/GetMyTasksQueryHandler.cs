using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.Contracts.Sla;
using Shared.Time;
using Workflow.Tasks.Features.GetTasks;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetMyTasks;

public class GetMyTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    IBusinessTimeCalculator businessTime,
    IDateTimeProvider clock
) : IQueryHandler<GetMyTasksQuery, GetMyTasksResult>
{
    public async Task<GetMyTasksResult> Handle(
        GetMyTasksQuery query,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("AssigneeUserId", currentUserService.Username);

        // Base predicates reference only raw PendingTasks columns.
        var basePredicates = new List<string> { "AssignedType = '1'", "AssigneeUserId = @AssigneeUserId" };

        var filter = query.Filter;
        var filterConditions = TaskListFilterBuilder.BuildConditions(filter, parameters);

        // Data query reads the enriched view (it returns enriched columns).
        var dataConditions = basePredicates.Concat(filterConditions.Select(c => c.Sql));
        var sql = "SELECT * FROM workflow.vw_TaskList WHERE " + string.Join(" AND ", dataConditions);

        // COUNT over the view can't short-circuit; when every filter is on a base
        // PendingTasks column, count straight off the base table (pure index seek).
        string? countSql = null;
        if (!filterConditions.Any(c => c.IsEnriched))
        {
            var countConditions = basePredicates.Concat(filterConditions.Select(c => c.Sql));
            countSql = TaskListFilterBuilder.BaseCountSource + " WHERE " + string.Join(" AND ", countConditions);
        }

        var orderBy = TaskListFilterBuilder.ResolveOrderBy(filter);

        var result = await connectionFactory.QueryPaginatedAsync<TaskDto>(
            sql,
            countSql,
            orderBy,
            query.PaginationRequest,
            parameters);

        // Elapsed/Remaining are computed in C# (business hours, excl. weekends/holidays/lunch)
        // since vw_TaskList no longer derives them. Only the returned page is recomputed.
        var now = clock.ApplicationNow;
        var items = new List<TaskDto>();
        foreach (var t in result.Items)
        {
            var (elapsed, remaining) =
                await businessTime.ComputeElapsedRemainingHoursAsync(now, t.AssignedDate, t.DueAt, cancellationToken);
            items.Add(t with { ElapsedHours = elapsed, RemainingHours = remaining });
        }

        var paged = new PaginatedResult<TaskDto>(items, result.Count, result.PageNumber, result.PageSize);
        return new GetMyTasksResult(paged);
    }
}