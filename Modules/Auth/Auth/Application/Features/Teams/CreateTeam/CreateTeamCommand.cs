namespace Auth.Application.Features.Teams.CreateTeam;

public record CreateTeamCommand(string Name, string Scope, string? Description = null)
    : ICommand<CreateTeamResult>;
