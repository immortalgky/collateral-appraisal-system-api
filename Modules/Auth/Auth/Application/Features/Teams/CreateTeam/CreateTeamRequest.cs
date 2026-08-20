using Auth.Domain;

namespace Auth.Application.Features.Teams.CreateTeam;

public record CreateTeamRequest(string Name, string Scope = AuthScopes.Bank, string? Description = null);
