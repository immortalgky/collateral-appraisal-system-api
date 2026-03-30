namespace Auth.Application.Features.Groups.GetGroupById;

public record GroupUserDto(Guid UserId);

public record GroupMonitoringDto(Guid MonitoredGroupId, string MonitoredGroupName);

public record GetGroupByIdResult(
    Guid Id,
    string Name,
    string Description,
    string Scope,
    Guid? CompanyId,
    List<GroupUserDto> Users,
    List<GroupMonitoringDto> MonitoredGroups);
