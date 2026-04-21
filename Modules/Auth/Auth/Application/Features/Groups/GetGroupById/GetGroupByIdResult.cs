namespace Auth.Application.Features.Groups.GetGroupById;

public record GroupUserDto(Guid UserId, string UserName, string FirstName, string LastName);

public record GroupMonitoringDto(Guid GroupId, string GroupName);

public record GetGroupByIdResult(
    Guid Id,
    string Name,
    string Description,
    string Scope,
    Guid? CompanyId,
    List<GroupUserDto> Users,
    List<GroupMonitoringDto> MonitoredGroups);
