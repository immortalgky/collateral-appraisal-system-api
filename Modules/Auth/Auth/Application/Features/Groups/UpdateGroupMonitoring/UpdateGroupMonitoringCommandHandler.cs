using Auth.Application.Services;

namespace Auth.Application.Features.Groups.UpdateGroupMonitoring;

public class UpdateGroupMonitoringCommandHandler(IGroupService groupService)
    : ICommandHandler<UpdateGroupMonitoringCommand>
{
    public async Task<Unit> Handle(UpdateGroupMonitoringCommand command, CancellationToken cancellationToken)
    {
        await groupService.UpdateGroupMonitoring(command.Id, command.MonitoredGroupIds, cancellationToken);
        return Unit.Value;
    }
}
