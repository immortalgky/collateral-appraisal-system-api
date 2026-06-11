namespace Auth.Application.Features.Teams.CreateTeam;

public record CreateTeamRequest(string Name, string Scope = "Bank", string? Description = null);
