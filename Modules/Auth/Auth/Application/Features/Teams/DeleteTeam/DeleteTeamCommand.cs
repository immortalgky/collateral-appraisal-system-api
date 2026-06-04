namespace Auth.Application.Features.Teams.DeleteTeam;

public record DeleteTeamCommand(Guid Id) : ICommand;
