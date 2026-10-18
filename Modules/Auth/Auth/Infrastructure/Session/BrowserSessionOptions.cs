using System.Text.RegularExpressions;

namespace Auth.Infrastructure.Session;

/// <summary>Settings of the browser session, bound from <c>Auth:Session</c>.</summary>
public sealed partial class BrowserSessionOptions
{
    public const string SectionName = "Auth:Session";

    /// <summary>
    /// Appended to the cookie names. SIT1-4 (and UAT2-4) share one host and cookies are scoped by host, not port,
    /// so without a per-environment suffix one environment's session overwrites another's. Leave it unset: it is
    /// then derived from the port of <c>AppBaseUrl</c> (<c>-7112</c>), which already differs between environments
    /// that share a host. Set it only to override (letters, digits and '-').
    /// </summary>
    public string CookieSuffix { get; set; } = "";

    /// <summary>Minutes without real activity before the session ends. Background polling does not count.</summary>
    public int IdleTimeoutMinutes { get; set; } = 30;

    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(IdleTimeoutMinutes);

    public string AppCookieName => $"__Host-cas-session{CookieSuffix}";
    public string IdpCookieName => $"__Host-cas-idp{CookieSuffix}";

    /// <summary>Short-lived marker that a sign-in just failed once (see <see cref="AppSessionEvents.RemoteFailureAsync"/>).</summary>
    public string RetryCookieName => $"cas-oidc-retry{CookieSuffix}";

    /// <summary>
    /// Validates the settings and fills in the cookie suffix when none is configured. An explicit suffix that is not
    /// a valid cookie-name token — an unsubstituted deployment token such as <c>#{...}#</c> — fails here instead of
    /// silently producing a broken or colliding cookie. Safe to call more than once.
    /// </summary>
    public BrowserSessionOptions Resolve(string appBaseUrl)
    {
        if (!SuffixPattern().IsMatch(CookieSuffix ?? ""))
            throw new InvalidOperationException(
                $"Auth:Session:CookieSuffix '{CookieSuffix}' is invalid: use letters, digits and '-' only (e.g. '-sit1'), " +
                "or leave it unset to derive it from the AppBaseUrl port. If this is a #{...}# token, the deployment did not substitute it.");
        if (IdleTimeoutMinutes < 1)
            throw new InvalidOperationException("Auth:Session:IdleTimeoutMinutes must be at least 1.");

        if (string.IsNullOrEmpty(CookieSuffix))
            CookieSuffix = $"-{new Uri(appBaseUrl).Port}";
        return this;
    }

    [GeneratedRegex("^[A-Za-z0-9-]*$")]
    private static partial Regex SuffixPattern();
}

/// <summary>The OIDC client the API uses to sign users in against our own OpenIddict (<c>Auth:Oidc</c>).</summary>
public sealed class OidcClientOptions
{
    public const string SectionName = "Auth:Oidc";

    /// <summary>The existing public <c>spa</c> client (PKCE, no secret).</summary>
    public string ClientId { get; set; } = "spa";

    /// <summary>Empty while the client is public. Set (ENC:v1) when the IdP is split out and the client becomes confidential.</summary>
    public string? ClientSecret { get; set; }
}
