using Auth.Services;
using OAuth2OpenId.Identity.Dtos;

namespace Auth.Auth.Features.RegisterClient;

public class RegisterClientCommandHandler(IRegistrationService registrationService)
    : ICommandHandler<RegisterClientCommand, RegisterClientResult>
{
    public async Task<RegisterClientResult> Handle(
        RegisterClientCommand command,
        CancellationToken cancellationToken
    )
    {
        var registerClientDto = command.Adapt<RegisterClientDto>();
        var client = await registrationService.RegisterClient(registerClientDto);

        var clientId = client.ClientId;
        var clientSecret = client.ClientSecret;
        return new RegisterClientResult(clientId, clientSecret);
    }
}
