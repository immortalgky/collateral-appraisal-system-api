using Auth.Application.Services;

namespace Auth.Application.Features.Groups.UpdateGroup;

public class UpdateGroupCommandHandler(IGroupService groupService)
    : ICommandHandler<UpdateGroupCommand>
{
    public async Task<Unit> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
    {
        await groupService.UpdateGroup(command.Id, command.Name, command.Description, cancellationToken);
        return Unit.Value;
    }
}
