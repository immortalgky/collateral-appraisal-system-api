using Auth.Application.Services;

namespace Auth.Application.Features.Groups.UpdateGroupUsers;

public class UpdateGroupUsersCommandHandler(IGroupService groupService)
    : ICommandHandler<UpdateGroupUsersCommand>
{
    public async Task<Unit> Handle(UpdateGroupUsersCommand command, CancellationToken cancellationToken)
    {
        await groupService.UpdateGroupUsers(command.Id, command.UserIds, cancellationToken);
        return Unit.Value;
    }
}
