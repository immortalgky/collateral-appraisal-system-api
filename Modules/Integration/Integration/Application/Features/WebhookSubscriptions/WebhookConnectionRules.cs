using FluentValidation;
using Integration.Domain.WebhookSubscriptions;

namespace Integration.Application.Features.WebhookSubscriptions;

/// <summary>Auth/method/secret rules shared by the create and update subscription commands.</summary>
public static class WebhookConnectionRules
{
    /// <summary>Limit on the plaintext secret; the stored (encrypted) column is wider.</summary>
    public const int MaxSecretLength = 256;

    public static bool BeAKnownAuthType(string authType) =>
        authType is WebhookAuthType.Hmac or WebhookAuthType.TokenBearer;

    public static bool BeAnAllowedMethod(string httpMethod) => httpMethod is "POST" or "PUT";

    /// <summary>
    /// TokenEndpoint + ClientId rules for a TokenBearer subscription (limits match the EF config).
    /// The ClientSecret rule differs between create (required) and update (optional), so callers add it.
    /// </summary>
    public static void AddTokenBearerRules<T>(
        AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string?>> tokenEndpoint,
        System.Linq.Expressions.Expression<Func<T, string?>> clientId)
    {
        validator.RuleFor(tokenEndpoint)
            .NotEmpty().MaximumLength(500)
            .Must(url => WebhookUrlRules.BeAnAbsoluteHttpUrl(url!))
            .WithMessage("TokenEndpoint must be an absolute http(s) URL.");
        validator.RuleFor(clientId).NotEmpty().MaximumLength(100);
    }
}
