using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.OAuthTokens.RevokeToken;

public record RevokeTokenCommand(string Id) : ICommand;

public class RevokeTokenCommandHandler(IOpenIddictTokenManager tokenManager)
    : ICommandHandler<RevokeTokenCommand>
{
    public async Task<Unit> Handle(RevokeTokenCommand command, CancellationToken cancellationToken)
    {
        var token = await tokenManager.FindByIdAsync(command.Id, cancellationToken)
                    ?? throw new NotFoundException("Token", command.Id);

        await tokenManager.TryRevokeAsync(token, cancellationToken);
        return Unit.Value;
    }
}
