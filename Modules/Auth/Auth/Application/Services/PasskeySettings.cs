using Auth.Infrastructure.Session;

namespace Auth.Application.Services;

/// <summary>
/// Where passkeys are valid. The relying-party id is the host of the identity provider's public address: a credential is
/// bound to it for life, and the browser only accepts an id that is the page's own host or a parent domain of it. The
/// origins are the pages allowed to ask: the IdP itself (sign-in) and the SPA (registration from the profile).
/// </summary>
public sealed class PasskeySettings
{
    public const string RpName = "Collateral Appraisal System";

    public string RpId { get; }
    public IReadOnlySet<string> Origins { get; }

    /// <summary>WebAuthn does not accept an IP address as a relying-party id: no passkey can work on this address.</summary>
    public bool RpIdIsIpAddress => System.Net.IPAddress.TryParse(RpId.Trim('[', ']'), out _);

    /// <summary>Origins from which the browser will refuse to use <see cref="RpId"/>: a passkey could not be registered there.</summary>
    public IReadOnlyList<string> MismatchedOrigins { get; }

    public PasskeySettings(BffUrls urls)
    {
        RpId = new Uri(urls.IdpPublicBase).Host;
        var origins = new[] { urls.IdpPublicBase }.Concat(urls.CorsOrigins)
            .Select(o => o.TrimEnd('/')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Origins = origins.ToHashSet(StringComparer.OrdinalIgnoreCase);
        MismatchedOrigins = origins.Where(o => !CanUse(RpId, o)).ToList();
    }

    /// <summary>The browser's rule: the relying-party id is the origin's host, or a parent domain of it.</summary>
    internal static bool CanUse(string rpId, string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        return string.Equals(uri.Host, rpId, StringComparison.OrdinalIgnoreCase)
               || uri.Host.EndsWith("." + rpId, StringComparison.OrdinalIgnoreCase);
    }
}
