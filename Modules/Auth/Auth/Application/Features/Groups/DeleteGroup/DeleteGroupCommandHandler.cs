using Auth.Application.Services;

namespace Auth.Application.Features.Groups.DeleteGroup;

public class DeleteGroupCommandHandler(IGroupService groupService)
    : ICommandHandler<DeleteGroupCommand>
{
    public async Task<Unit> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        await groupService.DeleteGroup(command.Id, command.DeletedBy, cancellationToken);
        return Unit.Value;
    }
}
