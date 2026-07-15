using Dapper;

namespace Workflow.Services.TaskMonitor;

/// <summary>
/// Resolves the optional <c>:TEAM</c> scope on the Task Monitor permissions
/// (<c>TASK_MONITOR_VIEW</c> / <c>TASK_MONITOR_REASSIGN</c>).
///
/// A user holding the plain base permission sees every user in their monitored groups (base wins).
/// A user holding only the <c>:TEAM</c> variant is additionally confined to their own "team" —
/// same auth.Teams team for internal users, same company for external users — resolved by the
/// shared <see cref="Shared.Identity.TeamScopePredicate"/> so Task Monitor and the Monitoring
/// screens never drift.
/// </summary>
public interface ITaskMonitorScope
{
    /// <summary>
    /// True when the user holds the <c>:TEAM</c> variant of <paramref name="basePermission"/> but
    /// NOT the base permission (base wins).
    /// </summary>
    bool IsTeamScoped(string basePermission);

    /// <summary>
    /// Returns an extra SQL WHERE fragment (no leading AND) restricting a person-assignee column
    /// to the user's team/company, or null when the user is not team-scoped.
    /// </summary>
    string? BuildScopeClause(string basePermission, DynamicParameters parameters);

    /// <summary>
    /// True when <paramref name="targetUsername"/> is within the current user's team/company scope,
    /// or when the user is not team-scoped. Used to block cross-boundary reassignment.
    /// </summary>
    Task<bool> IsTargetInScopeAsync(string basePermission, string targetUsername, CancellationToken cancellationToken = default);
}
