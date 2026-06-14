namespace Integration.Application.Features.WebhookSubscriptions;

/// <summary>Shared callback-URL validation used by the create and update subscription commands.</summary>
public static class WebhookUrlRules
{
    public static bool BeAnAbsoluteHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
