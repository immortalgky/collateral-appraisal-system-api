using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.UnlockUser;

public class UnlockUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
    : ICommandHandler<UnlockUserCommand>
{
    public async Task<Unit> Handle(UnlockUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        var lockoutResult = await userManager.SetLockoutEndDateAsync(user, null);
        if (!lockoutResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", lockoutResult.Errors.Select(e => e.Description)));

        var resetResult = await userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", resetResult.Errors.Select(e => e.Description)));

        auditWriter.Record(
            AuditAction.Updated,
            AuditEntityType.User,
            command.UserId,
            user.UserName,
            new { action = "unlock" });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
