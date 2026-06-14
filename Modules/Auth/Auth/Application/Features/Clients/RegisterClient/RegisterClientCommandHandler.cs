using System.Security.Cryptography;
using FluentValidation;
using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.Clients.RegisterClient;

public record RegisterClientCommand(
    string? ClientId,
    string DisplayName,
    string ClientType,
    List<Uri> RedirectUris,
    List<Uri> PostLogoutRedirectUris,
    List<string> GrantTypes,
    List<string> Scopes
) : ICommand<RegisterClientResult>;

/// <summary>ClientSecret is only ever returned here, once, at registration time.</summary>
public record RegisterClientResult(string Id, string ClientId, string? ClientSecret);

public class RegisterClientCommandValidator : AbstractValidator<RegisterClientCommand>
{
    public RegisterClientCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ClientType)
            .Must(t => t.Equals("public", StringComparison.OrdinalIgnoreCase)
                       || t.Equals("confidential", StringComparison.OrdinalIgnoreCase))
            .WithMessage("ClientType must be 'public' or 'confidential'.");
        RuleFor(x => x.GrantTypes)
            .NotEmpty().WithMessage("At least one grant type is required.");
        RuleForEach(x => x.GrantTypes)
            .Must(ClientValidationRules.IsKnownGrantType)
            .WithMessage("Unknown grant type.");
        // Authorization-code flow is meaningless without a redirect URI.
        RuleFor(x => x.RedirectUris)
            .NotEmpty()
            .When(x => x.GrantTypes.Contains(
                ClientPermissionMapper.GrantAuthorizationCode, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Redirect URIs are required for the authorization_code flow.");
        RuleForEach(x => x.RedirectUris)
            .Must(ClientValidationRules.IsAbsoluteHttpUri)
            .WithMessage("Redirect URIs must be absolute http(s) URLs.");
        RuleForEach(x => x.PostLogoutRedirectUris)
            .Must(ClientValidationRules.IsAbsoluteHttpUri)
            .WithMessage("Post-logout redirect URIs must be absolute http(s) URLs.");
    }
}

public class RegisterClientCommandHandler(IOpenIddictApplicationManager applicationManager)
    : ICommandHandler<RegisterClientCommand, RegisterClientResult>
{
    public async Task<RegisterClientResult> Handle(RegisterClientCommand command, CancellationToken cancellationToken)
    {
        var clientId = string.IsNullOrWhiteSpace(command.ClientId)
            ? Guid.NewGuid().ToString()
            : command.ClientId.Trim();

        if (await applicationManager.FindByClientIdAsync(clientId, cancellationToken) is not null)
            throw new ConflictException("Client", clientId);

        var clientType = ClientPermissionMapper.NormalizeClientType(command.ClientType);

        // Server mints the secret for confidential clients; public (PKCE) clients have none.
        var clientSecret = ClientPermissionMapper.IsConfidential(clientType)
            ? RandomNumberGenerator.GetHexString(40)
            : null;

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = command.DisplayName.Trim(),
            ClientType = clientType
        };

        ClientPermissionMapper.ApplyToDescriptor(
            descriptor, clientType, command.GrantTypes, command.Scopes,
            command.RedirectUris, command.PostLogoutRedirectUris);

        var created = await applicationManager.CreateAsync(descriptor, cancellationToken);
        var id = await applicationManager.GetIdAsync(created, cancellationToken) ?? "";

        return new RegisterClientResult(id, clientId, clientSecret);
    }
}
