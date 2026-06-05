using Auth.Application.Services;

namespace Auth.Application.Features.Teams.UpdateTeamUsers;

public class UpdateTeamUsersCommandHandler(ITeamService teamService)
    : ICommandHandler<UpdateTeamUsersCommand>
{
    public async Task<Unit> Handle(UpdateTeamUsersCommand command, CancellationToken cancellationToken)
    {
        await teamService.UpdateTeamMembers(command.Id, command.MemberUserIds, cancellationToken);
        return Unit.Value;
    }
}
