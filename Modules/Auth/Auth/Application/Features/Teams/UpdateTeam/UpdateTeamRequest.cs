namespace Auth.Application.Features.Teams.UpdateTeam;

public record UpdateTeamRequest(string Name, string Scope, string? Description = null);
