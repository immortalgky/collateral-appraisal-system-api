using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Sessions;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.ResetTwoFactor;

/// <summary>For the user who lost their phone: no authenticator key, two-factor off, no recovery codes, signed out everywhere.</summary>
public class ResetTwoFactorCommandHandler(
    UserManager<ApplicationUser> userManager,
    TwoFactorService twoFactor,
    IUserSessionService sessions,
    IAuthAuditWriter auditWriter)
    : ICommandHandler<ResetTwoFactorCommand, ResetTwoFactorResult>
{
    public async Task<ResetTwoFactorResult> Handle(ResetTwoFactorCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        // Rotates the security stamp, which also ends every "remember this device" cookie.
        await twoFactor.ResetAsync(user);
        var ended = await sessions.RevokeUserAsync(user.Id, user.UserName, SessionEndReason.Revoked,
            cancellationToken: cancellationToken);

        auditWriter.Record(AuditAction.TwoFactorReset, AuditEntityType.User, user.Id, user.UserName);
        return new ResetTwoFactorResult(ended);
    }
}
