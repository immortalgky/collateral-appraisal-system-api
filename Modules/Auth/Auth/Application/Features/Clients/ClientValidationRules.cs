namespace Auth.Application.Features.Clients;

/// <summary>
/// Validation predicates shared by the register and update client commands so the two paths
/// can never diverge.
/// </summary>
public static class ClientValidationRules
{
    public static bool IsKnownGrantType(string grantType) =>
        ClientPermissionMapper.AllGrantTypes.Contains(grantType, StringComparer.OrdinalIgnoreCase);

    /// <summary>Redirect/post-logout URIs must be absolute http(s) — rejects relative and javascript:/data: schemes.</summary>
    public static bool IsAbsoluteHttpUri(Uri uri) =>
        uri is { IsAbsoluteUri: true }
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Bounds one per-client token lifetime. Null is valid and means "fall back to the server
    /// default"; callers gate on HasValue before invoking this. Rejecting out-of-range values here
    /// matters because OpenIddict ignores a setting it cannot use and silently falls back, so an
    /// unchecked value would look saved while changing nothing.
    /// </summary>
    public static bool IsValidLifetime(int? minutes, ClientPermissionMapper.TokenLifetimeKind kind) =>
        minutes is { } value && value >= kind.MinMinutes && value <= kind.MaxMinutes;

    public static string LifetimeMessage(string label, ClientPermissionMapper.TokenLifetimeKind kind) =>
        $"{label} must be between {kind.MinMinutes} and {kind.MaxMinutes} minutes, or empty to use the server default.";
}
