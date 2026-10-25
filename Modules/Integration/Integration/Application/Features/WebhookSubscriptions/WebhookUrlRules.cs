using System.Diagnostics.CodeAnalysis;

namespace Integration.Application.Features.WebhookSubscriptions;

/// <summary>Shared callback-URL validation used by the create and update subscription commands.</summary>
public static class WebhookUrlRules
{
    public static bool BeAnAbsoluteHttpUrl(string url) => TryParseHttp(url, out _);

    public static bool TryParseHttp(string url, [NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(url, UriKind.Absolute, out uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
