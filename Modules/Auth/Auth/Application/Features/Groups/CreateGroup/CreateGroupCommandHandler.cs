using Auth.Application.Services;

namespace Auth.Application.Features.Groups.CreateGroup;

public class CreateGroupCommandHandler(IGroupService groupService)
    : ICommandHandler<CreateGroupCommand, CreateGroupResult>
{
    public async Task<CreateGroupResult> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await groupService.CreateGroup(
            command.Name,
            command.Description,
            command.Scope,
            command.CompanyId,
            cancellationToken);

        return new CreateGroupResult(group.Id);
    }
}
