using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Groups;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.UpdateUserGroups;

public class UpdateUserGroupsCommandHandler(
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext,
    IAuthAuditWriter auditWriter)
    : ICommandHandler<UpdateUserGroupsCommand>
{
    public async Task<Unit> Handle(UpdateUserGroupsCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        var requestedGroupIds = (command.GroupIds ?? []).Distinct().ToList();

        await UserAssignmentValidator.ValidateGroupsExistAsync(dbContext, requestedGroupIds, cancellationToken);

        var currentLinks = await dbContext.GroupUsers
            .Where(gu => gu.UserId == command.UserId)
            .ToListAsync(cancellationToken);

        var currentGroupIds = currentLinks.Select(l => l.GroupId).ToList();

        var toRemove = currentLinks.Where(l => !requestedGroupIds.Contains(l.GroupId)).ToList();
        if (toRemove.Count > 0)
            dbContext.GroupUsers.RemoveRange(toRemove);

        foreach (var groupId in requestedGroupIds.Except(currentGroupIds))
            dbContext.GroupUsers.Add(new GroupUser { GroupId = groupId, UserId = command.UserId });

        auditWriter.RecordAssignmentChange(
            AuditEntityType.User,
            command.UserId,
            user.UserName,
            currentGroupIds,
            requestedGroupIds,
            "groups");
        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
