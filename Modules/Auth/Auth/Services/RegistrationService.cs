using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using OAuth2OpenId.Data.Repository;
using OAuth2OpenId.Identity.Models;
using OpenIddict.Abstractions;
using Shared.Exceptions;

namespace Auth.Services;

public class RegistrationService(
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager applicationManager,
    IPermissionReadRepository permissionReadRepository,
    RoleManager<ApplicationRole> roleManager
) : IRegistrationService
{
    public async Task<ApplicationUser> RegisterUser(
        RegisterUserDto registerUserDto,
        CancellationToken cancellationToken = default
    )
    {
        await ValidateUserPermission(registerUserDto, cancellationToken);
        var roleNames = await GetRoleNames(registerUserDto);

        var user = new ApplicationUser
        {
            UserName = registerUserDto.Username,
            Email = registerUserDto.Email,
            Permissions =
            [
                .. registerUserDto.Permissions.Select(permissionId => new UserPermission
                {
                    PermissionId = permissionId,
                }),
            ],
        };

        var result = await userManager.CreateAsync(user, registerUserDto.Password);
        HandleIdentityResult(result);

        var roleResult = await userManager.AddToRolesAsync(user, roleNames);
        HandleIdentityResult(roleResult);

        return user;
    }

    private async Task ValidateUserPermission(
        RegisterUserDto registerUserDto,
        CancellationToken cancellationToken
    )
    {
        foreach (var permissionId in registerUserDto.Permissions)
        {
            var isPermissionExisted = await permissionReadRepository.ExistsAsync(
                permissionId,
                cancellationToken
            );
            if (!isPermissionExisted)
            {
                throw new NotFoundException("Permission", permissionId);
            }
        }
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
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description).ToList())
            );
        }
    }

    public async Task<OpenIddictApplicationDescriptor> RegisterClient(
        RegisterClientDto registerClientDto
    )
    {
        var clientId = Guid.NewGuid().ToString();
        string? clientSecret = null;
        if (registerClientDto.ClientType == OpenIddictConstants.ClientTypes.Confidential)
        {
            clientSecret = RandomNumberGenerator.GetHexString(30);
        }

        var applicationDescriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = registerClientDto.DisplayName,
            ClientType = registerClientDto.ClientType,
            PostLogoutRedirectUris = { },
            RedirectUris = { },
            Permissions = { },
            Requirements = { },
        };

        applicationDescriptor.PostLogoutRedirectUris.UnionWith(
            registerClientDto.PostLogoutRedirectUris
        );
        applicationDescriptor.RedirectUris.UnionWith(registerClientDto.RedirectUris);
        applicationDescriptor.Permissions.UnionWith(registerClientDto.Permissions);
        applicationDescriptor.Requirements.UnionWith(registerClientDto.Requirements);

        await applicationManager.CreateAsync(applicationDescriptor);
        return applicationDescriptor;
    }
}
