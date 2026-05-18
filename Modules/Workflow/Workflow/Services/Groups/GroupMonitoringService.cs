using Dapper;
using Shared.Data;

namespace Workflow.Services.Groups;

/// <summary>
/// Resolves supervisor relationships via auth.GroupMonitoring.
/// Supervisor = a user whose group has a GroupMonitoring row pointing at the target user's group.
/// </summary>
public class GroupMonitoringService : IGroupMonitoringService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<GroupMonitoringService> _logger;

    public GroupMonitoringService(ISqlConnectionFactory connectionFactory, ILogger<GroupMonitoringService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<bool> IsUserSupervisedByAsync(
        string targetUsername,
        string supervisorUsername,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.GetOpenConnection();

        // The supervisor's group must have a GroupMonitoring entry pointing at the target user's group.
        var count = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM auth.GroupMonitoring gm
            -- supervisor side: gm.MonitorGroupId is a group the supervisor belongs to
            INNER JOIN auth.GroupUsers supGu ON supGu.GroupId = gm.MonitorGroupId
            INNER JOIN auth.AspNetUsers supU ON supU.Id = supGu.UserId
                AND supU.NormalizedUserName = @SupervisorNormalizedUserName
            -- target side: gm.MonitoredGroupId is a group the target belongs to
            INNER JOIN auth.GroupUsers tgtGu ON tgtGu.GroupId = gm.MonitoredGroupId
            INNER JOIN auth.AspNetUsers tgtU ON tgtU.Id = tgtGu.UserId
                AND tgtU.NormalizedUserName = @TargetNormalizedUserName
            -- skip deleted groups on both sides
            INNER JOIN auth.Groups supG ON supG.Id = gm.MonitorGroupId AND supG.IsDeleted = 0
            INNER JOIN auth.Groups tgtG ON tgtG.Id = gm.MonitoredGroupId AND tgtG.IsDeleted = 0
            """,
            new
            {
                SupervisorNormalizedUserName = supervisorUsername.ToUpperInvariant(),
                TargetNormalizedUserName = targetUsername.ToUpperInvariant()
            });

        _logger.LogDebug(
            "IsUserSupervisedBy: target={Target}, supervisor={Supervisor}, result={Result}",
            targetUsername, supervisorUsername, count > 0);

        return count > 0;
    }
}
