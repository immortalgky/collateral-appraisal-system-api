using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;
using Shared.Identity;

namespace Auth.Application.Features.Users.SetUserActivation;

public class SetUserActivationCommandHandler(
    UserManager<ApplicationUser> userManager,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext,
    ICurrentUserService currentUserService)
    : ICommandHandler<SetUserActivationCommand>
{
    private const string AdminRoleName = "Admin";

    public async Task<Unit> Handle(SetUserActivationCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        if (!command.IsActive)
        {
            // Guard 1: cannot deactivate your own account
            if (command.UserId == currentUserService.UserId)
                throw new BadRequestException("You cannot deactivate your own account.");

            // Guard 2: cannot deactivate the last active Admin
            var isTargetAdmin = await userManager.IsInRoleAsync(user, AdminRoleName);
            if (isTargetAdmin)
            {
                var activeAdmins = await userManager.GetUsersInRoleAsync(AdminRoleName);
                var otherActiveAdmins = activeAdmins
                    .Where(u => u.Id != command.UserId && u.IsActive)
                    .ToList();

                if (otherActiveAdmins.Count == 0)
                    throw new BadRequestException(
                        "Cannot deactivate the last active Admin. Assign the Admin role to another active user first.");
            }
        }

        user.IsActive = command.IsActive;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        auditWriter.Record(
            AuditAction.Updated,
            AuditEntityType.User,
            command.UserId,
            user.UserName,
            new { isActive = command.IsActive });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
