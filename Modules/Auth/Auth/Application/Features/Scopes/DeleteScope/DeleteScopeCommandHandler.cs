using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.Scopes.DeleteScope;

public record DeleteScopeCommand(string Id) : ICommand;

public class DeleteScopeCommandHandler(IOpenIddictScopeManager scopeManager)
    : ICommandHandler<DeleteScopeCommand>
{
    public async Task<Unit> Handle(DeleteScopeCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeManager.FindByIdAsync(command.Id, cancellationToken)
                    ?? throw new NotFoundException("Scope", command.Id);

        await scopeManager.DeleteAsync(scope, cancellationToken);
        return Unit.Value;
    }
}
