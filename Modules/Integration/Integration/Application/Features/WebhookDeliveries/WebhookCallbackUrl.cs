using Integration.Application.Features.WebhookSubscriptions;

namespace Integration.Application.Features.WebhookDeliveries;

/// <summary>
/// Callback URLs are shown to WEBHOOK_DELIVERIES_VIEW holders, who cannot manage subscriptions, so the
/// display form is scheme + host + port + path only: userinfo, query string and fragment are dropped.
/// A value that is not a parseable absolute http(s) URL is not echoed at all — stripping secrets out of
/// a string we cannot parse is guesswork.
/// </summary>
public static class WebhookCallbackUrl
{
    public const string InvalidPlaceholder = "[invalid URL]";

    public static string ToDisplay(string url)
    {
        if (!WebhookUrlRules.TryParseHttp(url, out var uri))
            return InvalidPlaceholder;

        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
        return $"{uri.Scheme}://{uri.Host}{port}{uri.AbsolutePath}";
    }
}
