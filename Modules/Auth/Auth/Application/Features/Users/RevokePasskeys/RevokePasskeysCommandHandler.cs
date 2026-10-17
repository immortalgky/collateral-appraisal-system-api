using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Domain.Sessions;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.RevokePasskeys;

/// <summary>For the user who lost a device: every passkey removed, every session and IdP cookie ended. Two-factor settings are not touched.</summary>
public class RevokePasskeysCommandHandler(
    UserManager<ApplicationUser> userManager,
    AuthDbContext dbContext,
    IUserSessionService sessions,
    IAuthAuditWriter auditWriter)
    : ICommandHandler<RevokePasskeysCommand, RevokePasskeysResult>
{
    public async Task<RevokePasskeysResult> Handle(RevokePasskeysCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
                   ?? throw new NotFoundException("User", command.UserId);

        // Rotating the stamp ends every IdP (SSO) cookie of the user, which a lost device may still hold: without it the cookie
        // could go on minting authorization codes. Remember-device and parked sign-in cookies die with it. Same as Reset 2FA.
        var stamped = await userManager.UpdateSecurityStampAsync(user);
        if (!stamped.Succeeded)
            throw new BadRequestException("The passkeys could not be removed. Please contact your administrator.");

        var removed = await dbContext.UserPasskeys.Where(p => p.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        var ended = await sessions.RevokeUserAsync(user.Id, user.UserName, SessionEndReason.Revoked,
            cancellationToken: cancellationToken);

        auditWriter.Record(AuditAction.PasskeysRevoked, AuditEntityType.User, user.Id, user.UserName, new { removed });
        return new RevokePasskeysResult(ended);
    }
}
