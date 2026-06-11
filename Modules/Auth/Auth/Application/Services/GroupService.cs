using Microsoft.EntityFrameworkCore;
using Auth.Domain.Auditing;
using Auth.Domain.Groups;
using Auth.Infrastructure;
using Auth.Infrastructure.Repository;
using Shared.Exceptions;
using Shared.Pagination;

namespace Auth.Application.Services;

public class GroupService(
    IGroupRepository groupRepository,
    AuthDbContext dbContext,
    IAuthAuditWriter auditWriter) : IGroupService
{
    public async Task<Group> CreateGroup(string name, string description, string scope, Guid? companyId, CancellationToken cancellationToken = default)
    {
        var group = Group.Create(name, description, scope, companyId);
        await groupRepository.AddAsync(group, cancellationToken);
        auditWriter.Record(AuditAction.Created, AuditEntityType.Group, group.Id, name);
        await groupRepository.SaveChangesAsync(cancellationToken);
        return group;
    }

    public async Task<PaginatedResult<Group>> GetGroups(string? search, string? scope, PaginationRequest paginationRequest, CancellationToken cancellationToken = default)
    {
        return await groupRepository.GetPaginatedAsync(search, scope, paginationRequest, cancellationToken);
    }

    public async Task<Group?> GetGroupById(Guid id, CancellationToken cancellationToken = default)
    {
        return await groupRepository.GetByIdWithDetailsAsync(id, cancellationToken);
    }

    public async Task UpdateGroup(Guid id, string name, string description, CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Group", id);

        group.Update(name, description);
        auditWriter.Record(AuditAction.Updated, AuditEntityType.Group, id, name);
        await groupRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGroupUsers(Guid id, List<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetByIdWithDetailsAsync(id, cancellationToken)
            ?? throw new NotFoundException("Group", id);

        var beforeIds = group.Users.Select(u => u.UserId).ToList();

        var existing = dbContext.GroupUsers.Where(gu => gu.GroupId == id);
        dbContext.GroupUsers.RemoveRange(existing);

        foreach (var userId in userIds)
            dbContext.GroupUsers.Add(new GroupUser { GroupId = id, UserId = userId });

        auditWriter.RecordAssignmentChange(AuditEntityType.Group, id, group.Name, beforeIds, userIds, "users");
        await groupRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateGroupMonitoring(Guid id, List<Guid> monitoredGroupIds, CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Group", id);

        var beforeIds = await dbContext.GroupMonitoring
            .Where(gm => gm.MonitorGroupId == id)
            .Select(gm => gm.MonitoredGroupId)
            .ToListAsync(cancellationToken);

        var existing = dbContext.GroupMonitoring.Where(gm => gm.MonitorGroupId == id);
        dbContext.GroupMonitoring.RemoveRange(existing);

        foreach (var monitoredGroupId in monitoredGroupIds)
            dbContext.GroupMonitoring.Add(new GroupMonitoring { MonitorGroupId = id, MonitoredGroupId = monitoredGroupId });

        auditWriter.RecordAssignmentChange(AuditEntityType.Group, id, group.Name, beforeIds, monitoredGroupIds, "monitoredGroups");
        await groupRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGroup(Guid id, Guid? deletedBy, CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Group", id);

        group.Delete(deletedBy);
        auditWriter.Record(AuditAction.Deleted, AuditEntityType.Group, id, group.Name);
        await groupRepository.SaveChangesAsync(cancellationToken);
    }
}
