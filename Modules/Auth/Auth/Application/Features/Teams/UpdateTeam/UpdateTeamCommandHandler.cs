using Auth.Application.Services;

namespace Auth.Application.Features.Teams.UpdateTeam;

public class UpdateTeamCommandHandler(ITeamService teamService)
    : ICommandHandler<UpdateTeamCommand>
{
    public async Task<Unit> Handle(UpdateTeamCommand command, CancellationToken cancellationToken)
    {
        await teamService.UpdateTeam(command.Id, command.Name, command.Type, command.IsActive, cancellationToken);
        return Unit.Value;
    }
}
