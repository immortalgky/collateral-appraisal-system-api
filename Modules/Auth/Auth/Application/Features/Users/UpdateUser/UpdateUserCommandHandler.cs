using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Shared.Exceptions;

namespace Auth.Application.Features.Users.UpdateUser;

public class UpdateUserCommandHandler(
    UserManager<ApplicationUser> userManager,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
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
        // AO Code is a bank-internal attribute — only persist it for bank users (no company);
        // company users must never carry one.
        user.AoCode = command.CompanyId is null ? command.AoCode : null;

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

            user.AuthSource = command.AuthSource;
            if (AuthSources.IsLdap(command.AuthSource))
                user.MustChangePassword = false;
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
