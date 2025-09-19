using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using OAuth2OpenId.Identity.Dtos;
using OAuth2OpenId.Identity.Models;
using OpenIddict.Abstractions;
using Shared.Exceptions;

namespace Auth.Services;

public class RegistrationService(
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager manager
) : IRegistrationService
{
    public async Task<ApplicationUser> RegisterUser(RegisterUserDto registerUserDto)
    {
        var user = new ApplicationUser
        {
            UserName = registerUserDto.Username,
            Email = registerUserDto.Email,
            Permissions =
            [
                .. registerUserDto.Permissions.Select(permission => new UserPermission
                {
                    PermissionName = permission,
                }),
            ],
        };
        var result = await userManager.CreateAsync(user, registerUserDto.Password);

        if (!result.Succeeded)
        {
            throw new BadRequestException(
                string.Join("; ", result.Errors.Select(error => error.Description).ToList())
            );
        }

        return user;
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

        await manager.CreateAsync(applicationDescriptor);
        return applicationDescriptor;
    }
}
