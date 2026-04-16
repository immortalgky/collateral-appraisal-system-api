using Dapper;
using Shared.Data;

namespace Workflow.Services.Groups;

/// <summary>
/// Queries user IDs from auth.Groups/auth.GroupUsers tables by group name.
/// </summary>
public class UserGroupService : IUserGroupService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<UserGroupService> _logger;

    public UserGroupService(ISqlConnectionFactory connectionFactory, ILogger<UserGroupService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<List<string>> GetUsersInGroupAsync(string groupName,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.GetOpenConnection();

        var userIds = await connection.QueryAsync<string>(
            """
            SELECT u.UserName
            FROM auth.AspNetUsers u
            INNER JOIN auth.GroupUsers gu ON gu.UserId = u.Id
            INNER JOIN auth.Groups g ON g.Id = gu.GroupId
            WHERE g.Name = @GroupName
              AND g.IsDeleted = 0
            """,
            new { GroupName = groupName });

        var result = userIds.ToList();
        _logger.LogDebug("Found {UserCount} users in group {GroupName}", result.Count, groupName);
        return result;
    }

    public async Task<List<string>> GetUsersInGroupsAsync(List<string> groupNames,
        CancellationToken cancellationToken = default)
    {
        if (groupNames.Count == 0)
            return [];

        using var connection = _connectionFactory.GetOpenConnection();

        var users = await connection.QueryAsync<string>(
            """
            SELECT DISTINCT u.UserName
            FROM auth.AspNetUsers u
            INNER JOIN auth.GroupUsers gu ON gu.UserId = u.Id
            INNER JOIN auth.Groups g ON g.Id = gu.GroupId
            WHERE g.Name IN @GroupNames
              AND g.IsDeleted = 0
            """,
            new { GroupNames = groupNames });

        var result = users.ToList();

        _logger.LogDebug("Found {UserCount} unique users across {GroupCount} groups",
            result.Count, groupNames.Count);

        return result;
    }

    public async Task<List<string>> GetGroupsForUserAsync(string username,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.GetOpenConnection();

        var groups = await connection.QueryAsync<string>(
            """
            SELECT g.Name
            FROM auth.Groups g
            INNER JOIN auth.GroupUsers gu ON gu.GroupId = g.Id
            INNER JOIN auth.AspNetUsers u ON u.Id = gu.UserId
            WHERE u.NormalizedUserName = @NormalizedUserName
              AND g.IsDeleted = 0
            """,
            new { NormalizedUserName = username.ToUpperInvariant() });

        var result = groups.ToList();
        _logger.LogDebug("Found {GroupCount} groups for user {Username}", result.Count, username);
        return result;
    }
}
