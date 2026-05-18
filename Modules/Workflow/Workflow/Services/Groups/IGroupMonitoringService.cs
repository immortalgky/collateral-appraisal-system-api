namespace Workflow.Services.Groups;

/// <summary>
/// Queries the GroupMonitoring join table to determine supervisor relationships.
/// A supervisor is a user whose group monitors the group that contains the target user.
/// </summary>
public interface IGroupMonitoringService
{
    /// <summary>
    /// Returns true if <paramref name="supervisorUsername"/> belongs to any group that
    /// monitors the group(s) containing <paramref name="targetUsername"/>.
    /// </summary>
    Task<bool> IsUserSupervisedByAsync(string targetUsername, string supervisorUsername, CancellationToken cancellationToken = default);
}
