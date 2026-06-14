using System.Security.Cryptography;
using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.Clients.RotateClientSecret;

public record RotateClientSecretCommand(string Id) : ICommand<RotateClientSecretResult>;

/// <summary>The freshly generated secret — shown to the admin once, never stored in plaintext.</summary>
public record RotateClientSecretResult(string ClientId, string ClientSecret);

public class RotateClientSecretCommandHandler(IOpenIddictApplicationManager applicationManager)
    : ICommandHandler<RotateClientSecretCommand, RotateClientSecretResult>
{
    public async Task<RotateClientSecretResult> Handle(
        RotateClientSecretCommand command,
        CancellationToken cancellationToken)
    {
        var app = await applicationManager.FindByIdAsync(command.Id, cancellationToken)
                  ?? throw new NotFoundException("Client", command.Id);

        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, app, cancellationToken);

        // Seeded core clients (spa/los/cls) back live integrations whose secret is coordinated
        // out-of-band — block silent UI rotation, mirroring the DeleteClient guard.
        if (ClientPermissionMapper.SystemClientIds.Contains(descriptor.ClientId ?? ""))
            throw new ConflictException(
                $"Client '{descriptor.ClientId}' is a system client; its secret cannot be rotated from the UI.");

        if (!ClientPermissionMapper.IsConfidential(descriptor.ClientType ?? ""))
            throw new BadRequestException("Only confidential clients have a secret to rotate.");

        var newSecret = RandomNumberGenerator.GetHexString(40);
        descriptor.ClientSecret = newSecret;

        await applicationManager.UpdateAsync(app, descriptor, cancellationToken);

        return new RotateClientSecretResult(descriptor.ClientId ?? "", newSecret);
    }
}
