namespace Auth.Application.Features.Users.UpdateUserGroups;

public record UpdateUserGroupsCommand(Guid UserId, List<Guid> GroupIds) : ICommand;
