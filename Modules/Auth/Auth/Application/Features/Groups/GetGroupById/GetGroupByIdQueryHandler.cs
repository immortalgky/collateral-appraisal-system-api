using Auth.Application.Services;

namespace Auth.Application.Features.Groups.GetGroupById;

public class GetGroupByIdQueryHandler(IGroupService groupService)
    : IQueryHandler<GetGroupByIdQuery, GetGroupByIdResult>
{
    public async Task<GetGroupByIdResult> Handle(GetGroupByIdQuery query, CancellationToken cancellationToken)
    {
        var group = await groupService.GetGroupById(query.Id, cancellationToken);
        if (group is null) throw new KeyNotFoundException($"Group {query.Id} not found.");

        var users = group.Users.Select(u => new GroupUserDto(u.UserId)).ToList();
        var monitoredGroups = group.MonitoredGroups
            .Select(mg => new GroupMonitoringDto(mg.MonitoredGroupId, mg.MonitoredGroup.Name))
            .ToList();

        return new GetGroupByIdResult(
            group.Id, group.Name, group.Description, group.Scope, group.CompanyId,
            users, monitoredGroups);
    }
}
