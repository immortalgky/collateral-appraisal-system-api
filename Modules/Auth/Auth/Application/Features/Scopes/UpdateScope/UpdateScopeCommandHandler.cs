using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.Scopes.UpdateScope;

public record UpdateScopeCommand(
    string Id,
    string? DisplayName,
    string? Description,
    List<string> Resources
) : ICommand;

public class UpdateScopeCommandHandler(IOpenIddictScopeManager scopeManager)
    : ICommandHandler<UpdateScopeCommand>
{
    public async Task<Unit> Handle(UpdateScopeCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeManager.FindByIdAsync(command.Id, cancellationToken)
                    ?? throw new NotFoundException("Scope", command.Id);

        var descriptor = new OpenIddictScopeDescriptor();
        await scopeManager.PopulateAsync(descriptor, scope, cancellationToken);

        descriptor.DisplayName = command.DisplayName;
        descriptor.Description = command.Description;
        descriptor.Resources.Clear();
        descriptor.Resources.UnionWith(command.Resources.Where(r => !string.IsNullOrWhiteSpace(r)));

        await scopeManager.UpdateAsync(scope, descriptor, cancellationToken);
        return Unit.Value;
    }
}
