using Auth.Domain.Groups;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.UpdateUserGroups;

public class UpdateUserGroupsCommandHandler(
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext)
    : ICommandHandler<UpdateUserGroupsCommand>
{
    public async Task<Unit> Handle(UpdateUserGroupsCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        var requestedGroupIds = (command.GroupIds ?? []).Distinct().ToList();

        var existingGroupIds = await dbContext.Groups
            .Where(g => requestedGroupIds.Contains(g.Id))
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        var missing = requestedGroupIds.Except(existingGroupIds).ToList();
        if (missing.Count > 0)
            throw new NotFoundException("Group", missing[0]);

        var currentLinks = await dbContext.GroupUsers
            .Where(gu => gu.UserId == command.UserId)
            .ToListAsync(cancellationToken);

        var currentGroupIds = currentLinks.Select(l => l.GroupId).ToList();

        var toRemove = currentLinks.Where(l => !requestedGroupIds.Contains(l.GroupId)).ToList();
        if (toRemove.Count > 0)
            dbContext.GroupUsers.RemoveRange(toRemove);

        foreach (var groupId in requestedGroupIds.Except(currentGroupIds))
            dbContext.GroupUsers.Add(new GroupUser { GroupId = groupId, UserId = command.UserId });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
