using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;
using Shared.Time;

namespace Auth.Application.Features.Users.SetAccessWindow;

public class SetAccessWindowCommandHandler(
    UserManager<ApplicationUser> userManager,
    IPasswordPolicyProvider passwordPolicyProvider,
    IAuthAuditWriter auditWriter,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SetAccessWindowCommand, SetAccessWindowResult>
{
    public async Task<SetAccessWindowResult> Handle(
        SetAccessWindowCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.UserId.ToString())
            ?? throw new NotFoundException("User", command.UserId);

        // Only accounts flagged as temporary-access may be driven this way. Without this gate the
        // endpoint would be a "rotate any colleague's password and lock them out on a timer" button.
        if (!user.IsTemporaryAccess)
            throw new BadRequestException(
                "Access windows can only be opened on accounts marked as temporary-access.");

        // Opening a window means issuing a new password, which only exists for local accounts —
        // an LDAP account's password lives in AD and we cannot rotate it.
        if (AuthSources.IsLdap(user.AuthSource))
            throw new BadRequestException("Access windows are only available for local accounts.");

        var now = dateTimeProvider.ApplicationNow;
        var closing = command.ExpiresAt <= now;
        var policy = await passwordPolicyProvider.GetAsync(cancellationToken);

        if (!closing)
        {
            if (command.ExpiresAt > now.AddHours(policy.MaxAccessWindowHours))
                throw new BadRequestException(
                    $"An access window cannot be longer than {policy.MaxAccessWindowHours} hours. "
                    + "Raise the limit in the password policy first if a longer window is really needed.");
        }

        if (command.ExtendOnly)
        {
            // Extending is only meaningful while the window is still open: once it has lapsed the
            // password is the thing standing between the account and anyone who wrote it down, so the
            // way back in is a fresh window with a fresh password, not a nudge to the clock.
            if (!user.IsUsable(now))
                throw new BadRequestException(
                    "This account has no open access window to extend. Open a new one instead.");

            if (closing)
                throw new BadRequestException("An extension must move the expiry into the future.");
        }

        // UserManager and this handler share the request's AuthDbContext, so every field set here is
        // persisted alongside the audit row when the transactional behavior saves. On the rotate path
        // ResetPasswordAsync saves first, through the Identity user store — same transaction, so a
        // later failure still takes the whole thing back.
        user.AccessExpiresAt = command.ExpiresAt;

        string? password = null;
        if (!command.ExtendOnly)
        {
            var generated = TemporaryPasswordGenerator.Generate(policy);

            // This password IS the credential for the window, not a temporary one to be replaced on
            // first login — so, unlike an admin reset, do not demand a change (that would bounce the
            // account straight into the change-password screen) and do restamp the expiry clock so
            // password-age rules cannot retire a window that was issued seconds ago. Set before the
            // reset, which is what writes the row.
            user.MustChangePassword = false;
            user.PasswordChangedAt = now;

            // ResetPasswordAsync rotates the security stamp, which invalidates the account's existing
            // Identity cookie — so a window that is being closed really does end the session, and a
            // password written down during the previous window stops working the moment a new one opens.
            // It saves through the Identity user store, whose UserValidator rejects a duplicate email
            // (RequireUniqueEmail): such an account cannot have a window opened or closed until the
            // duplicate is resolved. Extending, which touches no password, is unaffected.
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, generated);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

            // A closed window's password is dead on arrival — rotating it is the point, handing it
            // back is not. Only an open window returns one.
            password = closing ? null : generated;
        }

        auditWriter.Record(
            AuditAction.Updated,
            AuditEntityType.User,
            command.UserId,
            user.UserName,
            new
            {
                action = closing ? "accessWindowClosed" : command.ExtendOnly ? "accessWindowExtended" : "accessWindowOpened",
                expiresAt = command.ExpiresAt,
                reason = command.Reason,
                passwordRotated = !command.ExtendOnly
            });

        return new SetAccessWindowResult(command.ExpiresAt, password);
    }
}
