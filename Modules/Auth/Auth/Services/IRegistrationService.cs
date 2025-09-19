using OAuth2OpenId.Identity.Dtos;
using OAuth2OpenId.Identity.Models;
using OpenIddict.Abstractions;

namespace Auth.Services;

public interface IRegistrationService
{
    public Task<ApplicationUser> RegisterUser(RegisterUserDto registerUserDto);
    public Task<OpenIddictApplicationDescriptor> RegisterClient(
        RegisterClientDto registerClientDto
    );
}
