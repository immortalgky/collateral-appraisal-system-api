using Microsoft.AspNetCore.Identity;
using Auth.Application.Services;
using Auth.Infrastructure.Repository;
using Auth.Domain.Identity;
using Shared.Exceptions;
using Shared.Time;

namespace Auth.Services;

public class RegistrationService(
    UserManager<ApplicationUser> userManager,
    IPermissionRepository permissionRepository,
    RoleManager<ApplicationRole> roleManager,
    IPasswordHistoryRecorder passwordHistoryRecorder,
    IDateTimeProvider dateTimeProvider
) : IRegistrationService
{
    public async Task<ApplicationUser> RegisterUser(
        RegisterUserDto registerUserDto,
        CancellationToken cancellationToken = default
    )
    {
        await PermissionService.ValidatePermissionsExistAsync(
            [.. registerUserDto.Permissions.Select(userPermission => userPermission.PermissionId)],
            permissionRepository,
            cancellationToken
        );
        var roleNames = await GetRoleNames(registerUserDto);

        var user = new ApplicationUser
        {
            UserName = registerUserDto.Username,
            Email = registerUserDto.Email,
            FirstName = registerUserDto.FirstName,
            LastName = registerUserDto.LastName,
            AvatarUrl = registerUserDto.AvatarUrl,
            Position = registerUserDto.Position,
            Department = registerUserDto.Department,
            CompanyId = registerUserDto.CompanyId,
            AoCode = registerUserDto.AoCode,
            EmployeeId = registerUserDto.EmployeeId,
            AuthSource = registerUserDto.AuthSource,
            IsTemporaryAccess = registerUserDto.IsTemporaryAccess,
            // A temporary-access account starts closed: it only becomes usable when an admin opens a
            // window, which is also what gives it its first password. Stamping "now" rather than
            // leaving this null keeps "no window has ever been opened" from reading as "no expiry".
            AccessExpiresAt = registerUserDto.IsTemporaryAccess ? dateTimeProvider.ApplicationNow : null,
            // Make the account lockable per-row so failed-attempt lockout actually engages — including
            // the LDAP login path, where UserManager.AccessFailedAsync only locks when this flag is set.
            LockoutEnabled = true,
            // Local accounts are created with an admin-set password — force the user to choose
            // their own on first login. LDAP accounts authenticate against AD (no local password).
            // Temporary accounts are exempt: each window issues its own password, and demanding a
            // change would drop the holder into the change-password screen instead of the job they
            // came to run.
            MustChangePassword = !AuthSources.IsLdap(registerUserDto.AuthSource)
                                 && !registerUserDto.IsTemporaryAccess,
            Permissions =
            [
                .. registerUserDto.Permissions.Select(userPermission => new UserPermission
                {
                    PermissionId = userPermission.PermissionId,
                    IsGranted = userPermission.IsGranted
                })
            ]
        };

        // LDAP users authenticate against AD and never use a local password hash. Create them
        // WITHOUT a password so DbPasswordValidator (which runs on every CreateAsync-with-password)
        // doesn't evaluate a synthetic secret — a random throwaway can randomly fail the policy
        // (e.g. no non-alphanumeric char), making LDAP user creation intermittently throw.
        // Created without a password when there is no password to set: LDAP accounts authenticate
        // against AD, and a temporary account has no credential until its first window is opened.
        var result = AuthSources.IsLdap(registerUserDto.AuthSource) || registerUserDto.IsTemporaryAccess
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, registerUserDto.Password);
        HandleIdentityResult(result);

        // Stamp PasswordChangedAt + seed password history (no-op for LDAP accounts).
        await passwordHistoryRecorder.RecordAsync(user, cancellationToken);

        var roleResult = await userManager.AddToRolesAsync(user, roleNames);
        HandleIdentityResult(roleResult);

        return user;
    }

    private async Task<List<string>> GetRoleNames(RegisterUserDto registerUserDto)
    {
        var roleNames = new List<string>();
        foreach (var roleId in registerUserDto.Roles)
        {
            var role =
                await roleManager.FindByIdAsync(roleId.ToString())
                ?? throw new NotFoundException("Role", roleId);
            roleNames.Add(role.Name!);
        }

        return roleNames;
    }

    private static void HandleIdentityResult(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description).ToList())
            );
    }
}