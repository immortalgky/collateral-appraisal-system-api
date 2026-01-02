namespace Assignment.Services.Groups;

/// <summary>
/// Service for managing user group memberships
/// </summary>
public class UserGroupService : IUserGroupService
{
    private readonly ILogger<UserGroupService> _logger;
    private readonly Dictionary<string, List<string>> _groupMemberships;

    public UserGroupService(ILogger<UserGroupService> logger)
    {
        _logger = logger;

        // TODO: Replace with actual group membership data source (e.g., database, external service)
        // Testing purposes only
        _groupMemberships = new Dictionary<string, List<string>>
        {
            ["Juniors"] = new() { "Alice", "Bob", "Charlie" },
            ["Seniors"] = new() { "Dave", "Eve", "Frank" }
        };
    }

    public async Task<List<string>> GetUsersInGroupAsync(string groupName,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (_groupMemberships.TryGetValue(groupName, out var users))
        {
            _logger.LogDebug("Found {UserCount} users in group {GroupName}", users.Count, groupName);
            return users;
        }

        _logger.LogWarning("Group {GroupName} not found, returning empty user list", groupName);
        return new List<string>();
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

        // Remove duplicates (user might be in multiple groups)
        var distinctUsers = allUsers.Distinct().ToList();

        _logger.LogDebug("Found {UserCount} unique users across {GroupCount} groups",
            distinctUsers.Count, groupNames.Count);

        return distinctUsers;
    }
}