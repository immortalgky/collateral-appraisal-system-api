using Dapper;
using Shared.Data;
using Shared.Identity;
using Shared.Pagination;
using Workflow.Contracts.Sla;
using Shared.Time;
using Workflow.AssigneeSelection.Teams;
using Workflow.Services.Groups;
using Workflow.Tasks.Authorization;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetPoolTasks;

public class GetPoolTasksQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService,
    IUserGroupService userGroupService,
    ITeamService teamService,
    IBusinessTimeCalculator businessTime,
    IDateTimeProvider clock
) : IQueryHandler<GetPoolTasksQuery, GetPoolTasksResult>
{
    public async Task<GetPoolTasksResult> Handle(
        GetPoolTasksQuery query,
        CancellationToken cancellationToken)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        // Get user's groups to match against pool assignments
        var userGroups = await userGroupService.GetGroupsForUserAsync(username, cancellationToken);
        if (userGroups.Count == 0)
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        var team = await teamService.GetTeamForUserAsync(username, cancellationToken);

        var clause = PoolTaskAccess.BuildSqlClause(userGroups, team?.TeamId, currentUserService.CompanyId, username);
        if (clause is null)
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        var parameters = new DynamicParameters();
        foreach (var (k, v) in clause.Parameters)
            parameters.Add(k, v);

        // Base predicates reference only raw PendingTasks columns (AssignedType +
        // the pool-access clause on AssigneeUserId/AssigneeCompanyId).
        var basePredicates = new List<string> { "AssignedType = '2'", clause.Sql };

        var filter = query.Filter;
        var filterConditions = TaskListFilterBuilder.BuildConditions(filter, parameters);

        // Data query reads the enriched view (it returns enriched columns).
        var dataConditions = basePredicates.Concat(filterConditions.Select(c => c.Sql));
        var sql = "SELECT * FROM workflow.vw_TaskList WHERE " + string.Join(" AND ", dataConditions);

        // COUNT is the expensive part over the view (it can't short-circuit like the
        // paged data query). When every filter is on a base PendingTasks column we
        // count straight off the base table instead — a pure index seek.
        string? countSql = null;
        if (!filterConditions.Any(c => c.IsEnriched))
        {
            var countConditions = basePredicates.Concat(filterConditions.Select(c => c.Sql));
            countSql = TaskListFilterBuilder.BaseCountSource + " WHERE " + string.Join(" AND ", countConditions);
        }

        var orderBy = TaskListFilterBuilder.ResolveOrderBy(filter);

        var result = await connectionFactory.QueryPaginatedAsync<PoolTaskDto>(
            sql,
            countSql,
            orderBy,
            query.PaginationRequest,
            parameters);

        // Elapsed/Remaining are computed in C# (business hours, excl. weekends/holidays/lunch)
        // since vw_TaskList no longer derives them. Only the returned page is recomputed.
        var now = clock.ApplicationNow;
        var items = new List<PoolTaskDto>();
        foreach (var t in result.Items)
        {
            var (elapsed, remaining) =
                await businessTime.ComputeElapsedRemainingHoursAsync(now, t.AssignedDate, t.DueAt, cancellationToken);
            items.Add(t with { ElapsedHours = elapsed, RemainingHours = remaining });
        }

        var paged = new PaginatedResult<PoolTaskDto>(items, result.Count, result.PageNumber, result.PageSize);
        return new GetPoolTasksResult(paged);
    }
}
