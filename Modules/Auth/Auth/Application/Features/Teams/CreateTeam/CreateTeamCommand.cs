namespace Auth.Application.Features.Teams.CreateTeam;

public record CreateTeamCommand(string Name, string Type, bool IsActive)
    : ICommand<CreateTeamResult>;
