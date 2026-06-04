namespace Auth.Application.Features.Users.UpdateUserTeams;

public record UpdateUserTeamsCommand(Guid UserId, List<Guid> TeamIds) : ICommand;
