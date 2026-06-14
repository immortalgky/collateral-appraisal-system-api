using FluentValidation;
using OpenIddict.Abstractions;
using Shared.CQRS;
using Shared.Exceptions;

namespace Auth.Application.Features.Clients.UpdateClient;

public record UpdateClientCommand(
    string Id,
    string DisplayName,
    List<Uri> RedirectUris,
    List<Uri> PostLogoutRedirectUris,
    List<string> GrantTypes,
    List<string> Scopes
) : ICommand;

public class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GrantTypes).NotEmpty();
        RuleForEach(x => x.GrantTypes)
            .Must(ClientValidationRules.IsKnownGrantType)
            .WithMessage("Unknown grant type.");
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

public class UpdateClientCommandHandler(IOpenIddictApplicationManager applicationManager)
    : ICommandHandler<UpdateClientCommand>
{
    public async Task<Unit> Handle(UpdateClientCommand command, CancellationToken cancellationToken)
    {
        var app = await applicationManager.FindByIdAsync(command.Id, cancellationToken)
                  ?? throw new NotFoundException("Client", command.Id);

        // Read the current state into a descriptor so ClientId/ClientType/ClientSecret are preserved.
        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, app, cancellationToken);

        descriptor.DisplayName = command.DisplayName.Trim();

        ClientPermissionMapper.ApplyToDescriptor(
            descriptor,
            descriptor.ClientType ?? OpenIddictConstants.ClientTypes.Public,
            command.GrantTypes, command.Scopes,
            command.RedirectUris, command.PostLogoutRedirectUris);

        await applicationManager.UpdateAsync(app, descriptor, cancellationToken);
        return Unit.Value;
    }
}
