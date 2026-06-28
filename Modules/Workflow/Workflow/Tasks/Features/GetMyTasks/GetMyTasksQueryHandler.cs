using System.Data;
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
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetMyTasksResult(new PaginatedResult<TaskDto>([], 0, 0, 10));

        // My tasks: direct assignments to this user (AssignedType='1'), no company gate.
        var parameters = TaskListProcParams.Build(
            assignedType: "1",
            assignees: new[] { username },
            companyGate: 0,
            callerCompanyId: null,
            filter: query.Filter,
            pagination: query.PaginationRequest);

        var connection = connectionFactory.GetOpenConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            "workflow.sp_GetTaskList", parameters,
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        var rows = (await grid.ReadAsync<TaskDto>()).ToList();
        var total = await grid.ReadFirstAsync<int>();

        // Elapsed/Remaining are computed in C# (business hours, excl. weekends/holidays/lunch).
        var now = clock.ApplicationNow;
        var items = new List<TaskDto>(rows.Count);
        foreach (var t in rows)
        {
            var (elapsed, remaining) =
                await businessTime.ComputeElapsedRemainingHoursAsync(now, t.SlaStartAt ?? t.AssignedDate, t.DueAt, clockStart: t.SlaStartAt, ct: cancellationToken);
            items.Add(t with { ElapsedHours = elapsed, RemainingHours = remaining });
        }

        var paged = new PaginatedResult<TaskDto>(
            items, total, query.PaginationRequest.PageNumber, query.PaginationRequest.PageSize);
        return new GetMyTasksResult(paged);
    }
}