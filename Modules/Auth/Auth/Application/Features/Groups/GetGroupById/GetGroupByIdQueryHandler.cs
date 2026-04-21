using Auth.Application.Services;
using Auth.Infrastructure;

namespace Auth.Application.Features.Groups.GetGroupById;

public class GetGroupByIdQueryHandler(IGroupService groupService, AuthDbContext dbContext)
    : IQueryHandler<GetGroupByIdQuery, GetGroupByIdResult>
{
    public async Task<GetGroupByIdResult> Handle(GetGroupByIdQuery query, CancellationToken cancellationToken)
    {
        var grp = await groupService.GetGroupById(query.Id, cancellationToken);
        if (grp is null) throw new KeyNotFoundException($"Group {query.Id} not found.");

        var users = await dbContext.GroupUsers
            .Where(gu => gu.GroupId == grp.Id)
            .Join(dbContext.Users, gu => gu.UserId, u => u.Id,
                (gu, u) => new GroupUserDto(u.Id, u.UserName!, u.FirstName, u.LastName))
            .ToListAsync(cancellationToken);

        var monitoredGroups = grp.MonitoredGroups
            .Select(mg => new GroupMonitoringDto(mg.MonitoredGroupId, mg.MonitoredGroup.Name))
            .ToList();

        return new GetGroupByIdResult(
            grp.Id, grp.Name, grp.Description, grp.Scope, grp.CompanyId,
            users, monitoredGroups);
    }
}
