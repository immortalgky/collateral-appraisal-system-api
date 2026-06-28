using System.Data;
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

        var (assignees, companyGate) =
            PoolTaskAccess.BuildProcAccess(userGroups, team?.TeamId, currentUserService.CompanyId, username);
        if (assignees.Count == 0)
            return new GetPoolTasksResult(new PaginatedResult<PoolTaskDto>([], 0, 0, 10));

        var parameters = TaskListProcParams.Build(
            assignedType: "2",
            assignees: assignees,
            companyGate: companyGate,
            callerCompanyId: currentUserService.CompanyId,
            filter: query.Filter,
            pagination: query.PaginationRequest);

        var connection = connectionFactory.GetOpenConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            "workflow.sp_GetTaskList", parameters,
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        var rows = (await grid.ReadAsync<PoolTaskDto>()).ToList();
        var total = await grid.ReadFirstAsync<int>();

        // Elapsed/Remaining are computed in C# (business hours, excl. weekends/holidays/lunch).
        var now = clock.ApplicationNow;
        var items = new List<PoolTaskDto>(rows.Count);
        foreach (var t in rows)
        {
            var (elapsed, remaining) =
                await businessTime.ComputeElapsedRemainingHoursAsync(now, t.SlaStartAt ?? t.AssignedDate, t.DueAt, clockStart: t.SlaStartAt, ct: cancellationToken);
            items.Add(t with { ElapsedHours = elapsed, RemainingHours = remaining });
        }

        var paged = new PaginatedResult<PoolTaskDto>(
            items, total, query.PaginationRequest.PageNumber, query.PaginationRequest.PageSize);
        return new GetPoolTasksResult(paged);
    }
}
