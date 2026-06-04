namespace Auth.Application.Features.Teams.UpdateTeam;

public record UpdateTeamCommand(Guid Id, string Name, string Type, bool IsActive) : ICommand;
