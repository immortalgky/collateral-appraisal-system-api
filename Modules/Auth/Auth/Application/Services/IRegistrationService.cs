using Auth.Domain.Identity;

namespace Auth.Services;

public interface IRegistrationService
{
    public Task<ApplicationUser> RegisterUser(
        RegisterUserDto registerUserDto,
        CancellationToken cancellationToken = default
    );
}
