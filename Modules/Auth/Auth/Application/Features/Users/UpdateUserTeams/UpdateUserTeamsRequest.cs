namespace Auth.Application.Features.Users.UpdateUserTeams;

public record UpdateUserTeamsRequest(List<Guid> TeamIds);
