namespace Assignment.Services.Groups;

// TODO: Later move to user management module

public interface IUserGroupService
{
    Task<List<string>> GetUsersInGroupAsync(string groupName, CancellationToken cancellationToken = default);
    Task<List<string>> GetUsersInGroupsAsync(List<string> groupNames, CancellationToken cancellationToken = default);
}