namespace Auth.Application.Features.Teams.UpdateTeam;

public record UpdateTeamRequest(string Name, string Type, bool IsActive);
