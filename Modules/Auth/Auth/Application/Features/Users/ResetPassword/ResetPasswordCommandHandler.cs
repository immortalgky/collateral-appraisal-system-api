using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.ResetPassword;

public class ResetPasswordCommandHandler(
    UserManager<ApplicationUser> userManager,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
    : ICommandHandler<ResetPasswordCommand>
{
    public async Task<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        if (AuthSources.IsLdap(user.AuthSource))
            throw new InvalidOperationException("Password reset is only available for local accounts.");

        // Admin reset: remove existing password and set new one
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, command.NewPassword);

        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        // Force the user to change their password on next login. We deliberately do NOT record the
        // admin-set temporary password into history or re-stamp PasswordChangedAt: the temp is a
        // throwaway that the user must replace immediately, so it should not consume a reuse slot or
        // reset the expiry clock. DbPasswordValidator still blocks reusing the current (temp) hash,
        // and the real change (ChangePassword → RecordAsync) stamps history/PasswordChangedAt.
        user.MustChangePassword = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", updateResult.Errors.Select(e => e.Description)));

        auditWriter.Record(
            AuditAction.Updated,
            AuditEntityType.User,
            command.UserId,
            user.UserName,
            new { action = "passwordReset" });
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
