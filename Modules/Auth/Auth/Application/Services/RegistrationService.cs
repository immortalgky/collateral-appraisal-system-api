using Microsoft.AspNetCore.Identity;
using Auth.Application.Services;
using Auth.Infrastructure.Repository;
using Auth.Domain.Identity;
using Shared.Exceptions;

namespace Auth.Services;

public class RegistrationService(
    UserManager<ApplicationUser> userManager,
    IPermissionRepository permissionRepository,
    RoleManager<ApplicationRole> roleManager,
    IPasswordHistoryRecorder passwordHistoryRecorder
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
            AuthSource = registerUserDto.AuthSource,
            // Make the account lockable per-row so failed-attempt lockout actually engages — including
            // the LDAP login path, where UserManager.AccessFailedAsync only locks when this flag is set.
            LockoutEnabled = true,
            // Local accounts are created with an admin-set password — force the user to choose
            // their own on first login. LDAP accounts authenticate against AD (no local password).
            MustChangePassword = !AuthSources.IsLdap(registerUserDto.AuthSource),
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
        var result = AuthSources.IsLdap(registerUserDto.AuthSource)
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