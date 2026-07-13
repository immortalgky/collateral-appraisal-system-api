using System.Data;
using Dapper;
using Shared.Data;
using Shared.Identity;
using Workflow.Tasks.Features.Shared;

namespace Workflow.Tasks.Features.GetMyTaskGroupCounts;

public class GetMyTaskGroupCountsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUserService
) : IQueryHandler<GetMyTaskGroupCountsQuery, GetMyTaskGroupCountsResult>
{
    private static readonly HashSet<string> AllowedGroupBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "status", "priority", "purpose", "activity", "slaStatus"
    };

    public async Task<GetMyTaskGroupCountsResult> Handle(
        GetMyTaskGroupCountsQuery query,
        CancellationToken cancellationToken)
    {
        if (!AllowedGroupBy.Contains(query.GroupBy))
            throw new BadRequestException(
                $"Invalid groupBy '{query.GroupBy}'. Allowed values: status, priority, purpose, activity, slaStatus.");

        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return new GetMyTaskGroupCountsResult([]);

        // Same access scope as GetMyTasksQueryHandler: direct assignments to this user
        // (AssignedType='1'), no company gate.
        var parameters = TaskGroupCountsProcParams.Build(
            assignedType: "1",
            assignees: new[] { username },
            companyGate: 0,
            callerCompanyId: null,
            filter: query.Filter,
            groupBy: query.GroupBy);

        var connection = connectionFactory.GetOpenConnection();
        var rows = await connection.QueryAsync<TaskGroupCountDto>(new CommandDefinition(
            "workflow.sp_GetTaskGroupCounts", parameters,
            commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return new GetMyTaskGroupCountsResult(rows.ToList());
    }
}
