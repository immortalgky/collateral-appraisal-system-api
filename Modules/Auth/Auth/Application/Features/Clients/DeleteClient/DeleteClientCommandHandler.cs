using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.Clients.DeleteClient;

public record DeleteClientCommand(string Id) : ICommand;

public class DeleteClientCommandHandler(IOpenIddictApplicationManager applicationManager)
    : ICommandHandler<DeleteClientCommand>
{
    public async Task<Unit> Handle(DeleteClientCommand command, CancellationToken cancellationToken)
    {
        var app = await applicationManager.FindByIdAsync(command.Id, cancellationToken)
                  ?? throw new NotFoundException("Client", command.Id);

        var clientId = await applicationManager.GetClientIdAsync(app, cancellationToken) ?? "";

        // The seeded core clients (spa/los/cls) back live integrations — block hard-delete.
        if (ClientPermissionMapper.SystemClientIds.Contains(clientId))
            throw new ConflictException(
                $"Client '{clientId}' is a system client and cannot be deleted.");

        await applicationManager.DeleteAsync(app, cancellationToken);
        return Unit.Value;
    }
}
