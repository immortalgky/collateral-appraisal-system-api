using Auth.Services;
using OAuth2OpenId.Identity.Dtos;

namespace Auth.Auth.Features.RegisterUser;

public class RegisterUserCommandHandler(IRegistrationService registrationService)
    : ICommandHandler<RegisterUserCommand, RegisterUserResult>
{
    public async Task<RegisterUserResult> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var registerUserDto = command.Adapt<RegisterUserDto>();
        var user = await registrationService.RegisterUser(registerUserDto);
        var userId = user.Id;
        return new RegisterUserResult(userId);
    }
}
