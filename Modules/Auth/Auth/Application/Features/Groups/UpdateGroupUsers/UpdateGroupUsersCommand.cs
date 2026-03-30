namespace Auth.Application.Features.Groups.UpdateGroupUsers;

public record UpdateGroupUsersCommand(Guid Id, List<Guid> UserIds) : ICommand;
