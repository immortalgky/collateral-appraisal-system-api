using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Workflow.Services.TaskMonitor;

/// <inheritdoc />
public class TaskMonitorScope(
    ICurrentUserService currentUserService,
    ISqlConnectionFactory connectionFactory) : ITaskMonitorScope
{
    private const string TeamSuffix = ":TEAM";

    public bool IsTeamScoped(string basePermission)
    {
        var permissions = currentUserService.Permissions;
        // Base wins: an unscoped grant means "see everyone", so :TEAM only bites when the user
        // holds the :TEAM variant WITHOUT the base permission.
        return !permissions.Contains(basePermission) && permissions.Contains(basePermission + TeamSuffix);
    }

    public string? BuildScopeClause(string basePermission, DynamicParameters parameters)
    {
        // workflow.vw_TaskMonitor already exposes only person-assigned rows and has no AssignedType
        // column, so skip the AssignedType='1' guard (it would reference a non-existent column).
        return IsTeamScoped(basePermission)
            ? TeamScopePredicate.Build(currentUserService, parameters, requirePersonAssigned: false)
            : null;
    }

    public async Task<bool> IsTargetInScopeAsync(
        string basePermission, string targetUsername, CancellationToken cancellationToken = default)
    {
        if (!IsTeamScoped(basePermission))
            return true;
        if (string.IsNullOrWhiteSpace(targetUsername))
            return false;

        var parameters = new DynamicParameters();
        var predicate = TeamScopePredicate.Build(currentUserService, parameters, requirePersonAssigned: false);
        parameters.Add("TargetUser", targetUsername);

        // Evaluate the shared predicate against a single-row derived table exposing the AssignedTo
        // column it references, so the same team/company rule that filters the list also gates the
        // reassign action. The caller already guarantees a person-assigned task, so no AssignedType.
        var sql = $"""
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM (SELECT @TargetUser AS AssignedTo) x
                WHERE {predicate}
            ) THEN 1 ELSE 0 END
            """;

        using var connection = connectionFactory.GetOpenConnection();
        var match = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return match == 1;
    }
}
