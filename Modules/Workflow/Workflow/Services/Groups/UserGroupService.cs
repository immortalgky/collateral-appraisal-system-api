using Dapper;
using Shared.Data;

namespace Workflow.Services.Groups;

/// <summary>
/// Queries real user IDs from ASP.NET Identity tables by role name.
/// Group name = role name (e.g. "Admin", "IntAppraisalStaff").
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
            INNER JOIN auth.AspNetUserRoles ur ON ur.UserId = u.Id
            INNER JOIN auth.AspNetRoles r ON r.Id = ur.RoleId
            WHERE r.NormalizedName = @NormalizedRoleName
            """,
            new { NormalizedRoleName = groupName.ToUpperInvariant() });

        var result = userIds.ToList();
        _logger.LogDebug("Found {UserCount} users in group {GroupName}", result.Count, groupName);
        return result;
    }

    public async Task<List<string>> GetUsersInGroupsAsync(List<string> groupNames,
        CancellationToken cancellationToken = default)
    {
        var allUsers = new List<string>();

        foreach (var groupName in groupNames)
        {
            var groupUsers = await GetUsersInGroupAsync(groupName, cancellationToken);
            allUsers.AddRange(groupUsers);
        }

        var distinctUsers = allUsers.Distinct().ToList();

        _logger.LogDebug("Found {UserCount} unique users across {GroupCount} groups",
            distinctUsers.Count, groupNames.Count);

        return distinctUsers;
    }

    public async Task<List<string>> GetGroupsForUserAsync(string username,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.GetOpenConnection();

        var roles = await connection.QueryAsync<string>(
            """
            SELECT r.Name
            FROM auth.AspNetRoles r
            INNER JOIN auth.AspNetUserRoles ur ON ur.RoleId = r.Id
            INNER JOIN auth.AspNetUsers u ON u.Id = ur.UserId
            WHERE u.NormalizedUserName = @NormalizedUserName
            """,
            new { NormalizedUserName = username.ToUpperInvariant() });

        var result = roles.ToList();
        _logger.LogDebug("Found {GroupCount} groups for user {Username}", result.Count, username);
        return result;
    }
}
