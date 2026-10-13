namespace Auth.Infrastructure.Session;

/// <summary>Scheme names, header names and claim types of the browser (BFF) session.</summary>
public static class AuthSchemes
{
    /// <summary>Policy scheme that picks the real scheme per request: dev header, then Bearer, then app cookie.</summary>
    public const string Selector = "CasAuth";

    /// <summary>The app session cookie (the SPA's only credential). Backed by <c>auth.UserSessions</c>.</summary>
    public const string AppCookie = "CasSession";

    /// <summary>OIDC handler that signs the user in against our own OpenIddict.</summary>
    public const string Oidc = "oidc";

    /// <summary>Claim carrying the <c>auth.UserSessions</c> id inside the app principal.</summary>
    public const string SessionIdClaim = "sid";

    /// <summary>Set (to true) on the app principal while the user still owes a password change.</summary>
    public const string MustChangePasswordClaim = "must_change_password";

    /// <summary>The SPA marks timer-driven polling with this header (value "1"); it never counts as activity.</summary>
    public const string BackgroundHeader = "X-Background-Request";

    // HttpContext.Items keys.
    internal const string EndReasonItem = "cas.session.end-reason";

    /// <summary>
    /// Why this request's session is gone, for the 401's <c>X-Session-End</c> header. Kept apart from
    /// <see cref="EndReasonItem"/> (the input of <c>RemoveAsync</c>) so reporting a reason can never change the one
    /// a later sign-out records.
    /// </summary>
    internal const string EndHintItem = "cas.session.end-hint";
    internal const string LogoutUrlItem = "cas.session.logout-url";

    // AuthenticationProperties.Items key: set on the second login attempt so a failing callback
    // cannot bounce between /auth/login and the IdP forever.
    internal const string RetryItem = "cas.retry";
}
