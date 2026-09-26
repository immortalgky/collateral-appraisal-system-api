using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;
using Shared.Time;

namespace Auth.Application.Features.Users.UpdateUser;

public class UpdateUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateUserCommand>
{
    public async Task<Unit> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(command.Id.ToString())
            ?? throw new NotFoundException("User", command.Id);

        user.FirstName = command.FirstName;
        user.LastName = command.LastName;
        user.Position = command.Position;
        user.Department = command.Department;
        user.CompanyId = command.CompanyId;
        // AO Code and Employee ID are bank-internal attributes — only persist them for bank users
        // (no company); company users must never carry one.
        user.AoCode = command.CompanyId is null ? command.AoCode : null;
        user.EmployeeId = command.CompanyId is null ? command.EmployeeId : null;

        // AuthSource changes only when explicitly sent (null = unchanged) and only to a different
        // value. Switching an LDAP account to Local would strand it — the account has no local
        // password hash, so it could never sign in. Block that flip; an admin must create a fresh
        // local account instead. Local→LDAP is allowed (validator already ensures LDAP is enabled);
        // clear MustChangePassword since the AD password now governs.
        if (command.AuthSource is not null && !string.Equals(command.AuthSource, user.AuthSource, StringComparison.OrdinalIgnoreCase))
        {
            if (AuthSources.IsLdap(user.AuthSource) && !AuthSources.IsLdap(command.AuthSource))
                throw new BadRequestException(
                    "Cannot switch an LDAP account to Local: it has no local password and could never sign in.");

            // A temporary-access account lives on its access window, and opening, extending or
            // closing one rotates a LOCAL password. Flip it to LDAP and every one of those calls is
            // refused — including the close, which would strand an account with an open window and
            // a live password that nothing short of deactivation can take back.
            var staysTemporary = command.IsTemporaryAccess ?? user.IsTemporaryAccess;
            if (staysTemporary && AuthSources.IsLdap(command.AuthSource))
                throw new BadRequestException(
                    "A temporary-access account must stay on local authentication. Clear the temporary-access flag first.");

            user.AuthSource = command.AuthSource;
            if (AuthSources.IsLdap(command.AuthSource))
                user.MustChangePassword = false;
        }

        // Converting an account to or from temporary-access. Only touched when a value is sent
        // (null = unchanged), and each direction has one thing that must move with the flag:
        //
        //  - On: the account becomes usable only inside a window, so close it now. It keeps whatever
        //    password it had, which no longer opens anything — the first window issues a new one.
        //    An account that already has an open window keeps it rather than being shut mid-job.
        //  - Off: clear the expiry, or the account stays permanently past its last window with no
        //    endpoint left to fix it (the access-window endpoint refuses accounts without the flag).
        //    Demand a password change too: the last password it holds was a throwaway an admin read
        //    off a screen, and whoever inherits this account should set their own.
        if (command.IsTemporaryAccess is { } temporaryAccess && temporaryAccess != user.IsTemporaryAccess)
        {
            if (temporaryAccess && AuthSources.IsLdap(user.AuthSource))
                throw new BadRequestException(
                    "Only a local account can be made temporary-access: opening a window issues a local password.");

            user.IsTemporaryAccess = temporaryAccess;
            if (temporaryAccess)
            {
                // "Already has an open window" is a window whose end is still ahead — NOT merely a
                // usable account. An ordinary account has no expiry at all and is therefore usable,
                // so testing usability here would leave it wide open under its new flag.
                var now = dateTimeProvider.ApplicationNow;
                var hasOpenWindow = user.AccessExpiresAt is { } windowEnd && windowEnd > now;
                if (!hasOpenWindow) user.AccessExpiresAt = now;
            }
            else
            {
                user.AccessExpiresAt = null;
                user.MustChangePassword = true;
            }

            auditWriter.Record(
                AuditAction.Updated, AuditEntityType.User, command.Id, user.UserName,
                new { action = temporaryAccess ? "temporaryAccessEnabled" : "temporaryAccessDisabled" });
        }

        // Set email + its normalized form; UpdateAsync runs the Identity UserValidator which rejects
        // a duplicate email because RequireUniqueEmail is enabled (see AuthModule).
        user.Email = command.Email;
        user.NormalizedEmail = userManager.NormalizeEmail(command.Email);

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        auditWriter.Record(AuditAction.Updated, AuditEntityType.User, command.Id, user.UserName);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
