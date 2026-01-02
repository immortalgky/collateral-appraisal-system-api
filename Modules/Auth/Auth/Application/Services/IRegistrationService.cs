using OAuth2OpenId.Domain.Identity.Models;
using OpenIddict.Abstractions;

namespace Auth.Services;

public interface IRegistrationService
{
    public Task<ApplicationUser> RegisterUser(
        RegisterUserDto registerUserDto,
        CancellationToken cancellationToken = default
    );
    public Task<OpenIddictApplicationDescriptor> RegisterClient(
        RegisterClientDto registerClientDto
    );
}
