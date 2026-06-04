using Auth.Application.Services;

namespace Auth.Application.Features.Teams.DeleteTeam;

public class DeleteTeamCommandHandler(ITeamService teamService)
    : ICommandHandler<DeleteTeamCommand>
{
    public async Task<Unit> Handle(DeleteTeamCommand command, CancellationToken cancellationToken)
    {
        await teamService.DeleteTeam(command.Id, cancellationToken);
        return Unit.Value;
    }
}
