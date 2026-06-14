using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.OAuthTokens.RevokeAuthorization;

public record RevokeAuthorizationCommand(string Id) : ICommand;

public class RevokeAuthorizationCommandHandler(IOpenIddictAuthorizationManager authorizationManager)
    : ICommandHandler<RevokeAuthorizationCommand>
{
    public async Task<Unit> Handle(RevokeAuthorizationCommand command, CancellationToken cancellationToken)
    {
        var authorization = await authorizationManager.FindByIdAsync(command.Id, cancellationToken)
                            ?? throw new NotFoundException("Authorization", command.Id);

        // Revoking the authorization also cascades revocation to its tokens.
        await authorizationManager.TryRevokeAsync(authorization, cancellationToken);
        return Unit.Value;
    }
}
