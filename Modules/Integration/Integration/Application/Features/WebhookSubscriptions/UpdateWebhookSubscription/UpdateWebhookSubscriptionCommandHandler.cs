using FluentValidation;
using Integration.Domain.WebhookSubscriptions;
using Integration.Infrastructure.Repositories;
using MediatR;
using Shared.CQRS;
using Shared.Exceptions;
using Shared.Security;

namespace Integration.Application.Features.WebhookSubscriptions.UpdateWebhookSubscription;

/// <summary>
/// <see cref="SecretKey"/> / <see cref="ClientSecret"/> are optional — supplied only when the admin is
/// replacing the secret. When null/blank the stored secret is left untouched. SystemCode and EventType
/// are the routing key and cannot change. AuthType/HttpMethod have no default on purpose: a client that
/// omits them gets a 400 instead of silently turning a TokenBearer subscription into HMAC.
/// </summary>
public record UpdateWebhookSubscriptionCommand(
    Guid Id,
    string CallbackUrl,
    string? SecretKey,
    string AuthType,
    string HttpMethod,
    string? TokenEndpoint = null,
    string? ClientId = null,
    string? ClientSecret = null) : ICommand
{
    /// <summary>Redacts the secrets — see CreateWebhookSubscriptionCommand.ToString.</summary>
    public override string ToString() =>
        $"{nameof(UpdateWebhookSubscriptionCommand)} {{ Id = {Id}, AuthType = {AuthType}, " +
        "SecretKey = ***, ClientSecret = *** }";
}

public class UpdateWebhookSubscriptionCommandValidator : AbstractValidator<UpdateWebhookSubscriptionCommand>
{
    public UpdateWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.CallbackUrl)
            .NotEmpty().MaximumLength(500)
            .Must(WebhookUrlRules.BeAnAbsoluteHttpUrl)
            .WithMessage("CallbackUrl must be an absolute http(s) URL.");
        RuleFor(x => x.AuthType).Must(WebhookConnectionRules.BeAKnownAuthType)
            .WithMessage("AuthType must be HMAC or TokenBearer.");
        RuleFor(x => x.HttpMethod).Must(WebhookConnectionRules.BeAnAllowedMethod)
            .WithMessage("HttpMethod must be POST or PUT.");
        RuleFor(x => x.SecretKey).MaximumLength(WebhookConnectionRules.MaxSecretLength);
        RuleFor(x => x.ClientSecret).MaximumLength(WebhookConnectionRules.MaxSecretLength);

        When(x => x.AuthType == WebhookAuthType.TokenBearer,
            () => WebhookConnectionRules.AddTokenBearerRules(this, x => x.TokenEndpoint, x => x.ClientId));
    }
}

public class UpdateWebhookSubscriptionCommandHandler(
    IWebhookSubscriptionRepository repository,
    ColumnSecretCipher cipher)
    : ICommandHandler<UpdateWebhookSubscriptionCommand>
{
    public async Task<Unit> Handle(UpdateWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var subscription = await repository.GetByIdAsync(command.Id, cancellationToken)
                           ?? throw new NotFoundException("WebhookSubscription", command.Id);

        // The domain keeps the stored secret when given null; a missing one it still needs is a 400.
        // Only the secret of the chosen auth type is encrypted — the other is discarded by the domain.
        var isTokenBearer = command.AuthType == WebhookAuthType.TokenBearer;
        subscription.Update(
            command.CallbackUrl.Trim(),
            command.HttpMethod,
            command.AuthType,
            command.TokenEndpoint?.Trim(),
            command.ClientId?.Trim(),
            isTokenBearer ? null : ProtectOrKeep(command.SecretKey),
            isTokenBearer ? ProtectOrKeep(command.ClientSecret) : null);

        await repository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private string? ProtectOrKeep(string? secret) =>
        string.IsNullOrWhiteSpace(secret) ? null : cipher.Protect(secret);
}
