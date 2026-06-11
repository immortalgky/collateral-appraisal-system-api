using Auth.Application.Services;

namespace Auth.Application.Features.Teams.CreateTeam;

public class CreateTeamCommandHandler(ITeamService teamService)
    : ICommandHandler<CreateTeamCommand, CreateTeamResult>
{
    public async Task<CreateTeamResult> Handle(CreateTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await teamService.CreateTeam(
            command.Name,
            command.Scope,
            command.Description,
            cancellationToken);

        return new CreateTeamResult(team.Id);
    }
}
