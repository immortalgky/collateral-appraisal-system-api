namespace Auth.Application.Features.Teams.UpdateTeamUsers;

public record UpdateTeamUsersRequest(List<Guid> UserIds);
