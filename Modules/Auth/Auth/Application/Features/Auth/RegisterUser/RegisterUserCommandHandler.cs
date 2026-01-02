using Auth.Services;

namespace Auth.Domain.Auth.Features.RegisterUser;

public class RegisterUserCommandHandler(IRegistrationService registrationService)
    : ICommandHandler<RegisterUserCommand, RegisterUserResult>
{
    public async Task<RegisterUserResult> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken
    )
    {
        var registerUserDto = command.Adapt<RegisterUserDto>();
        var user = await registrationService.RegisterUser(registerUserDto, cancellationToken);
        var userId = user.Id;
        return new RegisterUserResult(userId);
    }
}
