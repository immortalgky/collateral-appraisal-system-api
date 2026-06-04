namespace Auth.Application.Features.Teams.CreateTeam;

public record CreateTeamRequest(string Name, string Type = "Internal", bool IsActive = true);
