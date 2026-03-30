namespace Auth.Application.Features.Groups.UpdateGroupMonitoring;

public record UpdateGroupMonitoringCommand(Guid Id, List<Guid> MonitoredGroupIds) : ICommand;
